using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Rlm.Cli.Core.Documents;
using Shouldly;
using Spectre.IO;
using Spectre.IO.Testing;
using System.Text;

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
        
        // This assertion is expected to FAIL currently
        // We want to see:
        // # Heading Level 1
        // Normal text
        // ## Heading Level 2
        // More text
        
        string content = document.Content;
        content.ShouldContain("# Heading Level 1");
        content.ShouldContain("## Heading Level 2");
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

    public TestContext TestContext { get; set; }
}