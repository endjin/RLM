// <copyright file="FileDocumentReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Spectre.IO;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Reads documents from the file system using Spectre.IO for testability.
/// Supports directory traversal with glob patterns via ReadManyAsync.
/// </summary>
public sealed class FileDocumentReader(IFileSystem fileSystem) : IDocumentReader
{
    public bool CanRead(Uri source)
    {
        if (source.Scheme != "file")
        {
            return false;
        }

        string path = source.LocalPath;

        // Check if it's a file or directory
        FilePath filePath = new(path);
        DirectoryPath dirPath = new(path);

        return fileSystem.File.Exists(filePath) || fileSystem.Directory.Exists(dirPath);
    }

    public async Task<RlmDocument?> ReadAsync(
        Uri source,
        CancellationToken cancellationToken = default)
    {
        if (source.Scheme != "file")
        {
            return null;
        }

        string path = source.LocalPath;
        FilePath filePath = new(path);

        if (!fileSystem.File.Exists(filePath))
        {
            return null;
        }

        // Read content using Spectre.IO
        IFile file = fileSystem.GetFile(filePath);
        string content = await file.ReadAllTextAsync();
        string[] lines = content.Split('\n');

        return new()
        {
            Id = filePath.GetFilename().ToString(),
            Content = content,
            Metadata = new()
            {
                Source = source.ToString(),
                TotalLength = content.Length,
                TokenEstimate = content.Length / 4,
                LineCount = lines.Length,
                ContentType = GetContentType(path)
            }
        };
    }

    public async IAsyncEnumerable<RlmDocument> ReadManyAsync(
        Uri source,
        string? pattern = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (source.Scheme != "file")
        {
            yield break;
        }

        string path = source.LocalPath;

        // Check if source is a directory
        DirectoryPath dirPath = new(path);
        if (!fileSystem.Directory.Exists(dirPath))
        {
            // If it's a single file, just read it
            FilePath filePath = new(path);
            if (fileSystem.File.Exists(filePath))
            {
                RlmDocument? doc = await ReadAsync(source, cancellationToken);
                if (doc is not null)
                {
                    yield return doc;
                }
            }
            yield break;
        }

        // Use glob pattern matching for directory traversal
        IEnumerable<string> files = GetMatchingFiles(path, pattern ?? "**/*");

        foreach (string filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Uri fileUri = new($"file://{filePath}");
            RlmDocument? doc = await ReadAsync(fileUri, cancellationToken);
            if (doc is not null)
            {
                yield return doc;
            }
        }
    }

    private static IEnumerable<string> GetMatchingFiles(string directory, string pattern)
    {
        Matcher matcher = new();
        matcher.AddInclude(pattern);

        DirectoryInfoWrapper directoryInfo = new(new DirectoryInfo(directory));
        PatternMatchingResult result = matcher.Execute(directoryInfo);

        return result.Files.Select(f => System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, f.Path)));
    }

    private static string? GetContentType(string path)
    {
        FilePath filePath = new(path);
        string? extension = filePath.GetExtension()?.ToLowerInvariant();
        return extension switch
        {
            ".md" or ".markdown" => "text/markdown",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" or ".htm" => "text/html",
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".csv" => "text/csv",
            ".yaml" or ".yml" => "text/yaml",
            _ => null
        };
    }
}
