// <copyright file="ContentCleaningProcessor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Cleans document content by removing HTML comments, normalizing whitespace,
/// and removing empty links.
/// </summary>
public sealed partial class ContentCleaningProcessor : IDocumentProcessor
{
    public Task<RlmDocument> ProcessAsync(RlmDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string content = document.Content;

        // Remove HTML comments
        content = HtmlCommentRegex().Replace(content, string.Empty);

        // Remove empty links [](url) or [text]()
        content = EmptyLinkRegex().Replace(content, string.Empty);

        // Normalize multiple blank lines to double newline
        content = MultipleBlankLinesRegex().Replace(content, "\n\n");

        // Normalize multiple spaces to single space (preserve newlines)
        content = MultipleSpacesRegex().Replace(content, " ");

        // Trim leading/trailing whitespace from each line
        content = string.Join('\n', content.Split('\n').Select(line => line.Trim()));

        // Trim overall content
        content = content.Trim();

        // Return new document with cleaned content
        RlmDocument cleaned = new()
        {
            Id = document.Id,
            Content = content,
            Metadata = document.Metadata with
            {
                TotalLength = content.Length,
                TokenEstimate = content.Length / 4,
                LineCount = content.Split('\n').Length
            }
        };

        return Task.FromResult(cleaned);
    }

    [GeneratedRegex(@"<!--[\s\S]*?-->", RegexOptions.Compiled)]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex(@"\[([^\]]*)\]\(\s*\)|\[\s*\]\([^\)]*\)", RegexOptions.Compiled)]
    private static partial Regex EmptyLinkRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex MultipleBlankLinesRegex();

    [GeneratedRegex(@"[ \t]{2,}", RegexOptions.Compiled)]
    private static partial Regex MultipleSpacesRegex();
}
