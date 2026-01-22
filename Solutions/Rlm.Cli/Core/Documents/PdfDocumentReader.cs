// <copyright file="PdfDocumentReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Reads PDF documents using PdfPig library.
/// Extracts text content and PDF-specific metadata (pages, title, author).
/// </summary>
public sealed partial class PdfDocumentReader : IDocumentReader
{
    public bool CanRead(Uri source)
    {
        if (source.Scheme != "file")
        {
            return false;
        }

        string extension = Path.GetExtension(source.LocalPath).ToLowerInvariant();
        return extension == ".pdf";
    }

    public Task<RlmDocument?> ReadAsync(Uri source, CancellationToken cancellationToken = default)
    {
        if (!CanRead(source))
        {
            return Task.FromResult<RlmDocument?>(null);
        }

        string path = source.LocalPath;
        if (!File.Exists(path))
        {
            return Task.FromResult<RlmDocument?>(null);
        }

        return Task.FromResult<RlmDocument?>(ReadPdf(source, path));
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

    private RlmDocument? ReadPdf(Uri source, string path)
    {
        try
        {
            using PdfDocument document = PdfDocument.Open(path);
            StringBuilder content = new();

            foreach (Page page in document.GetPages())
            {
                content.AppendLine(page.Text);
                content.AppendLine();
            }

            string text = content.ToString();
            string[] lines = text.Split('\n');

            // Count words
            int wordCount = WordRegex().Matches(text).Count;

            // Calculate reading time (200 words per minute)
            int readingTime = Math.Max(1, wordCount / 200);

            return new RlmDocument
            {
                Id = Path.GetFileName(path),
                Content = text,
                Metadata = new DocumentMetadata
                {
                    Source = source.ToString(),
                    TotalLength = text.Length,
                    TokenEstimate = text.Length / 4,
                    LineCount = lines.Length,
                    ContentType = "application/pdf",
                    Title = document.Information?.Title,
                    Author = document.Information?.Author,
                    PageCount = document.NumberOfPages,
                    WordCount = wordCount,
                    EstimatedReadingTimeMinutes = readingTime
                }
            };
        }
        catch (Exception)
        {
            // PDF parsing failed - return null to allow fallback to other readers
            return null;
        }
    }

    [GeneratedRegex(@"\b\w+\b", RegexOptions.Compiled)]
    private static partial Regex WordRegex();
}
