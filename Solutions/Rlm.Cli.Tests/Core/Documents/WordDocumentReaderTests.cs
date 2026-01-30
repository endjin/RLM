// <copyright file="WordDocumentReaderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Rlm.Cli.Core.Documents;
using Shouldly;
using Spectre.IO.Testing;

namespace Rlm.Cli.Tests.Core.Documents;

[TestClass]
public sealed class WordDocumentReaderTests
{
    private FakeFileSystem fakeFileSystem = null!;
    private WordDocumentReader reader = null!;
    private string tempFilePath = null!;

    [TestInitialize]
    public void Setup()
    {
        // Setup fake file system for the reader's dependency
        // But note: WordDocumentReader uses System.IO to open the file via OpenXml SDK
        // So we need to ensure we pass checks but use a real file for OpenXml

        // We will use a real file path for the test execution because OpenXml SDK works with real files
        tempFilePath = System.IO.Path.GetTempFileName() + ".docx";

        // We need the fake file system to "know" about this file so CanRead/ReadAsync checks pass
        FakeEnvironment environment = FakeEnvironment.CreateLinuxEnvironment();
        fakeFileSystem = new(environment);

        // Populate fake file system so checks pass
        // The reader implementation checks if file exists using IFileSystem
        // So we must mock that
        fakeFileSystem.CreateFile(tempFilePath);

        reader = new(fakeFileSystem);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(tempFilePath))
        {
            File.Delete(tempFilePath);
        }
    }

    #region CanRead Tests

    [TestMethod]
    public void CanRead_DocxFile_ReturnsTrue()
    {
        Uri uri = new("file:///test/document.docx");
        reader.CanRead(uri).ShouldBeTrue();
    }

    [TestMethod]
    public void CanRead_NonDocxFile_ReturnsFalse()
    {
        Uri uri = new("file:///test/document.pdf");
        reader.CanRead(uri).ShouldBeFalse();
    }

    [TestMethod]
    public void CanRead_NonFileScheme_ReturnsFalse()
    {
        Uri uri = new("http://example.com/document.docx");
        reader.CanRead(uri).ShouldBeFalse();
    }

    [TestMethod]
    public void CanRead_DocxExtensionCaseInsensitive_ReturnsTrue()
    {
        Uri uri = new("file:///test/document.DOCX");
        reader.CanRead(uri).ShouldBeTrue();
    }

    #endregion

    #region ReadAsync Null/Error Path Tests

    [TestMethod]
    public async Task ReadAsync_NonDocxFile_ReturnsNull()
    {
        Uri uri = new("file:///test/document.pdf");
        RlmDocument? result = await reader.ReadAsync(uri, TestContext.CancellationToken);
        result.ShouldBeNull();
    }

    [TestMethod]
    public async Task ReadAsync_FileDoesNotExist_ReturnsNull()
    {
        Uri uri = new("file:///nonexistent/document.docx");
        RlmDocument? result = await reader.ReadAsync(uri, TestContext.CancellationToken);
        result.ShouldBeNull();
    }

    [TestMethod]
    public async Task ReadAsync_CorruptedDocument_ReturnsNull()
    {
        // Create an invalid docx file (just text, not a valid ZIP/OpenXml)
        File.WriteAllText(tempFilePath, "not a valid docx");
        Uri uri = new(tempFilePath);

        RlmDocument? result = await reader.ReadAsync(uri, TestContext.CancellationToken);
        result.ShouldBeNull();
    }

    #endregion

    #region ReadAsync Metadata Tests

    [TestMethod]
    public async Task ReadAsync_DocumentWithMetadata_ExtractsProperties()
    {
        CreateDocxWithMetadata(tempFilePath, title: "Test Title", author: "Test Author");
        Uri uri = new(tempFilePath);

        RlmDocument? document = await reader.ReadAsync(uri, TestContext.CancellationToken);

        document.ShouldNotBeNull();
        document.Id.ShouldBe("Test Title");
        document.Metadata.Title.ShouldBe("Test Title");
        document.Metadata.Author.ShouldBe("Test Author");
    }

    [TestMethod]
    public async Task ReadAsync_DocumentWithoutTitle_UsesFilenameAsId()
    {
        CreateDocxWithHeadings(tempFilePath);
        Uri uri = new(tempFilePath);

        RlmDocument? document = await reader.ReadAsync(uri, TestContext.CancellationToken);

        document.ShouldNotBeNull();
        document.Id.ShouldContain(".docx");
    }

    [TestMethod]
    public async Task ReadAsync_Document_CalculatesWordCount()
    {
        CreateDocxWithKnownContent(tempFilePath, "one two three four five");
        Uri uri = new(tempFilePath);

        RlmDocument? document = await reader.ReadAsync(uri, TestContext.CancellationToken);

        document.ShouldNotBeNull();
        document.Metadata.WordCount.ShouldBe(5);
    }

    [TestMethod]
    public async Task ReadAsync_Document_CalculatesReadingTime()
    {
        // 400 words = 2 minutes reading time (200 words per minute)
        string content = string.Join(" ", Enumerable.Range(1, 400).Select(i => "word"));
        CreateDocxWithKnownContent(tempFilePath, content);
        Uri uri = new(tempFilePath);

        RlmDocument? document = await reader.ReadAsync(uri, TestContext.CancellationToken);

        document.ShouldNotBeNull();
        document.Metadata.EstimatedReadingTimeMinutes.ShouldBe(2);
    }

    #endregion

    #region ReadManyAsync Tests

    [TestMethod]
    public async Task ReadManyAsync_ValidDocument_YieldsOneDocument()
    {
        CreateDocxWithHeadings(tempFilePath);
        Uri uri = new(tempFilePath);

        var documents = new List<RlmDocument>();
        await foreach (var doc in reader.ReadManyAsync(uri, null, TestContext.CancellationToken))
        {
            documents.Add(doc);
        }

        documents.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task ReadManyAsync_InvalidDocument_YieldsNothing()
    {
        Uri uri = new("file:///nonexistent.docx");

        var documents = new List<RlmDocument>();
        await foreach (var doc in reader.ReadManyAsync(uri, null, TestContext.CancellationToken))
        {
            documents.Add(doc);
        }

        documents.ShouldBeEmpty();
    }

    #endregion

    #region ReadAsync Structure Tests

    [TestMethod]
    public async Task ReadAsync_DocumentWithHeadings_PreservesStructure()
    {
        // Arrange
        CreateDocxWithHeadings(tempFilePath);
        Uri uri = new(tempFilePath);

        // Act
        RlmDocument? document = await reader.ReadAsync(uri, TestContext.CancellationToken);

        // Assert
        document.ShouldNotBeNull();

        string content = document.Content;
        content.ShouldContain("# Heading Level 1");
        content.ShouldContain("## Heading Level 2");

        // Verify exact heading count: 1 H1 + 1 H2 = 2 headings total
        string[] lines = content.Split('\n');
        int headingCount = lines.Count(line => line.TrimStart().StartsWith('#'));
        headingCount.ShouldBe(2);
    }

    [TestMethod]
    public async Task ReadAsync_ComplexDocument_PreservesStructureCorrectly()
    {
        // Arrange
        CreateComplexDocx(tempFilePath);
        Uri uri = new(tempFilePath);

        // Act
        RlmDocument? document = await reader.ReadAsync(uri, TestContext.CancellationToken);

        // Assert
        document.ShouldNotBeNull();
        string content = document.Content;

        // H6 should be detected
        content.ShouldContain("###### Heading Level 6");

        // H7 should NOT be detected (treated as normal text)
        content.ShouldContain("Heading Level 7");
        content.ShouldNotContain("####### Heading Level 7");

        // Heading0 should NOT be detected
        content.ShouldContain("Heading Level 0");
        content.ShouldNotContain("# Heading Level 0");

        // Case insensitive check (heading1 vs Heading1)
        content.ShouldContain("# Lowercase Heading 1");

        // Empty paragraph with heading style produces "# " followed by newline
        content.ShouldContain("# " + Environment.NewLine);
    }

    #endregion

    #region Helper Methods

    private static void CreateDocxWithHeadings(string filePath)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        Body body = mainPart.Document.AppendChild(new Body());

        // Heading 1
        Paragraph p1 = body.AppendChild(new Paragraph());
        ParagraphProperties pPr1 = new();
        pPr1.ParagraphStyleId = new ParagraphStyleId() { Val = "Heading1" };
        p1.AppendChild(pPr1);
        Run r1 = p1.AppendChild(new Run());
        r1.AppendChild(new Text("Heading Level 1"));

        // Normal text
        Paragraph p2 = body.AppendChild(new Paragraph());
        Run r2 = p2.AppendChild(new Run());
        r2.AppendChild(new Text("Normal text under H1"));

        // Heading 2
        Paragraph p3 = body.AppendChild(new Paragraph());
        ParagraphProperties pPr3 = new();
        pPr3.ParagraphStyleId = new ParagraphStyleId() { Val = "Heading2" };
        p3.AppendChild(pPr3);
        Run r3 = p3.AppendChild(new Run());
        r3.AppendChild(new Text("Heading Level 2"));

        // More text
        Paragraph p4 = body.AppendChild(new Paragraph());
        Run r4 = p4.AppendChild(new Run());
        r4.AppendChild(new Text("More text under H2"));

        doc.Save();
    }

    private static void CreateComplexDocx(string filePath)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        Body body = mainPart.Document.AppendChild(new Body());

        // Helper to add styled paragraph
        void AddStyledPara(string text, string? styleId)
        {
            Paragraph p = body.AppendChild(new Paragraph());
            if (styleId != null)
            {
                ParagraphProperties pPr = new();
                pPr.ParagraphStyleId = new ParagraphStyleId() { Val = styleId };
                p.AppendChild(pPr);
            }

            Run r = p.AppendChild(new Run());
            r.AppendChild(new Text(text));
        }

        AddStyledPara("Heading Level 6", "Heading6");
        AddStyledPara("Heading Level 7", "Heading7");
        AddStyledPara("Heading Level 0", "Heading0");
        AddStyledPara("Lowercase Heading 1", "heading1");

        // Empty paragraph with Heading1 style
        Paragraph pEmpty = body.AppendChild(new Paragraph());
        ParagraphProperties pPrEmpty = new();
        pPrEmpty.ParagraphStyleId = new ParagraphStyleId() { Val = "Heading1" };
        pEmpty.AppendChild(pPrEmpty);
        // No text run

        doc.Save();
    }

    private static void CreateDocxWithMetadata(string filePath, string? title, string? author)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        doc.PackageProperties.Title = title;
        doc.PackageProperties.Creator = author;

        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        Body body = mainPart.Document.AppendChild(new Body());

        // Add minimal content
        Paragraph p = body.AppendChild(new Paragraph());
        p.AppendChild(new Run()).AppendChild(new Text("Content"));
        doc.Save();
    }

    private static void CreateDocxWithKnownContent(string filePath, string content)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        Body body = mainPart.Document.AppendChild(new Body());

        Paragraph p = body.AppendChild(new Paragraph());
        p.AppendChild(new Run()).AppendChild(new Text(content));
        doc.Save();
    }

    #endregion

    public TestContext TestContext { get; set; } = null!;
}