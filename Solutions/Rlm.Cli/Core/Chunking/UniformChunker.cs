// <copyright file="UniformChunker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Core.Chunking;

/// <summary>
/// Splits documents into fixed-size chunks with optional overlap.
/// Use for aggregation/summary tasks where all content is potentially relevant.
/// </summary>
public sealed class UniformChunker(int chunkSize, int overlap = 0) : IChunker
{
    public async IAsyncEnumerable<ContentChunk> ChunkAsync(
        RlmDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string content = document.Content;
        int position = 0;
        int index = 0;
        int totalChunks = CalculateTotalChunks(content.Length);

        while (position < content.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int end = Math.Min(position + chunkSize, content.Length);
            string chunkContent = content[position..end];

            yield return new()
            {
                Index = index++,
                Content = chunkContent,
                StartPosition = position,
                EndPosition = end,
                Metadata = new()
                {
                    ["documentId"] = document.Id,
                    ["totalChunks"] = totalChunks.ToString(),
                    ["strategy"] = "uniform"
                }
            };

            position += chunkSize - overlap;
            await Task.Yield(); // Allow for cancellation between chunks
        }
    }

    private int CalculateTotalChunks(int contentLength)
    {
        if (contentLength <= chunkSize)
        {
            return 1;
        }

        int effectiveStep = chunkSize - overlap;
        return (int)Math.Ceiling((double)(contentLength - overlap) / effectiveStep);
    }
}