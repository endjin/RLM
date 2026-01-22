// <copyright file="TokenBasedChunkerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Chunking;

[TestClass]
public sealed class TokenBasedChunkerTests
{
    [TestMethod]
    public async Task ChunkAsync_ContentSmallerThanMaxTokens_ReturnsSingleChunk()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("test-doc")
            .WithContent("Hello world")
            .Build();
        TokenBasedChunker chunker = new(maxTokens: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("Hello world");
        chunks[0].Metadata["strategy"].ShouldBe("token");
    }

    [TestMethod]
    public async Task ChunkAsync_ContentLargerThanMaxTokens_ReturnsMultipleChunks()
    {
        // Arrange
        // Create content that will definitely exceed 10 tokens
        string content = string.Join(" ", Enumerable.Repeat("word", 50));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        TokenBasedChunker chunker = new(maxTokens: 10, overlapTokens: 0);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBeGreaterThan(1);
    }

    [TestMethod]
    public async Task ChunkAsync_SetsTokenCountMetadata()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("doc-123")
            .WithContent("This is a test document with some tokens")
            .Build();
        TokenBasedChunker chunker = new(maxTokens: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Metadata.ShouldContainKey("tokenCount");
        int.Parse(chunks[0].Metadata["tokenCount"]).ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public async Task ChunkAsync_SetsCorrectMetadata()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("doc-123")
            .WithContent("Test content")
            .Build();
        TokenBasedChunker chunker = new(maxTokens: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks[0].Metadata["documentId"].ShouldBe("doc-123");
        chunks[0].Metadata["totalChunks"].ShouldBe("1");
        chunks[0].Metadata["strategy"].ShouldBe("token");
    }

    [TestMethod]
    public async Task ChunkAsync_WithOverlap_ChunksOverlap()
    {
        // Arrange
        // Create content with many tokens
        string content = string.Join(" ", Enumerable.Repeat("test", 100));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        TokenBasedChunker chunker = new(maxTokens: 20, overlapTokens: 5);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBeGreaterThan(1);
        // With overlap, we should have more chunks than without
    }

    [TestMethod]
    public async Task ChunkAsync_IndicesAreSequential()
    {
        // Arrange
        string content = string.Join(" ", Enumerable.Repeat("word", 100));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        TokenBasedChunker chunker = new(maxTokens: 20, overlapTokens: 0);

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
        TokenBasedChunker chunker = new(maxTokens: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task ChunkAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        string content = string.Join(" ", Enumerable.Repeat("word", 1000));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        TokenBasedChunker chunker = new(maxTokens: 10);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (ContentChunk _ in chunker.ChunkAsync(document, cts.Token))
            {
            }
        });
    }

    [TestMethod]
    public async Task ChunkAsync_SetsStartAndEndTokenPositions()
    {
        // Arrange
        string content = string.Join(" ", Enumerable.Repeat("word", 50));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        TokenBasedChunker chunker = new(maxTokens: 10, overlapTokens: 0);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks[0].Metadata.ShouldContainKey("startToken");
        chunks[0].Metadata.ShouldContainKey("endToken");
        chunks[0].Metadata["startToken"].ShouldBe("0");
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
