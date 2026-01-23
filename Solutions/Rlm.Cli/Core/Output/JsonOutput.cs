// <copyright file="JsonOutput.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Rlm.Cli.Core.Output;

/// <summary>
/// JSON output model for session info.
/// </summary>
public sealed record SessionInfoOutput
{
    public required string Source { get; init; }
    public required long TotalLength { get; init; }
    public required int TokenEstimate { get; init; }
    public required int LineCount { get; init; }
    public required string LoadedAt { get; init; }
    public required int ChunkCount { get; init; }
    public required int CurrentChunkIndex { get; init; }
    public required int RemainingChunks { get; init; }
    public required int ResultCount { get; init; }
    public required int RecursionDepth { get; init; }
    public required int MaxRecursionDepth { get; init; }

    // Progress fields
    public double ProgressPercent { get; init; }
    public int ProcessedChars { get; init; }
    public int TotalChars { get; init; }
    public int AverageChunkSize { get; init; }
    public int RemainingTokenEstimate { get; init; }
}

/// <summary>
/// JSON output model for chunk data.
/// </summary>
public sealed record ChunkOutput
{
    public required int Index { get; init; }
    public required int TotalChunks { get; init; }
    public required int StartPosition { get; init; }
    public required int EndPosition { get; init; }
    public required int Length { get; init; }
    public required int TokenEstimate { get; init; }
    public required int? TokenCount { get; init; }
    public required bool HasMore { get; init; }
    public required string Content { get; init; }
    public required Dictionary<string, string> Metadata { get; init; }
}

/// <summary>
/// JSON output model for aggregation with FINAL signal.
/// </summary>
public sealed record AggregateOutput
{
    public required int ResultCount { get; init; }
    public required string Combined { get; init; }
    public required string Signal { get; init; }
    public required Dictionary<string, string> Results { get; init; }
}
