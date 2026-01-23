// <copyright file="ContentChunk.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Rlm.Cli.Core.Chunking;

/// <summary>
/// Represents a chunk of content from a document, analogous to IngestionChunk.
/// </summary>
public sealed record ContentChunk
{
    /// <summary>
    /// Zero-based index of this chunk in the sequence.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// The chunk content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Start position (character offset) in the original document.
    /// </summary>
    public required int StartPosition { get; init; }

    /// <summary>
    /// End position (character offset) in the original document.
    /// </summary>
    public required int EndPosition { get; init; }

    /// <summary>
    /// Additional metadata about the chunk (e.g., section headers, matched terms).
    /// All values are stored as strings for JSON serialization compatibility.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = [];

    /// <summary>
    /// Length of the chunk content.
    /// </summary>
    public int Length => Content.Length;

    /// <summary>
    /// Estimated token count for this chunk.
    /// </summary>
    public int TokenEstimate => Content.Length / 4;
}