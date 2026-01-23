// <copyright file="IDocumentProcessor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Processes documents before chunking, enabling content transformation and metadata enrichment.
/// </summary>
public interface IDocumentProcessor
{
    /// <summary>
    /// Processes a document and returns the transformed result.
    /// </summary>
    /// <param name="document">The document to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The processed document (may be the same instance or a new one).</returns>
    Task<RlmDocument> ProcessAsync(RlmDocument document, CancellationToken cancellationToken = default);
}
