// <copyright file="MetadataExtractionProcessorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Documents;

[TestClass]
public sealed class MetadataExtractionProcessorTests
{
    private MetadataExtractionProcessor processor = null!;

    [TestInitialize]
    public void Setup()
    {
        processor = new MetadataExtractionProcessor();
    }

    [TestMethod]
    public async Task ProcessAsync_ExtractsTitleFromH1Header()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("# My Document Title\n\nSome content here.")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.Title.ShouldBe("My Document Title");
        result.Id.ShouldBe("My Document Title");
    }

    [TestMethod]
    public async Task ProcessAsync_ExtractsYamlFrontmatter()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                ---
                title: Frontmatter Title
                author: John Doe
                version: 1.0
                ---

                # Content Header

                Some content here.
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.Title.ShouldBe("Frontmatter Title");
        result.Metadata.ExtendedMetadata.ShouldNotBeNull();
        result.Metadata.ExtendedMetadata!["author"].ShouldBe("John Doe");
        result.Metadata.ExtendedMetadata!["version"].ShouldBe("1.0");
    }

    [TestMethod]
    public async Task ProcessAsync_FrontmatterTitleOverridesH1()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                ---
                title: Frontmatter Title
                ---

                # Content Header

                Some content.
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.Title.ShouldBe("Frontmatter Title");
    }

    [TestMethod]
    public async Task ProcessAsync_RemovesFrontmatterFromContent()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                ---
                title: Test
                ---

                Actual content starts here.
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Content.ShouldNotContain("---");
        result.Content.ShouldNotContain("title: Test");
        result.Content.ShouldStartWith("Actual content");
    }

    [TestMethod]
    public async Task ProcessAsync_CountsWords()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("One two three four five six seven eight nine ten.")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.WordCount.ShouldBe(10);
    }

    [TestMethod]
    public async Task ProcessAsync_CountsHeaders()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # Header 1
                Content
                ## Header 2
                More content
                ### Header 3
                Even more
                #### Header 4
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.HeaderCount.ShouldBe(4);
    }

    [TestMethod]
    public async Task ProcessAsync_CountsCodeBlocks()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # Code Examples

                ```python
                print("Hello")
                ```

                Some text.

                ```javascript
                console.log("World");
                ```
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.CodeBlockCount.ShouldBe(2);
    }

    [TestMethod]
    public async Task ProcessAsync_ExtractsCodeLanguages()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                ```python
                code
                ```

                ```javascript
                code
                ```

                ```python
                more code
                ```
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.CodeLanguages.ShouldNotBeNull();
        result.Metadata.CodeLanguages.ShouldContain("python");
        result.Metadata.CodeLanguages.ShouldContain("javascript");
        result.Metadata.CodeLanguages!.Count.ShouldBe(2); // Distinct languages
    }

    [TestMethod]
    public async Task ProcessAsync_CalculatesReadingTime()
    {
        // Arrange - 400 words = 2 minutes at 200 wpm
        string words = string.Join(" ", Enumerable.Repeat("word", 400));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(words)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.EstimatedReadingTimeMinutes.ShouldBe(2);
    }

    [TestMethod]
    public async Task ProcessAsync_MinimumReadingTimeIsOneMinute()
    {
        // Arrange - Very short content
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Just a few words.")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.EstimatedReadingTimeMinutes.ShouldBe(1);
    }

    [TestMethod]
    public async Task ProcessAsync_DetectsApiDocumentation()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # API Reference

                ## GET /users endpoint

                Request parameters and response format.
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.ExtendedMetadata.ShouldNotBeNull();
        result.Metadata.ExtendedMetadata!["detectedType"].ShouldBe("api-documentation");
    }

    [TestMethod]
    public async Task ProcessAsync_DetectsConfigurationDocumentation()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # Configuration Settings

                Set the config option and environment variable as needed.
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.ExtendedMetadata.ShouldNotBeNull();
        result.Metadata.ExtendedMetadata!["detectedType"].ShouldBe("configuration");
    }

    [TestMethod]
    public async Task ProcessAsync_DetectsTutorialContent()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # Getting Started Tutorial

                Step 1: Install the package
                Step 2: Configure the settings
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.ExtendedMetadata.ShouldNotBeNull();
        result.Metadata.ExtendedMetadata!["detectedType"].ShouldBe("tutorial");
    }

    [TestMethod]
    public async Task ProcessAsync_DetectsChangelogContent()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # Changelog

                ## Version 2.0 Release Notes

                Breaking change in the library.
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.ExtendedMetadata.ShouldNotBeNull();
        result.Metadata.ExtendedMetadata!["detectedType"].ShouldBe("changelog");
    }

    [TestMethod]
    public async Task ProcessAsync_DetectsSpecificationContent()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # Technical Specification

                The system must handle the requirement.
                It shall support multiple formats.
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.ExtendedMetadata.ShouldNotBeNull();
        result.Metadata.ExtendedMetadata!["detectedType"].ShouldBe("specification");
    }

    [TestMethod]
    public async Task ProcessAsync_NoDetectedTypeForGenericContent()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # Generic Document

                This is just some generic content without any specific patterns.
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.ExtendedMetadata?.ContainsKey("detectedType").ShouldBeFalse();
    }

    [TestMethod]
    public async Task ProcessAsync_UpdatesMetadataLengthAndLines()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Line 1\nLine 2\nLine 3")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.TotalLength.ShouldBe(result.Content.Length);
        result.Metadata.LineCount.ShouldBe(3);
    }

    [TestMethod]
    public async Task ProcessAsync_CancellationRequested_ThrowsOperationCancelledException()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default().Build();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => processor.ProcessAsync(document, cts.Token));
    }

    [TestMethod]
    public async Task ProcessAsync_HandlesYamlWithQuotedValues()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                ---
                title: "Quoted Title"
                description: 'Single quoted'
                ---

                Content
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.ExtendedMetadata!["title"].ShouldBe("Quoted Title");
        result.Metadata.ExtendedMetadata!["description"].ShouldBe("Single quoted");
    }

    [TestMethod]
    public async Task ProcessAsync_IgnoresYamlComments()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                ---
                title: Test
                # This is a comment
                author: Jane
                ---

                Content
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.ExtendedMetadata!.ContainsKey("# This is a comment").ShouldBeFalse();
        result.Metadata.ExtendedMetadata!["author"].ShouldBe("Jane");
    }

    [TestMethod]
    public async Task ProcessAsync_NoCodeBlocks_ReturnsNullCodeBlockCount()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Just plain text without any code blocks.")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.CodeBlockCount.ShouldBeNull();
    }

    [TestMethod]
    public async Task ProcessAsync_PreservesOriginalTitleIfNoTitleExtracted()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Content without any headers")
            .WithMetadata(m => m.WithTitle("Original Title"))
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document, TestContext.CancellationToken);

        // Assert
        result.Metadata.Title.ShouldBe("Original Title");
    }

    public TestContext TestContext { get; set; }
}
