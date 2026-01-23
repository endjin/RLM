// <copyright file="StdinDocumentReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Reads document content from standard input.
/// Accepts stdin:// URIs or "-" via extension methods.
/// </summary>
public sealed class StdinDocumentReader : IDocumentReader
{
    public bool CanRead(Uri source) => source.Scheme == "stdin";

    public async Task<RlmDocument?> ReadAsync(Uri source, CancellationToken cancellationToken = default)
    {
        if (source.Scheme != "stdin")
        {
            return null;
        }

        string content = await Console.In.ReadToEndAsync(cancellationToken);
        string[] lines = content.Split('\n');

        return new()
        {
            Id = "stdin",
            Content = content,
            Metadata = new()
            {
                Source = "stdin://input",
                TotalLength = content.Length,
                TokenEstimate = content.Length / 4,
                LineCount = lines.Length,
                ContentType = "text/plain"
            }
        };
    }

    public async IAsyncEnumerable<RlmDocument> ReadManyAsync(
        Uri source,
        string? pattern = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Stdin only supports single document reading
        RlmDocument? doc = await ReadAsync(source, cancellationToken);
        if (doc is not null)
        {
            yield return doc;
        }
    }
}
