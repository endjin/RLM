// <copyright file="IDocumentValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Core.Validation;

/// <summary>
/// Validates documents before processing to prevent garbage-in problems.
/// </summary>
public interface IDocumentValidator
{
    /// <summary>
    /// Validates a document and returns the validation result.
    /// </summary>
    /// <param name="document">The document to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with success status and any error messages.</returns>
    Task<ValidationResult> ValidateAsync(RlmDocument document, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of document validation.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Whether the document passed validation.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Error messages if validation failed.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Warning messages that don't prevent processing.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a successful validation result with warnings.
    /// </summary>
    public static ValidationResult SuccessWithWarnings(params string[] warnings) => new()
    {
        IsValid = true,
        Warnings = warnings
    };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };

    /// <summary>
    /// Creates a failed validation result with warnings.
    /// </summary>
    public static ValidationResult Failure(IReadOnlyList<string> errors, IReadOnlyList<string> warnings) => new()
    {
        IsValid = false,
        Errors = errors,
        Warnings = warnings
    };
}
