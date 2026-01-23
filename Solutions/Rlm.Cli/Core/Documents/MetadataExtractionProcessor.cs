// <copyright file="MetadataExtractionProcessor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Extracts metadata from document content including title from first H1 header,
/// YAML frontmatter parsing, code block detection with language hints, and reading time estimation.
/// </summary>
public sealed partial class MetadataExtractionProcessor : IDocumentProcessor
{
    public Task<RlmDocument> ProcessAsync(RlmDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string content = document.Content;
        Dictionary<string, string> extractedMetadata = [];

        // Extract title from first H1 header
        string? title = null;
        Match h1Match = H1HeaderRegex().Match(content);
        if (h1Match.Success)
        {
            title = h1Match.Groups[1].Value.Trim();
            extractedMetadata["title"] = title;
        }

        // Extract YAML frontmatter if present
        Match frontmatterMatch = YamlFrontmatterRegex().Match(content);
        if (frontmatterMatch.Success)
        {
            string yaml = frontmatterMatch.Groups[1].Value;
            ParseSimpleYaml(yaml, extractedMetadata);

            // Override title from frontmatter if present
            if (extractedMetadata.TryGetValue("title", out string? frontmatterTitle))
            {
                title = frontmatterTitle;
            }

            // Remove frontmatter from content
            content = content[frontmatterMatch.Length..].TrimStart();
        }

        // Count words
        int wordCount = WordRegex().Matches(content).Count;
        extractedMetadata["wordCount"] = wordCount.ToString();

        // Count headers
        int headerCount = HeaderRegex().Matches(content).Count;
        extractedMetadata["headerCount"] = headerCount.ToString();

        // Count and extract code blocks with language detection
        MatchCollection codeBlocks = CodeBlockRegex().Matches(content);
        int codeBlockCount = codeBlocks.Count;
        extractedMetadata["codeBlockCount"] = codeBlockCount.ToString();

        List<string> codeLanguages = codeBlocks
            .Select(m => m.Groups[1].Value.Trim())
            .Where(lang => !string.IsNullOrEmpty(lang))
            .Distinct()
            .ToList();

        if (codeLanguages.Count > 0)
        {
            extractedMetadata["codeLanguages"] = string.Join(",", codeLanguages);
        }

        // Calculate estimated reading time (200 words per minute)
        int readingTimeMinutes = Math.Max(1, wordCount / 200);
        extractedMetadata["estimatedReadingTimeMinutes"] = readingTimeMinutes.ToString();

        // Detect document type based on content patterns
        string? detectedType = DetectDocumentType(content);
        if (detectedType is not null)
        {
            extractedMetadata["detectedType"] = detectedType;
        }

        // Build new metadata combining existing and extracted
        DocumentMetadata newMetadata = document.Metadata with
        {
            TotalLength = content.Length,
            TokenEstimate = content.Length / 4,
            LineCount = content.Split('\n').Length,
            Title = title ?? document.Metadata.Title,
            WordCount = wordCount,
            HeaderCount = headerCount,
            CodeBlockCount = codeBlockCount > 0 ? codeBlockCount : null,
            CodeLanguages = codeLanguages.Count > 0 ? codeLanguages : null,
            EstimatedReadingTimeMinutes = readingTimeMinutes,
            ExtendedMetadata = extractedMetadata.Count > 0 ? extractedMetadata : null
        };

        // Create new document with extracted metadata
        RlmDocument processed = new()
        {
            Id = title ?? document.Id,
            Content = content,
            Metadata = newMetadata
        };

        return Task.FromResult(processed);
    }

    private static void ParseSimpleYaml(string yaml, Dictionary<string, string> metadata)
    {
        // Simple YAML parsing for key: value pairs
        var pairs = yaml.Split('\n')
            .Select(line => line.Trim())
            .Where(trimmed => !string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith('#'))
            .Select(trimmed =>
            {
                int colonIndex = trimmed.IndexOf(':');
                return colonIndex > 0
                    ? (key: trimmed[..colonIndex].Trim(), value: trimmed[(colonIndex + 1)..].Trim().Trim('"', '\''))
                    : (key: string.Empty, value: string.Empty);
            })
            .Where(pair => !string.IsNullOrEmpty(pair.key) && !string.IsNullOrEmpty(pair.value));

        foreach (var (key, value) in pairs)
        {
            metadata[key] = value;
        }
    }

    private static string? DetectDocumentType(string content)
    {
        // Detect API documentation
        if (ApiPatternRegex().IsMatch(content))
        {
            return "api-documentation";
        }

        // Detect configuration/settings documentation
        if (ConfigPatternRegex().IsMatch(content))
        {
            return "configuration";
        }

        // Detect tutorial/how-to content
        if (TutorialPatternRegex().IsMatch(content))
        {
            return "tutorial";
        }

        // Detect changelog/release notes
        if (ChangelogPatternRegex().IsMatch(content))
        {
            return "changelog";
        }

        // Detect technical specification
        if (SpecPatternRegex().IsMatch(content))
        {
            return "specification";
        }

        return null;
    }

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex H1HeaderRegex();

    [GeneratedRegex(@"^---\s*\n([\s\S]*?)\n---\s*\n", RegexOptions.Compiled)]
    private static partial Regex YamlFrontmatterRegex();

    [GeneratedRegex(@"\b\w+\b", RegexOptions.Compiled)]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"^#{1,6}\s+", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"```(\w*)\s*\n[\s\S]*?```", RegexOptions.Compiled)]
    private static partial Regex CodeBlockRegex();

    // Document type detection patterns
    [GeneratedRegex(@"\b(endpoint|api|request|response|http|get|post|put|delete)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ApiPatternRegex();

    [GeneratedRegex(@"\b(config|setting|option|parameter|environment|variable)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ConfigPatternRegex();

    [GeneratedRegex(@"\b(step\s+\d|tutorial|how\s+to|guide|walkthrough)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TutorialPatternRegex();

    [GeneratedRegex(@"\b(changelog|release\s+notes|version\s+\d|breaking\s+change)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ChangelogPatternRegex();

    [GeneratedRegex(@"\b(specification|spec|requirement|must|shall|should)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SpecPatternRegex();
}
