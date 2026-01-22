// <copyright file="FilteringChunker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Core.Chunking;

/// <summary>
/// Filters content based on regex patterns, returning only matching segments with context.
/// Use for needle-in-haystack tasks where you're searching for specific information.
/// </summary>
public sealed partial class FilteringChunker(string pattern, int contextSize = 500) : IChunker
{
    public async IAsyncEnumerable<ContentChunk> ChunkAsync(
        RlmDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Regex regex;
        try
        {
            regex = new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"Invalid regex pattern '{pattern}': {ex.Message}", nameof(pattern), ex);
        }
        MatchCollection matches = regex.Matches(document.Content);

        if (matches.Count == 0)
        {
            yield break;
        }

        List<Segment> segments = MergeOverlappingSegments(matches, document.Content);

        int index = 0;
        foreach (Segment segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return new()
            {
                Index = index++,
                Content = segment.Text,
                StartPosition = segment.Start,
                EndPosition = segment.End,
                Metadata = new()
                {
                    ["documentId"] = document.Id,
                    ["matchedTerms"] = string.Join(", ", segment.MatchedTerms.Distinct()),
                    ["matchCount"] = segment.MatchCount.ToString(),
                    ["strategy"] = "filtering"
                }
            };

            await Task.Yield();
        }
    }

    private List<Segment> MergeOverlappingSegments(MatchCollection matches, string content)
    {
        List<(int Start, int End, List<string> Matches)> segments = [];

        foreach (Match match in matches)
        {
            int start = Math.Max(0, match.Index - contextSize);
            int end = Math.Min(content.Length, match.Index + match.Length + contextSize);

            segments.Add((start, end, [match.Value]));
        }

        // Sort by start position
        segments = [.. segments.OrderBy(s => s.Start)];

        // Merge overlapping segments
        List<Segment> merged = [];
        foreach ((int start, int end, List<string> matchList) in segments)
        {
            if (merged.Count == 0 || start > merged[^1].End)
            {
                merged.Add(new()
                {
                    Start = start,
                    End = end,
                    Text = content[start..end],
                    MatchedTerms = matchList,
                    MatchCount = matchList.Count
                });
            }
            else
            {
                // Extend the last segment
                Segment last = merged[^1];
                int newEnd = Math.Max(last.End, end);
                merged[^1] = last with
                {
                    End = newEnd,
                    Text = content[last.Start..newEnd],
                    MatchedTerms = [.. last.MatchedTerms, .. matchList],
                    MatchCount = last.MatchCount + matchList.Count
                };
            }
        }

        return merged;
    }

    private sealed record Segment
    {
        public required int Start { get; init; }
        public required int End { get; init; }
        public required string Text { get; init; }
        public required List<string> MatchedTerms { get; init; }
        public required int MatchCount { get; init; }
    }
}