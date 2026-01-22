// <copyright file="RlmSession.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Core.Session;

/// <summary>
/// Represents the current RLM session state, persisted between CLI invocations.
/// </summary>
public sealed class RlmSession
{
    /// <summary>
    /// Maximum allowed recursion depth for decomposition operations.
    /// </summary>
    public const int MaxRecursionDepth = 5;

    /// <summary>
    /// Current recursion depth for tracking nested decomposition operations.
    /// </summary>
    public int RecursionDepth { get; set; }

    /// <summary>
    /// The loaded document content.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Metadata about the loaded document.
    /// </summary>
    public DocumentMetadata? Metadata { get; set; }

    /// <summary>
    /// Buffer of chunks from the last chunking operation.
    /// </summary>
    public List<ContentChunk> ChunkBuffer { get; set; } = [];

    /// <summary>
    /// Current position in the chunk buffer.
    /// </summary>
    public int CurrentChunkIndex { get; set; }

    /// <summary>
    /// Stored partial results.
    /// </summary>
    public Dictionary<string, string> Results { get; set; } = [];

    /// <summary>
    /// Whether a document is currently loaded.
    /// </summary>
    public bool HasDocument => Content is not null;

    /// <summary>
    /// Whether chunks are available.
    /// </summary>
    public bool HasChunks => ChunkBuffer.Count > 0;

    /// <summary>
    /// Whether there are more chunks to process.
    /// </summary>
    public bool HasMoreChunks => CurrentChunkIndex < ChunkBuffer.Count - 1;

    /// <summary>
    /// Gets the current chunk.
    /// </summary>
    public ContentChunk? CurrentChunk =>
        ChunkBuffer.Count > CurrentChunkIndex ? ChunkBuffer[CurrentChunkIndex] : null;

    /// <summary>
    /// Converts session content to an RlmDocument for processing.
    /// </summary>
    public RlmDocument ToDocument() => new()
    {
        Id = Metadata?.Source ?? "session",
        Content = Content ?? string.Empty,
        Metadata = Metadata ?? new DocumentMetadata
        {
            Source = "memory",
            TotalLength = Content?.Length ?? 0,
            TokenEstimate = (Content?.Length ?? 0) / 4,
            LineCount = Content?.Split('\n').Length ?? 0
        }
    };

    /// <summary>
    /// Loads a document into the session.
    /// </summary>
    public void LoadDocument(RlmDocument document)
    {
        Content = document.Content;
        Metadata = document.Metadata;
        ChunkBuffer = [];
        CurrentChunkIndex = 0;
    }

    /// <summary>
    /// Clears the session state.
    /// </summary>
    public void Clear()
    {
        Content = null;
        Metadata = null;
        ChunkBuffer = [];
        CurrentChunkIndex = 0;
        Results = [];
        RecursionDepth = 0;
    }

    /// <summary>
    /// Increments recursion depth and returns whether the limit has been exceeded.
    /// </summary>
    /// <returns>True if the recursion limit has been exceeded.</returns>
    public bool IncrementRecursionDepth()
    {
        RecursionDepth++;
        return RecursionDepth > MaxRecursionDepth;
    }

    /// <summary>
    /// Decrements recursion depth.
    /// </summary>
    public void DecrementRecursionDepth()
    {
        if (RecursionDepth > 0)
        {
            RecursionDepth--;
        }
    }

    /// <summary>
    /// Converts stored results to a ResultBuffer instance.
    /// </summary>
    public ResultBuffer ToResultBuffer()
    {
        ResultBuffer buffer = new();
        foreach ((string key, string value) in Results)
        {
            buffer.Store(key, value);
        }
        return buffer;
    }
}