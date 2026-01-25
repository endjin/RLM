// <copyright file="UniformChunkerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Chunking;

[TestClass]
public sealed class UniformChunkerTests
{
    [TestMethod]
    public async Task ChunkAsync_ContentSmallerThanChunkSize_ReturnsSingleChunk()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("test-doc")
            .WithContent("Short content")
            .Build();
        UniformChunker chunker = new(chunkSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("Short content");
        chunks[0].Index.ShouldBe(0);
        chunks[0].StartPosition.ShouldBe(0);
        chunks[0].EndPosition.ShouldBe(13);
    }

    [TestMethod]
    public async Task ChunkAsync_ContentExactlyChunkSize_ReturnsSingleChunk()
    {
        // Arrange
        string content = new('x', 100);
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        UniformChunker chunker = new(chunkSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.Length.ShouldBe(100);
    }

    [TestMethod]
    public async Task ChunkAsync_ContentLargerThanChunkSize_ReturnsMultipleChunks()
    {
        // Arrange
        string content = new('x', 250);
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        UniformChunker chunker = new(chunkSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(3);
        chunks[0].Content.Length.ShouldBe(100);
        chunks[1].Content.Length.ShouldBe(100);
        chunks[2].Content.Length.ShouldBe(50);
    }

    [TestMethod]
    public async Task ChunkAsync_WithOverlap_ChunksOverlap()
    {
        // Arrange
        string content = "AAAAABBBBBCCCCC"; // 15 chars
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        UniformChunker chunker = new(chunkSize: 10, overlap: 5);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert - with step=5, we get chunks at positions 0, 5, 10
        chunks.Count.ShouldBe(3);
        chunks[0].Content.ShouldBe("AAAAABBBBB"); // 0-10
        chunks[1].Content.ShouldBe("BBBBBCCCCC"); // 5-15
        chunks[2].Content.ShouldBe("CCCCC");      // 10-15
        chunks[0].StartPosition.ShouldBe(0);
        chunks[1].StartPosition.ShouldBe(5);
        chunks[2].StartPosition.ShouldBe(10);
    }

    [TestMethod]
    public async Task ChunkAsync_SetsCorrectMetadata()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("doc-123")
            .WithContent(new('x', 150))
            .Build();
        UniformChunker chunker = new(chunkSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        foreach (ContentChunk chunk in chunks)
        {
            chunk.Metadata["documentId"].ShouldBe("doc-123");
            chunk.Metadata["totalChunks"].ShouldBe("2");
            chunk.Metadata["strategy"].ShouldBe("uniform");
        }
    }

    [TestMethod]
    public async Task ChunkAsync_IndicesAreSequential()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(new('x', 300))
            .Build();
        UniformChunker chunker = new(chunkSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Index.ShouldBe(i);
        }
    }

    [TestMethod]
    public async Task ChunkAsync_PositionsAreCorrect()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(new('x', 250))
            .Build();
        UniformChunker chunker = new(chunkSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks[0].StartPosition.ShouldBe(0);
        chunks[0].EndPosition.ShouldBe(100);
        chunks[1].StartPosition.ShouldBe(100);
        chunks[1].EndPosition.ShouldBe(200);
        chunks[2].StartPosition.ShouldBe(200);
        chunks[2].EndPosition.ShouldBe(250);
    }

    [TestMethod]
    public async Task ChunkAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(new('x', 1000))
            .Build();
        UniformChunker chunker = new(chunkSize: 10);

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
    public async Task ChunkAsync_EmptyContent_ReturnsNoChunks()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("")
            .Build();
        UniformChunker chunker = new(chunkSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task ChunkAsync_LargeOverlap_CorrectlyCalculatesTotalChunks()
    {
        // Arrange
        string content = new('x', 100);
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        UniformChunker chunker = new(chunkSize: 50, overlap: 25);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert - with step=25, we get chunks at positions 0, 25, 50, 75
        chunks.Count.ShouldBe(4);
        chunks[0].StartPosition.ShouldBe(0);
        chunks[1].StartPosition.ShouldBe(25);
        chunks[2].StartPosition.ShouldBe(50);
        chunks[3].StartPosition.ShouldBe(75);
    }

    private static async Task<List<ContentChunk>> CollectChunksAsync(
        IChunker chunker,
        Rlm.Cli.Core.Documents.RlmDocument document,
        CancellationToken cancellationToken = default)
    {
        List<ContentChunk> chunks = [];
        await foreach (ContentChunk chunk in chunker.ChunkAsync(document, cancellationToken))
        {
            chunks.Add(chunk);
        }
        return chunks;
    }

    public TestContext TestContext { get; set; }
}