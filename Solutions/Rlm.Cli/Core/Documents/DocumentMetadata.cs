// <copyright file="DocumentMetadata.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Metadata about a loaded document, following the Data Ingestion pattern.
/// Supports format-specific metadata for PDF, HTML, Word, JSON, and Markdown documents.
/// </summary>
public sealed record DocumentMetadata
{
    /// <summary>
    /// Source path or identifier for the document.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Total character count of the content.
    /// </summary>
    public required int TotalLength { get; init; }

    /// <summary>
    /// Estimated token count (roughly chars / 4).
    /// </summary>
    public required int TokenEstimate { get; init; }

    /// <summary>
    /// Number of lines in the content.
    /// </summary>
    public required int LineCount { get; init; }

    /// <summary>
    /// When the document was loaded into the session.
    /// </summary>
    public DateTimeOffset LoadedAt { get; init; } = DateTimeOffset.UtcNow;

    // Format-specific metadata

    /// <summary>
    /// MIME type of the content (e.g., "text/markdown", "application/pdf").
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Original format before conversion (e.g., "text/html" when HTML is converted to Markdown).
    /// </summary>
    public string? OriginalFormat { get; init; }

    /// <summary>
    /// Document title extracted from metadata or content.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Document author extracted from metadata.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Number of pages (for PDF documents).
    /// </summary>
    public int? PageCount { get; init; }

    /// <summary>
    /// Number of elements (for JSON documents - root element count).
    /// </summary>
    public int? ElementCount { get; init; }

    /// <summary>
    /// Word count extracted from content.
    /// </summary>
    public int? WordCount { get; init; }

    /// <summary>
    /// Number of headers in the document.
    /// </summary>
    public int? HeaderCount { get; init; }

    /// <summary>
    /// Number of code blocks in the document.
    /// </summary>
    public int? CodeBlockCount { get; init; }

    /// <summary>
    /// Detected programming languages in code blocks.
    /// </summary>
    public IReadOnlyList<string>? CodeLanguages { get; init; }

    /// <summary>
    /// Estimated reading time in minutes (based on 200 words/minute).
    /// </summary>
    public int? EstimatedReadingTimeMinutes { get; init; }

    /// <summary>
    /// Additional metadata extracted from frontmatter or document properties.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ExtendedMetadata { get; init; }
}
