// <copyright file="RlmDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Represents a document in the RLM pipeline, analogous to IngestionDocument
/// from Data Ingestion Building Blocks.
/// </summary>
public sealed class RlmDocument
{
    /// <summary>
    /// Unique identifier for the document.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Raw content of the document.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Document metadata including source, length, and token estimates.
    /// </summary>
    public required DocumentMetadata Metadata { get; init; }
}