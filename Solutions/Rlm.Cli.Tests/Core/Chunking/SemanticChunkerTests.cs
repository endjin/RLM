// <copyright file="SemanticChunkerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Chunking;

[TestClass]
public sealed class SemanticChunkerTests
{
    [TestMethod]
    public async Task ChunkAsync_NoHeaders_ReturnsSingleChunk()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("test-doc")
            .WithContent("Just plain text without any headers.")
            .Build();
        SemanticChunker chunker = new();

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe("Just plain text without any headers.");
        chunks[0].Index.ShouldBe(0);
        chunks[0].Metadata["strategy"].ShouldBe("semantic");
    }

    [TestMethod]
    public async Task ChunkAsync_SingleHeader_ReturnsSingleChunk()
    {
        // Arrange
        string content = """
                         # Introduction

                         This is the introduction content.
                         """;
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("test-doc")
            .WithContent(content)
            .Build();
        SemanticChunker chunker = new();

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Metadata["sectionHeader"].ShouldBe("Introduction");
        chunks[0].Metadata["headerLevel"].ShouldBe("1");
        chunks[0].Metadata["headerPath"].ShouldBe("Introduction");
    }

    [TestMethod]
    public async Task ChunkAsync_MultipleHeaders_ReturnsMultipleChunks()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.WithMarkdownContent().Build();
        SemanticChunker chunker = new();

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBeGreaterThan(1);
        chunks[0].Metadata["sectionHeader"].ShouldBe("Introduction");
    }

    [TestMethod]
    public async Task ChunkAsync_NestedHeaders_BuildsHeaderPath()
    {
        // Arrange
        string content = """
                         # Chapter 1

                         Introduction text.

                         ## Section A

                         Section A content.

                         ### Subsection A.1

                         Subsection content.
                         """;
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        SemanticChunker chunker = new();

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(3);

        // First chunk: # Chapter 1
        chunks[0].Metadata["headerPath"].ShouldBe("Chapter 1");
        chunks[0].Metadata["headerLevel"].ShouldBe("1");

        // Second chunk: ## Section A
        chunks[1].Metadata["headerPath"].ShouldBe("Chapter 1 > Section A");
        chunks[1].Metadata["headerLevel"].ShouldBe("2");

        // Third chunk: ### Subsection A.1
        chunks[2].Metadata["headerPath"].ShouldBe("Chapter 1 > Section A > Subsection A.1");
        chunks[2].Metadata["headerLevel"].ShouldBe("3");
    }

    [TestMethod]
    public async Task ChunkAsync_SiblingHeaders_ResetHeaderPath()
    {
        // Arrange
        string content = """
                         # Chapter 1

                         Content 1.

                         ## Section A

                         Section A content.

                         ## Section B

                         Section B content.
                         """;
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        SemanticChunker chunker = new();

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(3);

        // Section B should reset path to just Chapter 1 > Section B
        chunks[2].Metadata["headerPath"].ShouldBe("Chapter 1 > Section B");
    }

    [TestMethod]
    public async Task ChunkAsync_MinLevelFilter_IgnoresLowerLevelHeaders()
    {
        // Arrange
        string content = """
                         # Chapter 1

                         Content 1.

                         ## Section A

                         Section A content.

                         ### Subsection A.1

                         Subsection content.
                         """;
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        SemanticChunker chunker = new(minLevel: 2, maxLevel: 3);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(2);
        chunks[0].Metadata["sectionHeader"].ShouldBe("Section A");
        chunks[1].Metadata["sectionHeader"].ShouldBe("Subsection A.1");
    }

    [TestMethod]
    public async Task ChunkAsync_MaxLevelFilter_IgnoresHigherLevelHeaders()
    {
        // Arrange
        string content = """
                         # Chapter 1

                         Content 1.

                         ## Section A

                         Section A content.

                         ### Subsection A.1

                         Subsection content.
                         """;
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        SemanticChunker chunker = new(minLevel: 1, maxLevel: 2);

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(2);
        chunks[0].Metadata["sectionHeader"].ShouldBe("Chapter 1");
        chunks[1].Metadata["sectionHeader"].ShouldBe("Section A");
    }

    [TestMethod]
    public async Task ChunkAsync_SetsDocumentIdInMetadata()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("my-document")
            .WithContent("# Header\n\nContent")
            .Build();
        SemanticChunker chunker = new();

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks[0].Metadata["documentId"].ShouldBe("my-document");
    }

    [TestMethod]
    public async Task ChunkAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        string content = string.Join("\n\n", Enumerable.Range(1, 100).Select(i => $"# Header {i}\n\nContent {i}"));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        SemanticChunker chunker = new();

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
    public async Task ChunkAsync_ContentAfterLastHeader_IncludedInLastChunk()
    {
        // Arrange
        string content = """
                         # Header

                         Content under header.

                         More content without another header.
                         """;
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        SemanticChunker chunker = new();

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldContain("More content without another header.");
    }

    [TestMethod]
    public async Task ChunkAsync_ChunkPositionsAreCorrect()
    {
        // Arrange
        string content = "# First\n\nContent A.\n\n# Second\n\nContent B.";
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();
        SemanticChunker chunker = new();

        // Act
        List<ContentChunk> chunks = await CollectChunksAsync(chunker, document, TestContext.CancellationToken);

        // Assert
        chunks.Count.ShouldBe(2);
        chunks[0].StartPosition.ShouldBe(0);
        chunks[1].StartPosition.ShouldBeGreaterThan(0);
        chunks[1].EndPosition.ShouldBe(content.Length);
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