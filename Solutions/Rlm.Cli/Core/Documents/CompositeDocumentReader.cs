// <copyright file="CompositeDocumentReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Combines multiple document readers, delegating to the first one that can handle the source.
/// Supports Uri-based sources and batch reading via ReadManyAsync.
/// </summary>
public sealed class CompositeDocumentReader(params IDocumentReader[] readers) : IDocumentReader
{
    public bool CanRead(Uri source) => readers.Any(r => r.CanRead(source));

    public async Task<RlmDocument?> ReadAsync(
        Uri source,
        CancellationToken cancellationToken = default)
    {
        IDocumentReader? reader = readers.FirstOrDefault(r => r.CanRead(source));
        return reader is not null
            ? await reader.ReadAsync(source, cancellationToken)
            : null;
    }

    public async IAsyncEnumerable<RlmDocument> ReadManyAsync(
        Uri source,
        string? pattern = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IDocumentReader? reader = readers.FirstOrDefault(r => r.CanRead(source));
        if (reader is null)
        {
            yield break;
        }

        await foreach (RlmDocument doc in reader.ReadManyAsync(source, pattern, cancellationToken))
        {
            yield return doc;
        }
    }
}
