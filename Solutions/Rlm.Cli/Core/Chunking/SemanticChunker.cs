// <copyright file="SemanticChunker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Core.Chunking;

/// <summary>
/// Splits documents on Markdown headers, preserving section hierarchy.
/// Use for document structure analysis and section-based comparison.
/// </summary>
/// <param name="minLevel">Minimum header level to split on (1-6).</param>
/// <param name="maxLevel">Maximum header level to split on (1-6).</param>
/// <param name="minSize">Minimum chunk size in characters. Smaller chunks will be merged.</param>
/// <param name="maxSize">Maximum chunk size in characters. Larger chunks will be split.</param>
/// <param name="mergeSmall">Whether to merge consecutive small sections.</param>
/// <param name="filterPattern">Optional regex pattern to filter sections.</param>
public sealed partial class SemanticChunker(
    int minLevel = 1,
    int maxLevel = 3,
    int minSize = 0,
    int maxSize = 0,
    bool mergeSmall = false,
    string? filterPattern = null) : IChunker
{
    public async IAsyncEnumerable<ContentChunk> ChunkAsync(
        RlmDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string headerPattern = $@"^(#{{{minLevel},{maxLevel}}})\s+(.+)$";
        Regex regex = new(headerPattern, RegexOptions.Multiline);
        MatchCollection matches = regex.Matches(document.Content);

        if (matches.Count == 0)
        {
            // No headers found, return entire document as single chunk
            yield return new()
            {
                Index = 0,
                Content = document.Content,
                StartPosition = 0,
                EndPosition = document.Content.Length,
                Metadata = new()
                {
                    ["documentId"] = document.Id,
                    ["strategy"] = "semantic"
                }
            };
            yield break;
        }

        List<Section> sections = ExtractSections(matches, document.Content);

        // Apply filter pattern if specified (hybrid mode)
        if (!string.IsNullOrEmpty(filterPattern))
        {
            Regex filter;
            try
            {
                filter = new(filterPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Invalid filter pattern '{filterPattern}': {ex.Message}", nameof(filterPattern), ex);
            }
            sections = sections.Where(s => filter.IsMatch(s.Content) || filter.IsMatch(s.Header)).ToList();
        }

        // Merge small sections if enabled
        if (mergeSmall && minSize > 0)
        {
            sections = MergeSmallSections(sections, minSize);
        }

        // Split large sections if max size is specified
        if (maxSize > 0)
        {
            sections = SplitLargeSections(sections, maxSize);
        }

        int index = 0;
        Stack<(int Level, string Header)> headerPath = new();

        foreach (Section section in sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Maintain header hierarchy for path
            while (headerPath.Count > 0 && headerPath.Peek().Level >= section.Level)
            {
                headerPath.Pop();
            }
            headerPath.Push((section.Level, section.Header));

            string path = string.Join(" > ", headerPath.Reverse().Select(h => h.Header));

            Dictionary<string, string> metadata = new()
            {
                ["documentId"] = document.Id,
                ["sectionHeader"] = section.Header,
                ["headerLevel"] = section.Level.ToString(),
                ["headerPath"] = path,
                ["strategy"] = "semantic"
            };

            // Add merged sections info if applicable
            if (section.MergedCount > 1)
            {
                metadata["mergedSections"] = section.MergedCount.ToString();
            }

            yield return new()
            {
                Index = index++,
                Content = section.Content,
                StartPosition = section.Start,
                EndPosition = section.End,
                Metadata = metadata
            };

            await Task.Yield();
        }
    }

    private static List<Section> ExtractSections(MatchCollection matches, string content)
    {
        List<Section> sections = [];

        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            Match? nextMatch = i + 1 < matches.Count ? matches[i + 1] : null;

            int start = match.Index;
            int end = nextMatch?.Index ?? content.Length;

            sections.Add(new()
            {
                Start = start,
                End = end,
                Content = content[start..end].Trim(),
                Header = match.Groups[2].Value.Trim(),
                Level = match.Groups[1].Value.Length,
                MergedCount = 1
            });
        }

        return sections;
    }

    /// <summary>
    /// Merges consecutive sections that are smaller than the minimum size.
    /// </summary>
    private static List<Section> MergeSmallSections(List<Section> sections, int minSize)
    {
        if (sections.Count == 0)
        {
            return sections;
        }

        List<Section> merged = [];
        Section? accumulator = null;

        foreach (Section section in sections)
        {
            if (accumulator is null)
            {
                accumulator = section;
                continue;
            }

            // If accumulated content is still below minSize, merge with current section
            if (accumulator.Content.Length < minSize)
            {
                accumulator = new Section
                {
                    Start = accumulator.Start,
                    End = section.End,
                    Content = accumulator.Content + "\n\n" + section.Content,
                    Header = accumulator.Header + " + " + section.Header,
                    Level = Math.Min(accumulator.Level, section.Level),
                    MergedCount = accumulator.MergedCount + 1
                };
            }
            else
            {
                // Accumulated content is large enough, emit it
                merged.Add(accumulator);
                accumulator = section;
            }
        }

        // Don't forget the last accumulator
        if (accumulator is not null)
        {
            merged.Add(accumulator);
        }

        return merged;
    }

    /// <summary>
    /// Splits sections that exceed the maximum size into smaller chunks.
    /// </summary>
    private static List<Section> SplitLargeSections(List<Section> sections, int maxSize)
    {
        List<Section> result = [];

        foreach (Section section in sections)
        {
            if (section.Content.Length <= maxSize)
            {
                result.Add(section);
                continue;
            }

            // Split the section content at paragraph boundaries
            string[] paragraphs = section.Content.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries);
            StringBuilder currentChunk = new();
            int currentStart = section.Start;
            int partNumber = 1;

            foreach (string paragraph in paragraphs)
            {
                if (currentChunk.Length + paragraph.Length + 2 > maxSize && currentChunk.Length > 0)
                {
                    // Emit current chunk
                    string content = currentChunk.ToString().Trim();
                    result.Add(new Section
                    {
                        Start = currentStart,
                        End = currentStart + content.Length,
                        Content = content,
                        Header = $"{section.Header} (Part {partNumber})",
                        Level = section.Level,
                        MergedCount = 1
                    });

                    currentStart += content.Length;
                    currentChunk.Clear();
                    partNumber++;
                }

                if (currentChunk.Length > 0)
                {
                    currentChunk.Append("\n\n");
                }
                currentChunk.Append(paragraph);
            }

            // Emit remaining content
            if (currentChunk.Length > 0)
            {
                string content = currentChunk.ToString().Trim();
                result.Add(new Section
                {
                    Start = currentStart,
                    End = section.End,
                    Content = content,
                    Header = partNumber > 1 ? $"{section.Header} (Part {partNumber})" : section.Header,
                    Level = section.Level,
                    MergedCount = 1
                });
            }
        }

        return result;
    }

    private sealed record Section
    {
        public required int Start { get; init; }
        public required int End { get; init; }
        public required string Content { get; init; }
        public required string Header { get; init; }
        public required int Level { get; init; }
        public int MergedCount { get; init; } = 1;
    }
}