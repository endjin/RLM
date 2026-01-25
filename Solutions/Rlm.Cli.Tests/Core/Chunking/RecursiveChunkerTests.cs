// <copyright file="RecursiveChunkerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Chunking;

[TestClass]
public sealed class RecursiveChunkerTests
{
    [TestMethod]
    public async Task ChunkAsync_ContentSmallerThanMaxSize_ReturnsSingleChunk()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("test-doc")
            .WithContent("Short content")
            .Build();
        RecursiveChunker chunker = new(maxChunkSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("Short content");
        chunks[0].Metadata["strategy"].ShouldBe("recursive");
    }

    [TestMethod]
    public async Task ChunkAsync_SplitsOnHeaders()
    {
        // Arrange - Content with H2 headers that need splitting (headers need \n prefix)
        string content = """
            Introduction paragraph.

            ## Section 1
            This is the content for section 1 with enough text to make it substantial.

            ## Section 2
            This is the content for section 2 with enough text to make it substantial.
            """;
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        RecursiveChunker chunker = new(maxChunkSize: 80); // Small enough to force header split

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBeGreaterThan(1);
        chunks.Any(c => c.Metadata["separatorUsed"] == "h2").ShouldBeTrue();
    }

    [TestMethod]
    public async Task ChunkAsync_SplitsOnParagraphs_WhenNoHeaders()
    {
        // Arrange - Content with double newlines for paragraph separation
        string content = "First paragraph with some content that is long enough.\n\nSecond paragraph with more content that is also long.\n\nThird paragraph with additional content that matters.";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        RecursiveChunker chunker = new(maxChunkSize: 60);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBeGreaterThan(1);
        chunks.Any(c => c.Metadata["separatorUsed"] == "paragraph").ShouldBeTrue();
    }

    [TestMethod]
    public async Task ChunkAsync_SplitsOnSentences_WhenParagraphsTooLarge()
    {
        // Arrange
        // Create a long paragraph without breaks
        string content = "First sentence here. Second sentence here. Third sentence here. Fourth sentence here. Fifth sentence here.";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        RecursiveChunker chunker = new(maxChunkSize: 50);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBeGreaterThan(1);
    }

    [TestMethod]
    public async Task ChunkAsync_ForceSplits_WhenNoSeparatorsWork()
    {
        // Arrange
        // Create content without any separators
        string content = new('x', 200);
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        RecursiveChunker chunker = new(maxChunkSize: 50);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBeGreaterThan(1);
        chunks.Any(c => c.Metadata["separatorUsed"] == "forced").ShouldBeTrue();
    }

    [TestMethod]
    public async Task ChunkAsync_SetsCorrectMetadata()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("doc-123")
            .WithContent("Test content")
            .Build();
        RecursiveChunker chunker = new(maxChunkSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks[0].Metadata["documentId"].ShouldBe("doc-123");
        chunks[0].Metadata["totalChunks"].ShouldBe("1");
        chunks[0].Metadata["strategy"].ShouldBe("recursive");
        chunks[0].Metadata.ShouldContainKey("separatorUsed");
    }

    [TestMethod]
    public async Task ChunkAsync_IndicesAreSequential()
    {
        // Arrange
        string content = """
            ## Section 1
            Content 1

            ## Section 2
            Content 2

            ## Section 3
            Content 3
            """;
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        RecursiveChunker chunker = new(maxChunkSize: 30);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Index.ShouldBe(i);
        }
    }

    [TestMethod]
    public async Task ChunkAsync_EmptyContent_ReturnsNoChunks()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("")
            .Build();
        RecursiveChunker chunker = new(maxChunkSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task ChunkAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        string content = string.Join("\n\n", Enumerable.Repeat("Paragraph content here.", 100));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        RecursiveChunker chunker = new(maxChunkSize: 50);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (ContentChunk _ in chunker.ChunkAsync(document, cts.Token))
            {
                // Intentionally empty - iterating to trigger cancellation check
            }
        });
    }

    [TestMethod]
    public async Task ChunkAsync_RespectsMinChunkSize()
    {
        // Arrange
        string content = "a b c d e f g h i j k l m n o p q r s t u v w x y z";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        RecursiveChunker chunker = new(maxChunkSize: 10, minChunkSize: 5);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.ShouldNotBeEmpty();
    }

    [TestMethod]
    public async Task ChunkAsync_H3Headers_UsesH3Separator()
    {
        // Arrange - Content with H3 headers that needs splitting (headers need \n prefix)
        string content = "Introduction text.\n\n### Subsection 1\nThis is the content for subsection 1 with substantial text.\n\n### Subsection 2\nThis is the content for subsection 2 with substantial text.";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        RecursiveChunker chunker = new(maxChunkSize: 80);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBeGreaterThan(1);
        chunks.Any(c => c.Metadata["separatorUsed"] == "h3").ShouldBeTrue();
    }

    private static async Task<List<ContentChunk>> CollectChunksAsync(
        IChunker chunker,
        RlmDocument document,
        CancellationToken cancellationToken = default)
    {
        List<ContentChunk> chunks = [];
        await foreach (ContentChunk chunk in chunker.ChunkAsync(document, cancellationToken))
        {
            chunks.Add(chunk);
        }
        return chunks;
    }

    public TestContext TestContext { get; set; } = null!;
}
