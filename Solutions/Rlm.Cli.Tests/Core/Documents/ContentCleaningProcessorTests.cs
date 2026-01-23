// <copyright file="ContentCleaningProcessorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Documents;

[TestClass]
public sealed class ContentCleaningProcessorTests
{
    private ContentCleaningProcessor processor = null!;

    [TestInitialize]
    public void Setup()
    {
        processor = new ContentCleaningProcessor();
    }

    [TestMethod]
    public async Task ProcessAsync_RemovesHtmlComments()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Hello <!-- this is a comment --> World")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert - Multiple spaces are normalized to single space
        result.Content.ShouldBe("Hello World");
    }

    [TestMethod]
    public async Task ProcessAsync_RemovesMultilineHtmlComments()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                Before
                <!--
                Multi-line
                comment
                -->
                After
                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert
        result.Content.ShouldNotContain("<!--");
        result.Content.ShouldNotContain("-->");
        result.Content.ShouldNotContain("Multi-line");
        result.Content.ShouldContain("Before");
        result.Content.ShouldContain("After");
    }

    [TestMethod]
    public async Task ProcessAsync_RemovesEmptyLinksWithEmptyUrl()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Click [here]() for nothing")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert - Multiple spaces are normalized to single space
        result.Content.ShouldBe("Click for nothing");
    }

    [TestMethod]
    public async Task ProcessAsync_RemovesEmptyLinksWithEmptyText()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Click [](http://example.com) for nothing")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert - Multiple spaces are normalized to single space
        result.Content.ShouldBe("Click for nothing");
    }

    [TestMethod]
    public async Task ProcessAsync_NormalizesMultipleBlankLines()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Line 1\n\n\n\n\nLine 2")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert - Multiple blank lines are reduced (regex replaces 3+ newlines with 2)
        result.Content.ShouldNotContain("\n\n\n");
        result.Content.ShouldContain("Line 1");
        result.Content.ShouldContain("Line 2");
    }

    [TestMethod]
    public async Task ProcessAsync_NormalizesMultipleSpaces()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Hello    World     Test")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert
        result.Content.ShouldBe("Hello World Test");
    }

    [TestMethod]
    public async Task ProcessAsync_NormalizesMultipleTabs()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Hello\t\t\tWorld")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert
        result.Content.ShouldBe("Hello World");
    }

    [TestMethod]
    public async Task ProcessAsync_TrimsLeadingAndTrailingWhitespace()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("  Line 1  \n  Line 2  \n  Line 3  ")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert
        result.Content.ShouldBe("Line 1\nLine 2\nLine 3");
    }

    [TestMethod]
    public async Task ProcessAsync_TrimsOverallContent()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("\n\n  Content  \n\n")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert
        result.Content.ShouldBe("Content");
    }

    [TestMethod]
    public async Task ProcessAsync_UpdatesMetadata()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Short content\n\n\n\nMore")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert
        result.Metadata.TotalLength.ShouldBe(result.Content.Length);
        result.Metadata.TokenEstimate.ShouldBe(result.Content.Length / 4);
        result.Metadata.LineCount.ShouldBe(result.Content.Split('\n').Length);
    }

    [TestMethod]
    public async Task ProcessAsync_PreservesDocumentId()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("my-document-id")
            .WithContent("Some content")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert
        result.Id.ShouldBe("my-document-id");
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
    public async Task ProcessAsync_EmptyDocument_ReturnsEmptyContent()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("   \n\n   ")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert
        result.Content.ShouldBe("");
    }

    [TestMethod]
    public async Task ProcessAsync_PreservesValidLinks()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Click [here](http://example.com) for more")
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert
        result.Content.ShouldContain("[here](http://example.com)");
    }

    [TestMethod]
    public async Task ProcessAsync_CombinedCleaning()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                  <!-- Header comment -->

                  # Title



                Click [](broken) here    and    [text]() there.



                End   of   document.

                """)
            .Build();

        // Act
        RlmDocument result = await processor.ProcessAsync(document);

        // Assert
        result.Content.ShouldNotContain("<!-- Header comment -->");
        result.Content.ShouldNotContain("[](broken)");
        result.Content.ShouldNotContain("[text]()");
        result.Content.ShouldNotContain("    "); // No multiple spaces
        // Content should be cleaned - verify key elements are present
        result.Content.ShouldContain("# Title");
        result.Content.ShouldContain("here and there");
        result.Content.ShouldContain("End of document.");
    }
}
