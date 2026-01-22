// <copyright file="RangeValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Core.Validation;

/// <summary>
/// Validates document size constraints including maximum file size and line count.
/// </summary>
public sealed class RangeValidator : IDocumentValidator
{
    /// <summary>
    /// Default maximum document size in bytes (5 MB).
    /// </summary>
    public const int DefaultMaxSizeBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Default maximum line count.
    /// </summary>
    public const int DefaultMaxLineCount = 100_000;

    /// <summary>
    /// Default minimum document size in characters.
    /// </summary>
    public const int DefaultMinSizeChars = 1;

    private readonly int _maxSizeBytes;
    private readonly int _maxLineCount;
    private readonly int _minSizeChars;

    /// <summary>
    /// Creates a new RangeValidator with default limits.
    /// </summary>
    public RangeValidator() : this(DefaultMaxSizeBytes, DefaultMaxLineCount, DefaultMinSizeChars)
    {
    }

    /// <summary>
    /// Creates a new RangeValidator with custom limits.
    /// </summary>
    public RangeValidator(int maxSizeBytes, int maxLineCount, int minSizeChars)
    {
        _maxSizeBytes = maxSizeBytes;
        _maxLineCount = maxLineCount;
        _minSizeChars = minSizeChars;
    }

    public Task<ValidationResult> ValidateAsync(RlmDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<string> errors = [];
        List<string> warnings = [];

        // Check minimum size
        if (document.Content.Length < _minSizeChars)
        {
            errors.Add($"Document is too small ({document.Content.Length} chars). Minimum required: {_minSizeChars} chars.");
        }

        // Check maximum size (approximate byte count using UTF-8)
        int estimatedBytes = System.Text.Encoding.UTF8.GetByteCount(document.Content);
        if (estimatedBytes > _maxSizeBytes)
        {
            double sizeMb = estimatedBytes / (1024.0 * 1024.0);
            double maxMb = _maxSizeBytes / (1024.0 * 1024.0);
            errors.Add($"Document exceeds maximum size ({sizeMb:F1} MB). Maximum allowed: {maxMb:F1} MB.");
        }

        // Check line count
        if (document.Metadata.LineCount > _maxLineCount)
        {
            errors.Add($"Document exceeds maximum line count ({document.Metadata.LineCount:N0} lines). Maximum allowed: {_maxLineCount:N0} lines.");
        }

        // Warnings for documents approaching limits
        if (estimatedBytes > _maxSizeBytes * 0.8 && estimatedBytes <= _maxSizeBytes)
        {
            double sizeMb = estimatedBytes / (1024.0 * 1024.0);
            warnings.Add($"Document is approaching size limit ({sizeMb:F1} MB).");
        }

        if (document.Metadata.LineCount > _maxLineCount * 0.8 && document.Metadata.LineCount <= _maxLineCount)
        {
            warnings.Add($"Document is approaching line count limit ({document.Metadata.LineCount:N0} lines).");
        }

        return Task.FromResult(errors.Count > 0
            ? ValidationResult.Failure([.. errors], [.. warnings])
            : warnings.Count > 0
                ? ValidationResult.SuccessWithWarnings([.. warnings])
                : ValidationResult.Success());
    }
}
