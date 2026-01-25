// <copyright file="JsonDocumentReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.Json;
using Spectre.IO;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Reads JSON documents and formats them for readability.
/// Extracts element count and pretty-prints the JSON content.
/// </summary>
public sealed class JsonDocumentReader(IFileSystem fileSystem) : IDocumentReader
{
    private static readonly JsonSerializerOptions PrettyPrintOptions = new()
    {
        WriteIndented = true
    };

    public bool CanRead(Uri source)
    {
        if (source.Scheme != "file")
        {
            return false;
        }

        FilePath filePath = new(source.LocalPath);
        string? extension = filePath.GetExtension()?.ToLowerInvariant();
        return extension == ".json";
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
        string json = await file.ReadAllTextAsync();
        return ParseJson(source, json);
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

    private RlmDocument? ParseJson(Uri source, string json)
    {
        string path = source.LocalPath;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);

            // Pretty-print for readability in RLM context
            string formatted = JsonSerializer.Serialize(doc, PrettyPrintOptions);
            string[] lines = formatted.Split('\n');

            // Extract element count
            int elementCount = CountElements(doc.RootElement);

            FilePath filePath = new(path);
            return new RlmDocument
            {
                Id = filePath.GetFilename().ToString(),
                Content = formatted,
                Metadata = new DocumentMetadata
                {
                    Source = source.ToString(),
                    TotalLength = formatted.Length,
                    TokenEstimate = formatted.Length / 4,
                    LineCount = lines.Length,
                    ContentType = "application/json",
                    ElementCount = elementCount
                }
            };
        }
        catch (JsonException)
        {
            // Invalid JSON - return null to allow fallback
            return null;
        }
    }

    private static int CountElements(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Count(),
        JsonValueKind.Array => element.GetArrayLength(),
        _ => 1
    };
}
