// <copyright file="IChunker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Core.Chunking;

/// <summary>
/// Splits documents into chunks for processing, analogous to IngestionChunker.
/// Uses IAsyncEnumerable for memory-efficient streaming.
/// </summary>
public interface IChunker
{
    /// <summary>
    /// Chunks the document into smaller segments.
    /// </summary>
    /// <param name="document">The document to chunk.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of content chunks.</returns>
    IAsyncEnumerable<ContentChunk> ChunkAsync(
        RlmDocument document,
        CancellationToken cancellationToken = default);
}