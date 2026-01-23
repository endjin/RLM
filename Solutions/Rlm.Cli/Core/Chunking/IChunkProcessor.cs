// <copyright file="IChunkProcessor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Rlm.Cli.Core.Chunking;

/// <summary>
/// Processes chunks after chunking to enrich them with additional metadata or transformations.
/// </summary>
public interface IChunkProcessor
{
    /// <summary>
    /// Processes a chunk and returns the enriched result.
    /// </summary>
    /// <param name="chunk">The chunk to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The processed chunk with enriched metadata.</returns>
    Task<ContentChunk> ProcessAsync(ContentChunk chunk, CancellationToken cancellationToken = default);
}
