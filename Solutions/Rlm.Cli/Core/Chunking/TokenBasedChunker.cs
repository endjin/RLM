// <copyright file="TokenBasedChunker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Microsoft.ML.Tokenizers;
using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Core.Chunking;

/// <summary>
/// Splits documents into chunks based on token count using Microsoft.ML.Tokenizers.
/// Use for model context limit compliance with accurate token counting.
/// </summary>
public sealed class TokenBasedChunker : IChunker
{
    private readonly int _maxTokens;
    private readonly int _overlapTokens;
    private readonly Tokenizer _tokenizer;

    /// <summary>
    /// Creates a new TokenBasedChunker with the specified token limits.
    /// </summary>
    /// <param name="maxTokens">Maximum tokens per chunk (default 512).</param>
    /// <param name="overlapTokens">Number of overlap tokens between chunks (default 50).</param>
    /// <param name="tokenizer">Optional tokenizer instance. Uses TiktokenTokenizer for cl100k_base by default.</param>
    public TokenBasedChunker(int maxTokens = 512, int overlapTokens = 50, Tokenizer? tokenizer = null)
    {
        _maxTokens = maxTokens;
        _overlapTokens = overlapTokens;
        _tokenizer = tokenizer ?? TiktokenTokenizer.CreateForModel("gpt-4");
    }

    public async IAsyncEnumerable<ContentChunk> ChunkAsync(
        RlmDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string content = document.Content;

        if (string.IsNullOrEmpty(content))
        {
            yield break;
        }

        IReadOnlyList<int> allTokenIds = _tokenizer.EncodeToIds(content);
        int totalTokens = allTokenIds.Count;

        if (totalTokens <= _maxTokens)
        {
            yield return new ContentChunk
            {
                Index = 0,
                Content = content,
                StartPosition = 0,
                EndPosition = content.Length,
                Metadata = new Dictionary<string, string>
                {
                    ["documentId"] = document.Id,
                    ["totalChunks"] = "1",
                    ["tokenCount"] = totalTokens.ToString(),
                    ["strategy"] = "token"
                }
            };
            yield break;
        }

        int index = 0;
        int tokenPosition = 0;
        int effectiveStep = _maxTokens - _overlapTokens;
        int totalChunks = CalculateTotalChunks(totalTokens);

        while (tokenPosition < totalTokens)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int endTokenPosition = Math.Min(tokenPosition + _maxTokens, totalTokens);
            int chunkTokenCount = endTokenPosition - tokenPosition;

            // Decode the token range back to text
            IReadOnlyList<int> chunkTokenIds = allTokenIds.Skip(tokenPosition).Take(chunkTokenCount).ToList();
            string chunkContent = _tokenizer.Decode(chunkTokenIds);

            // Calculate approximate character positions
            int startCharPosition = CalculateApproximateCharPosition(content, tokenPosition, allTokenIds);
            int endCharPosition = CalculateApproximateCharPosition(content, endTokenPosition, allTokenIds);

            yield return new ContentChunk
            {
                Index = index++,
                Content = chunkContent,
                StartPosition = startCharPosition,
                EndPosition = endCharPosition,
                Metadata = new Dictionary<string, string>
                {
                    ["documentId"] = document.Id,
                    ["totalChunks"] = totalChunks.ToString(),
                    ["tokenCount"] = chunkTokenCount.ToString(),
                    ["startToken"] = tokenPosition.ToString(),
                    ["endToken"] = endTokenPosition.ToString(),
                    ["strategy"] = "token"
                }
            };

            tokenPosition += effectiveStep;
            await Task.Yield();
        }
    }

    private int CalculateTotalChunks(int totalTokens)
    {
        if (totalTokens <= _maxTokens)
        {
            return 1;
        }

        int effectiveStep = _maxTokens - _overlapTokens;
        return (int)Math.Ceiling((double)(totalTokens - _overlapTokens) / effectiveStep);
    }

    private int CalculateApproximateCharPosition(string content, int tokenPosition, IReadOnlyList<int> allTokenIds)
    {
        if (tokenPosition == 0)
        {
            return 0;
        }

        if (tokenPosition >= allTokenIds.Count)
        {
            return content.Length;
        }

        // Decode tokens up to the position to get accurate character position
        IReadOnlyList<int> tokensUpToPosition = allTokenIds.Take(tokenPosition).ToList();
        string decodedUpToPosition = _tokenizer.Decode(tokensUpToPosition);
        return Math.Min(decodedUpToPosition.Length, content.Length);
    }
}
