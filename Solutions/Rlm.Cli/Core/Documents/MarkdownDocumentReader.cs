// <copyright file="MarkdownDocumentReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Reads Markdown documents with YAML frontmatter extraction and structure detection.
/// Extracts metadata including title, headers, code blocks, and frontmatter fields.
/// </summary>
public sealed partial class MarkdownDocumentReader : IDocumentReader
{
    public bool CanRead(Uri source)
    {
        if (source.Scheme != "file")
        {
            return false;
        }

        string extension = Path.GetExtension(source.LocalPath).ToLowerInvariant();
        return extension is ".md" or ".markdown";
    }

    public async Task<RlmDocument?> ReadAsync(Uri source, CancellationToken cancellationToken = default)
    {
        if (!CanRead(source))
        {
            return null;
        }

        string path = source.LocalPath;
        if (!File.Exists(path))
        {
            return null;
        }

        string content = await File.ReadAllTextAsync(path, cancellationToken);
        return ParseMarkdown(source, content);
    }

    public async IAsyncEnumerable<RlmDocument> ReadManyAsync(
        Uri source,
        string? pattern = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        RlmDocument? doc = await ReadAsync(source, cancellationToken);
        if (doc is not null)
        {
            yield return doc;
        }
    }

    private RlmDocument ParseMarkdown(Uri source, string content)
    {
        string path = source.LocalPath;
        Dictionary<string, string> frontmatterData = [];
        string processedContent = content;

        // Extract YAML frontmatter if present
        Match frontmatterMatch = YamlFrontmatterRegex().Match(content);
        if (frontmatterMatch.Success)
        {
            string yaml = frontmatterMatch.Groups[1].Value;
            ParseSimpleYaml(yaml, frontmatterData);
            processedContent = content[frontmatterMatch.Length..].TrimStart();
        }

        // Extract title from first H1 header or frontmatter
        string? title = null;
        if (frontmatterData.TryGetValue("title", out string? frontmatterTitle))
        {
            title = frontmatterTitle;
        }
        else
        {
            Match h1Match = H1HeaderRegex().Match(processedContent);
            if (h1Match.Success)
            {
                title = h1Match.Groups[1].Value.Trim();
            }
        }

        // Count headers
        int headerCount = HeaderRegex().Matches(processedContent).Count;

        // Count and extract code blocks
        MatchCollection codeBlocks = CodeBlockRegex().Matches(processedContent);
        int codeBlockCount = codeBlocks.Count;

        // Extract programming languages from code blocks
        List<string> codeLanguages = codeBlocks
            .Select(m => m.Groups[1].Value.Trim())
            .Where(lang => !string.IsNullOrEmpty(lang))
            .Distinct()
            .ToList();

        // Count words
        int wordCount = WordRegex().Matches(processedContent).Count;

        // Calculate reading time (200 words per minute)
        int readingTime = Math.Max(1, wordCount / 200);

        string[] lines = processedContent.Split('\n');

        return new RlmDocument
        {
            Id = title ?? Path.GetFileName(path),
            Content = processedContent,
            Metadata = new DocumentMetadata
            {
                Source = source.ToString(),
                TotalLength = processedContent.Length,
                TokenEstimate = processedContent.Length / 4,
                LineCount = lines.Length,
                ContentType = "text/markdown",
                Title = title,
                WordCount = wordCount,
                HeaderCount = headerCount,
                CodeBlockCount = codeBlockCount,
                CodeLanguages = codeLanguages.Count > 0 ? codeLanguages : null,
                EstimatedReadingTimeMinutes = readingTime,
                ExtendedMetadata = frontmatterData.Count > 0 ? frontmatterData : null
            }
        };
    }

    private static void ParseSimpleYaml(string yaml, Dictionary<string, string> metadata)
    {
        foreach (string line in yaml.Split('\n'))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex > 0)
            {
                string key = trimmed[..colonIndex].Trim();
                string value = trimmed[(colonIndex + 1)..].Trim().Trim('"', '\'');

                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                {
                    metadata[key] = value;
                }
            }
        }
    }

    [GeneratedRegex(@"^---\s*\n([\s\S]*?)\n---\s*\n", RegexOptions.Compiled)]
    private static partial Regex YamlFrontmatterRegex();

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex H1HeaderRegex();

    [GeneratedRegex(@"^#{1,6}\s+", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"```(\w*)\s*\n[\s\S]*?```", RegexOptions.Compiled)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"\b\w+\b", RegexOptions.Compiled)]
    private static partial Regex WordRegex();
}
