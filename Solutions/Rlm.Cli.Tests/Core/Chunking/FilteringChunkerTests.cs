// <copyright file="FilteringChunkerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Chunking;

[TestClass]
public sealed class FilteringChunkerTests
{
    [TestMethod]
    public async Task ChunkAsync_NoMatches_ReturnsNoChunks()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("This content has no email addresses.")
            .Build();
        FilteringChunker chunker = new(pattern: @"\b[\w.-]+@[\w.-]+\.\w+\b");

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task ChunkAsync_SingleMatch_ReturnsOneChunk()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Contact us at test@example.com for support.")
            .Build();
        FilteringChunker chunker = new(pattern: @"[\w.-]+@[\w.-]+\.\w+", contextSize: 10);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldContain("test@example.com");
        chunks[0].Metadata["matchCount"].ShouldBe("1");
    }

    [TestMethod]
    public async Task ChunkAsync_MultipleDistantMatches_ReturnsMultipleChunks()
    {
        // Arrange
        string content = "First email: a@b.com " + new string('x', 500) + " Second email: c@d.com";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        FilteringChunker chunker = new(pattern: @"[\w.-]+@[\w.-]+\.\w+", contextSize: 50);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(2);
        chunks[0].Content.ShouldContain("a@b.com");
        chunks[1].Content.ShouldContain("c@d.com");
    }

    [TestMethod]
    public async Task ChunkAsync_NearbyMatches_MergesIntoSingleChunk()
    {
        // Arrange
        string content = "Email 1: a@b.com and Email 2: c@d.com are here.";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        FilteringChunker chunker = new(pattern: @"[\w.-]+@[\w.-]+\.\w+", contextSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldContain("a@b.com");
        chunks[0].Content.ShouldContain("c@d.com");
        chunks[0].Metadata["matchCount"].ShouldBe("2");
    }

    [TestMethod]
    public async Task ChunkAsync_SetsCorrectMetadata()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("doc-123")
            .WithContent("Contact: test@example.com")
            .Build();
        FilteringChunker chunker = new(pattern: @"[\w.-]+@[\w.-]+\.\w+");

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks[0].Metadata["documentId"].ShouldBe("doc-123");
        chunks[0].Metadata["strategy"].ShouldBe("filtering");
        chunks[0].Metadata["matchedTerms"].ShouldContain("test@example.com");
    }

    [TestMethod]
    public async Task ChunkAsync_ContextSize_IncludesContextAroundMatch()
    {
        // Arrange
        string content = "AAA BBB CCC test@example.com DDD EEE FFF";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        FilteringChunker chunker = new(pattern: @"[\w.-]+@[\w.-]+\.\w+", contextSize: 8);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks[0].Content.ShouldContain("CCC");
        chunks[0].Content.ShouldContain("DDD");
    }

    [TestMethod]
    public async Task ChunkAsync_MatchAtBeginning_IncludesFromStart()
    {
        // Arrange
        string content = "test@example.com is at the beginning.";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        FilteringChunker chunker = new(pattern: @"[\w.-]+@[\w.-]+\.\w+", contextSize: 20);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks[0].StartPosition.ShouldBe(0);
    }

    [TestMethod]
    public async Task ChunkAsync_MatchAtEnd_IncludesToEnd()
    {
        // Arrange
        string content = "Email is at the end: test@example.com";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        FilteringChunker chunker = new(pattern: @"[\w.-]+@[\w.-]+\.\w+", contextSize: 20);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks[0].EndPosition.ShouldBe(content.Length);
    }

    [TestMethod]
    public async Task ChunkAsync_CaseInsensitive_MatchesBothCases()
    {
        // Arrange
        string content = "Find KEYWORD and keyword in this text.";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        FilteringChunker chunker = new(pattern: "keyword", contextSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Metadata["matchCount"].ShouldBe("2");
    }

    [TestMethod]
    public async Task ChunkAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        string content = string.Join(" ", Enumerable.Repeat("word test@example.com", 100));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        FilteringChunker chunker = new(pattern: @"[\w.-]+@[\w.-]+\.\w+", contextSize: 5);

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
    public async Task ChunkAsync_IndicesAreSequential()
    {
        // Arrange
        string content = "a@b.com " + new string('x', 1000) + " c@d.com " + new string('y', 1000) + " e@f.com";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        FilteringChunker chunker = new(pattern: @"[\w.-]+@[\w.-]+\.\w+", contextSize: 50);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Index.ShouldBe(i);
        }
    }

    [TestMethod]
    public async Task ChunkAsync_MatchedTermsMetadata_ListsAllMatches()
    {
        // Arrange
        string content = "Contact a@b.com and c@d.com for help.";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        FilteringChunker chunker = new(pattern: @"[\w.-]+@[\w.-]+\.\w+", contextSize: 100);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        string matchedTerms = chunks[0].Metadata["matchedTerms"];
        matchedTerms.ShouldContain("a@b.com");
        matchedTerms.ShouldContain("c@d.com");
    }

    [TestMethod]
    public async Task ChunkAsync_ZeroContextSize_ReturnsOnlyMatch()
    {
        // Arrange
        string content = "Before test@example.com After";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        FilteringChunker chunker = new(pattern: @"test@example\.com", contextSize: 0);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("test@example.com");
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