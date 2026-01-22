// <copyright file="RlmSessionBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Core.Session;

namespace Rlm.Cli.Tests.Builders;

/// <summary>
/// Builder for creating RlmSession test instances.
/// </summary>
public sealed class RlmSessionBuilder
{
    private string? content;
    private DocumentMetadata? metadata;
    private List<ContentChunk> chunkBuffer = [];
    private int currentChunkIndex;
    private Dictionary<string, string> results = [];

    public RlmSessionBuilder WithContent(string content)
    {
        this.content = content;
        return this;
    }

    public RlmSessionBuilder WithMetadata(DocumentMetadata metadata)
    {
        this.metadata = metadata;
        return this;
    }

    public RlmSessionBuilder WithMetadata(Action<DocumentMetadataBuilder> configure)
    {
        DocumentMetadataBuilder builder = DocumentMetadataBuilder.Default();
        configure(builder);
        metadata = builder.Build();
        return this;
    }

    public RlmSessionBuilder WithDocument(RlmDocument document)
    {
        content = document.Content;
        metadata = document.Metadata;
        return this;
    }

    public RlmSessionBuilder WithDocument(Action<RlmDocumentBuilder> configure)
    {
        RlmDocumentBuilder builder = RlmDocumentBuilder.Default();
        configure(builder);
        RlmDocument doc = builder.Build();
        content = doc.Content;
        metadata = doc.Metadata;
        return this;
    }

    public RlmSessionBuilder WithChunk(ContentChunk chunk)
    {
        chunkBuffer.Add(chunk);
        return this;
    }

    public RlmSessionBuilder WithChunk(Action<ContentChunkBuilder> configure)
    {
        ContentChunkBuilder builder = ContentChunkBuilder.Default();
        configure(builder);
        chunkBuffer.Add(builder.Build());
        return this;
    }

    public RlmSessionBuilder WithChunks(IEnumerable<ContentChunk> chunks)
    {
        chunkBuffer = [.. chunks];
        return this;
    }

    public RlmSessionBuilder WithChunks(int count, Func<int, ContentChunk>? chunkFactory = null)
    {
        chunkFactory ??= i => ContentChunkBuilder.Default()
            .WithIndex(i)
            .WithContent($"Chunk {i} content")
            .WithPositions(i * 100, (i + 1) * 100)
            .Build();

        chunkBuffer = Enumerable.Range(0, count).Select(chunkFactory).ToList();
        return this;
    }

    public RlmSessionBuilder WithCurrentChunkIndex(int index)
    {
        currentChunkIndex = index;
        return this;
    }

    public RlmSessionBuilder WithResult(string key, string value)
    {
        results[key] = value;
        return this;
    }

    public RlmSessionBuilder WithResults(Dictionary<string, string> results)
    {
        this.results = new Dictionary<string, string>(results);
        return this;
    }

    public RlmSession Build()
    {
        RlmSession session = new()
        {
            Content = content,
            Metadata = metadata,
            ChunkBuffer = chunkBuffer,
            CurrentChunkIndex = currentChunkIndex,
            Results = results
        };
        return session;
    }

    public static RlmSessionBuilder Default() => new();

    /// <summary>
    /// Creates an empty session (no document loaded).
    /// </summary>
    public static RlmSessionBuilder Empty() => new();

    /// <summary>
    /// Creates a session with a document loaded.
    /// </summary>
    public static RlmSessionBuilder WithLoadedDocument() => new RlmSessionBuilder()
        .WithContent("Test document content for processing.")
        .WithMetadata(m => m
            .WithSource("/test/document.txt")
            .WithTotalLength(37)
            .WithLineCount(1));

    /// <summary>
    /// Creates a session with chunks available.
    /// </summary>
    public static RlmSessionBuilder WithLoadedChunks(int chunkCount = 3) =>
        WithLoadedDocument()
            .WithChunks(chunkCount);

    /// <summary>
    /// Creates a session with stored results.
    /// </summary>
    public static RlmSessionBuilder WithStoredResults() =>
        WithLoadedChunks()
            .WithResult("chunk_0", "Result for chunk 0")
            .WithResult("chunk_1", "Result for chunk 1")
            .WithResult("chunk_2", "Result for chunk 2");
}