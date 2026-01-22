// <copyright file="RecursiveChunker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Core.Chunking;

/// <summary>
/// Splits documents using a hierarchy of separators, recursively subdividing
/// oversized chunks until they fit within the maximum size.
/// </summary>
public sealed partial class RecursiveChunker : IChunker
{
    private readonly int _maxChunkSize;
    private readonly int _minChunkSize;

    /// <summary>
    /// Separators in order of preference, from coarsest to finest.
    /// </summary>
    private static readonly string[] Separators =
    [
        "\n## ",      // Level 2 headers
        "\n### ",     // Level 3 headers
        "\n#### ",    // Level 4 headers
        "\n\n",       // Double newline (paragraphs)
        "\n",         // Single newline
        ". ",         // Sentences
        " "           // Words
    ];

    /// <summary>
    /// Creates a new RecursiveChunker with the specified size limits.
    /// </summary>
    /// <param name="maxChunkSize">Maximum chunk size in characters (default: 50000).</param>
    /// <param name="minChunkSize">Minimum chunk size before giving up subdivision (default: 100).</param>
    public RecursiveChunker(int maxChunkSize = 50000, int minChunkSize = 100)
    {
        _maxChunkSize = maxChunkSize;
        _minChunkSize = minChunkSize;
    }

    public async IAsyncEnumerable<ContentChunk> ChunkAsync(
        RlmDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string content = document.Content;

        if (string.IsNullOrEmpty(content))
        {
            yield break;
        }

        // If content fits, return as single chunk
        if (content.Length <= _maxChunkSize)
        {
            yield return CreateChunk(document.Id, content, 0, 0, content.Length, 1);
            yield break;
        }

        List<ChunkSegment> segments = [];
        SplitRecursively(content, 0, segments, 0);

        int index = 0;
        int totalChunks = segments.Count;

        foreach (ChunkSegment segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return CreateChunk(
                document.Id,
                segment.Content,
                index,
                segment.StartPosition,
                segment.EndPosition,
                totalChunks,
                segment.SeparatorUsed);

            index++;
            await Task.Yield();
        }
    }

    private void SplitRecursively(string content, int basePosition, List<ChunkSegment> segments, int separatorIndex)
    {
        // If content fits, add it as a segment
        if (content.Length <= _maxChunkSize)
        {
            segments.Add(new ChunkSegment(content, basePosition, basePosition + content.Length, GetSeparatorName(separatorIndex)));
            return;
        }

        // If we've run out of separators or chunk is too small to split
        if (separatorIndex >= Separators.Length || content.Length <= _minChunkSize)
        {
            // Force split at max size
            for (int i = 0; i < content.Length; i += _maxChunkSize)
            {
                int end = Math.Min(i + _maxChunkSize, content.Length);
                string chunk = content[i..end];
                segments.Add(new ChunkSegment(chunk, basePosition + i, basePosition + end, "forced"));
            }
            return;
        }

        // Try to split using current separator
        string separator = Separators[separatorIndex];
        string[] parts = SplitKeepingSeparator(content, separator);

        if (parts.Length <= 1)
        {
            // Separator not found, try next one
            SplitRecursively(content, basePosition, segments, separatorIndex + 1);
            return;
        }

        // Process each part
        int currentPosition = basePosition;
        foreach (string part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            if (part.Length <= _maxChunkSize)
            {
                segments.Add(new ChunkSegment(part, currentPosition, currentPosition + part.Length, GetSeparatorName(separatorIndex)));
            }
            else
            {
                // Part is still too large, recurse with next separator
                SplitRecursively(part, currentPosition, segments, separatorIndex + 1);
            }

            currentPosition += part.Length;
        }
    }

    private static string[] SplitKeepingSeparator(string content, string separator)
    {
        // Split but keep the separator at the beginning of each part (except first)
        List<string> result = [];
        int lastIndex = 0;

        int index = content.IndexOf(separator, StringComparison.Ordinal);
        while (index != -1)
        {
            if (index > lastIndex)
            {
                result.Add(content[lastIndex..index]);
            }
            lastIndex = index;
            index = content.IndexOf(separator, lastIndex + separator.Length, StringComparison.Ordinal);
        }

        // Add remaining content
        if (lastIndex < content.Length)
        {
            result.Add(content[lastIndex..]);
        }

        return [.. result];
    }

    private static string GetSeparatorName(int index) => index switch
    {
        0 => "h2",
        1 => "h3",
        2 => "h4",
        3 => "paragraph",
        4 => "line",
        5 => "sentence",
        6 => "word",
        _ => "unknown"
    };

    private static ContentChunk CreateChunk(
        string documentId,
        string content,
        int index,
        int startPosition,
        int endPosition,
        int totalChunks,
        string? separatorUsed = null)
    {
        return new ContentChunk
        {
            Index = index,
            Content = content,
            StartPosition = startPosition,
            EndPosition = endPosition,
            Metadata = new Dictionary<string, string>
            {
                ["documentId"] = documentId,
                ["totalChunks"] = totalChunks.ToString(),
                ["strategy"] = "recursive",
                ["separatorUsed"] = separatorUsed ?? "none"
            }
        };
    }

    private sealed record ChunkSegment(string Content, int StartPosition, int EndPosition, string SeparatorUsed);
}
