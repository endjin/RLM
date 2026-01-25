// <copyright file="WordDocumentReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Spectre.IO;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Reads Word (.docx) documents using DocumentFormat.OpenXml.
/// Extracts paragraph text and document metadata (title, author).
/// </summary>
public sealed partial class WordDocumentReader(IFileSystem fileSystem) : IDocumentReader
{
    public bool CanRead(Uri source)
    {
        if (source.Scheme != "file")
        {
            return false;
        }

        FilePath filePath = new(source.LocalPath);
        string? extension = filePath.GetExtension()?.ToLowerInvariant();
        return extension == ".docx";
    }

    public Task<RlmDocument?> ReadAsync(Uri source, CancellationToken cancellationToken = default)
    {
        if (!CanRead(source))
        {
            return Task.FromResult<RlmDocument?>(null);
        }

        string path = source.LocalPath;
        FilePath filePath = new(path);
        if (!fileSystem.File.Exists(filePath))
        {
            return Task.FromResult<RlmDocument?>(null);
        }

        return Task.FromResult<RlmDocument?>(ReadWord(source, path));
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

    private RlmDocument? ReadWord(Uri source, string path)
    {
        try
        {
            using WordprocessingDocument doc = WordprocessingDocument.Open(path, false);
            Body? body = doc.MainDocumentPart?.Document?.Body;

            if (body is null)
            {
                return null;
            }

            StringBuilder content = new();
            foreach (Paragraph para in body.Elements<Paragraph>())
            {
                content.AppendLine(para.InnerText);
            }

            string text = content.ToString();
            string[] lines = text.Split('\n');

            // Count words
            int wordCount = WordRegex().Matches(text).Count;

            // Calculate reading time (200 words per minute)
            int readingTime = Math.Max(1, wordCount / 200);

            // Extract document properties
            string? title = doc.PackageProperties.Title;
            string? author = doc.PackageProperties.Creator;

            FilePath filePath = new(path);
            return new RlmDocument
            {
                Id = title ?? filePath.GetFilename().ToString(),
                Content = text,
                Metadata = new DocumentMetadata
                {
                    Source = source.ToString(),
                    TotalLength = text.Length,
                    TokenEstimate = text.Length / 4,
                    LineCount = lines.Length,
                    ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    Title = title,
                    Author = author,
                    WordCount = wordCount,
                    EstimatedReadingTimeMinutes = readingTime
                }
            };
        }
        catch (Exception)
        {
            // Word document parsing failed - return null to allow fallback
            return null;
        }
    }

    [GeneratedRegex(@"\b\w+\b", RegexOptions.Compiled)]
    private static partial Regex WordRegex();
}
