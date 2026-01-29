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
        
        // This test verifies that WordDocumentReader correctly detects headings
        // and preserves them as Markdown-style headings in the document content.
        
        string content = document.Content;
        content.ShouldContain("# Heading Level 1");
        content.ShouldContain("## Heading Level 2");
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
        
        // Empty paragraph with heading style should be just the hashes? Or maybe handled gracefully?
        // Implementation: content.Append(new string('#', level)).Append(' '); then append text.
        // So empty paragraph -> "# " + Environment.NewLine
        content.ShouldContain("# " + Environment.NewLine);
    }

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
        void AddStyledPara(string text, string styleId)
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

    public TestContext TestContext { get; set; }
}