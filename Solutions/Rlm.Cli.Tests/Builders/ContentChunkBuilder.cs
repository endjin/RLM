// <copyright file="ContentChunkBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Chunking;

namespace Rlm.Cli.Tests.Builders;

/// <summary>
/// Builder for creating ContentChunk test instances.
/// </summary>
public sealed class ContentChunkBuilder
{
    private int index;
    private string content = "Chunk content";
    private int startPosition;
    private int endPosition = 13;
    private Dictionary<string, string> metadata = [];

    public ContentChunkBuilder WithIndex(int index)
    {
        this.index = index;
        return this;
    }

    public ContentChunkBuilder WithContent(string content)
    {
        this.content = content;
        endPosition = startPosition + content.Length;
        return this;
    }

    public ContentChunkBuilder WithStartPosition(int startPosition)
    {
        this.startPosition = startPosition;
        return this;
    }

    public ContentChunkBuilder WithEndPosition(int endPosition)
    {
        this.endPosition = endPosition;
        return this;
    }

    public ContentChunkBuilder WithPositions(int start, int end)
    {
        startPosition = start;
        endPosition = end;
        return this;
    }

    public ContentChunkBuilder WithMetadata(string key, string value)
    {
        metadata[key] = value;
        return this;
    }

    public ContentChunkBuilder WithMetadata(Dictionary<string, string> metadata)
    {
        this.metadata = new Dictionary<string, string>(metadata);
        return this;
    }

    public ContentChunk Build() => new()
    {
        Index = index,
        Content = content,
        StartPosition = startPosition,
        EndPosition = endPosition,
        Metadata = new Dictionary<string, string>(metadata)
    };

    public static ContentChunkBuilder Default() => new();

    /// <summary>
    /// Creates a chunk with uniform strategy metadata.
    /// </summary>
    public static ContentChunkBuilder WithUniformMetadata(string documentId, int totalChunks) =>
        new ContentChunkBuilder()
            .WithMetadata("documentId", documentId)
            .WithMetadata("totalChunks", totalChunks.ToString())
            .WithMetadata("strategy", "uniform");

    /// <summary>
    /// Creates a chunk with semantic strategy metadata.
    /// </summary>
    public static ContentChunkBuilder WithSemanticMetadata(string documentId, string sectionHeader, int headerLevel, string headerPath) =>
        new ContentChunkBuilder()
            .WithMetadata("documentId", documentId)
            .WithMetadata("sectionHeader", sectionHeader)
            .WithMetadata("headerLevel", headerLevel.ToString())
            .WithMetadata("headerPath", headerPath)
            .WithMetadata("strategy", "semantic");

    /// <summary>
    /// Creates a chunk with filtering strategy metadata.
    /// </summary>
    public static ContentChunkBuilder WithFilteringMetadata(string documentId, string matchedTerms, int matchCount) =>
        new ContentChunkBuilder()
            .WithMetadata("documentId", documentId)
            .WithMetadata("matchedTerms", matchedTerms)
            .WithMetadata("matchCount", matchCount.ToString())
            .WithMetadata("strategy", "filtering");
}