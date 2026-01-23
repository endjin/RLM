// <copyright file="JsonDocumentReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Reads JSON documents and formats them for readability.
/// Extracts element count and pretty-prints the JSON content.
/// </summary>
public sealed class JsonDocumentReader : IDocumentReader
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

        string extension = Path.GetExtension(source.LocalPath).ToLowerInvariant();
        return extension == ".json";
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

        string json = await File.ReadAllTextAsync(path, cancellationToken);
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

            return new RlmDocument
            {
                Id = Path.GetFileName(path),
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
