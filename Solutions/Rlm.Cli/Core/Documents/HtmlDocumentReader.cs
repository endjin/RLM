// <copyright file="HtmlDocumentReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ReverseMarkdown;
using Spectre.IO;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Reads HTML documents and converts them to Markdown for consistent processing.
/// Uses ReverseMarkdown to preserve semantic structure (headers, lists, code blocks).
/// </summary>
public sealed partial class HtmlDocumentReader(IFileSystem fileSystem) : IDocumentReader
{
    private readonly Converter _converter = new(new Config
    {
        UnknownTags = Config.UnknownTagsOption.Bypass,
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true
    });

    public bool CanRead(Uri source)
    {
        if (source.Scheme != "file")
        {
            return false;
        }

        FilePath filePath = new(source.LocalPath);
        string? extension = filePath.GetExtension()?.ToLowerInvariant();
        return extension is ".html" or ".htm";
    }

    public async Task<RlmDocument?> ReadAsync(Uri source, CancellationToken cancellationToken = default)
    {
        if (!CanRead(source))
        {
            return null;
        }

        string path = source.LocalPath;
        FilePath filePath = new(path);
        if (!fileSystem.File.Exists(filePath))
        {
            return null;
        }

        IFile file = fileSystem.GetFile(filePath);
        string html = await file.ReadAllTextAsync();
        return ConvertHtmlToDocument(source, html);
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

    private RlmDocument ConvertHtmlToDocument(Uri source, string html)
    {
        string path = source.LocalPath;

        // Convert HTML to Markdown - preserves structure (headers, lists, code, links)
        string markdown = _converter.Convert(html);

        // Extract title from <title> or first <h1>
        string? title = ExtractTitle(html);

        string[] lines = markdown.Split('\n');

        // Count words in the converted markdown
        int wordCount = WordRegex().Matches(markdown).Count;

        // Count headers in converted markdown
        int headerCount = HeaderRegex().Matches(markdown).Count;

        // Calculate reading time (200 words per minute)
        int readingTime = Math.Max(1, wordCount / 200);

        FilePath filePath = new(path);
        return new RlmDocument
        {
            Id = title ?? filePath.GetFilename().ToString(),
            Content = markdown,
            Metadata = new DocumentMetadata
            {
                Source = source.ToString(),
                TotalLength = markdown.Length,
                TokenEstimate = markdown.Length / 4,
                LineCount = lines.Length,
                ContentType = "text/markdown",  // Output is Markdown
                OriginalFormat = "text/html",
                Title = title,
                WordCount = wordCount,
                HeaderCount = headerCount,
                EstimatedReadingTimeMinutes = readingTime
            }
        };
    }

    private static string? ExtractTitle(string html)
    {
        // Try to extract from <title> tag
        Match titleMatch = TitleTagRegex().Match(html);
        if (titleMatch.Success)
        {
            return titleMatch.Groups[1].Value.Trim();
        }

        // Fallback to first <h1>
        Match h1Match = H1TagRegex().Match(html);
        return h1Match.Success ? h1Match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex(@"<title>([^<]+)</title>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TitleTagRegex();

    [GeneratedRegex(@"<h1[^>]*>([^<]+)</h1>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex H1TagRegex();

    [GeneratedRegex(@"\b\w+\b", RegexOptions.Compiled)]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"^#{1,6}\s+", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex HeaderRegex();
}
