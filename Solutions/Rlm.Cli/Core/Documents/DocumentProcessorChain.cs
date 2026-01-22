// <copyright file="DocumentProcessorChain.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Chains multiple document processors together, executing them in sequence.
/// </summary>
public sealed class DocumentProcessorChain : IDocumentProcessor
{
    private readonly IReadOnlyList<IDocumentProcessor> _processors;

    /// <summary>
    /// Creates a processor chain with the specified processors.
    /// </summary>
    /// <param name="processors">Processors to execute in order.</param>
    public DocumentProcessorChain(IEnumerable<IDocumentProcessor> processors)
    {
        _processors = processors.ToList();
    }

    /// <summary>
    /// Creates a processor chain with the specified processors.
    /// </summary>
    /// <param name="processors">Processors to execute in order.</param>
    public DocumentProcessorChain(params IDocumentProcessor[] processors)
    {
        _processors = processors;
    }

    /// <summary>
    /// Gets the number of processors in the chain.
    /// </summary>
    public int Count => _processors.Count;

    public async Task<RlmDocument> ProcessAsync(RlmDocument document, CancellationToken cancellationToken = default)
    {
        RlmDocument result = document;

        foreach (IDocumentProcessor processor in _processors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = await processor.ProcessAsync(result, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Creates a default processor chain with content cleaning and metadata extraction.
    /// </summary>
    public static DocumentProcessorChain CreateDefault() => new(
        new ContentCleaningProcessor(),
        new MetadataExtractionProcessor()
    );
}
