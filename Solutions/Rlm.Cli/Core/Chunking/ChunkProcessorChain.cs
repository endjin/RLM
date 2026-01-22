// <copyright file="ChunkProcessorChain.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Rlm.Cli.Core.Chunking;

/// <summary>
/// Chains multiple chunk processors for sequential chunk enrichment.
/// </summary>
public sealed class ChunkProcessorChain : IChunkProcessor
{
    private readonly IReadOnlyList<IChunkProcessor> _processors;

    /// <summary>
    /// Creates a new processor chain with the specified processors.
    /// </summary>
    public ChunkProcessorChain(params IChunkProcessor[] processors)
    {
        _processors = processors;
    }

    /// <summary>
    /// Creates a new processor chain with the specified processors.
    /// </summary>
    public ChunkProcessorChain(IEnumerable<IChunkProcessor> processors)
    {
        _processors = processors.ToList();
    }

    /// <summary>
    /// Number of processors in the chain.
    /// </summary>
    public int Count => _processors.Count;

    public async Task<ContentChunk> ProcessAsync(ContentChunk chunk, CancellationToken cancellationToken = default)
    {
        ContentChunk result = chunk;

        foreach (IChunkProcessor processor in _processors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = await processor.ProcessAsync(result, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Creates a default processor chain with statistics enrichment.
    /// </summary>
    public static ChunkProcessorChain CreateDefault() => new(
        new ChunkStatisticsProcessor()
    );
}
