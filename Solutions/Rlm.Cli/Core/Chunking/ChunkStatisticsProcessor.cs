// <copyright file="ChunkStatisticsProcessor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;

namespace Rlm.Cli.Core.Chunking;

/// <summary>
/// Enriches chunks with statistical metadata including word count, character count,
/// and line count.
/// </summary>
public sealed partial class ChunkStatisticsProcessor : IChunkProcessor
{
    public Task<ContentChunk> ProcessAsync(ContentChunk chunk, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Calculate statistics
        int wordCount = WordRegex().Matches(chunk.Content).Count;
        int lineCount = chunk.Content.Split('\n').Length;
        int charCount = chunk.Content.Length;
        int charCountNoWhitespace = chunk.Content.Count(c => !char.IsWhiteSpace(c));

        // Enrich metadata
        Dictionary<string, string> enrichedMetadata = new(chunk.Metadata)
        {
            ["wordCount"] = wordCount.ToString(),
            ["lineCount"] = lineCount.ToString(),
            ["charCount"] = charCount.ToString(),
            ["charCountNoWhitespace"] = charCountNoWhitespace.ToString()
        };

        return Task.FromResult(chunk with { Metadata = enrichedMetadata });
    }

    [GeneratedRegex(@"\b\w+\b", RegexOptions.Compiled)]
    private static partial Regex WordRegex();
}
