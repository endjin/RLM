// <copyright file="CompositeValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;

namespace Rlm.Cli.Core.Validation;

/// <summary>
/// Combines multiple validators and aggregates their results.
/// Validation stops on first error unless configured for full validation.
/// </summary>
public sealed class CompositeValidator : IDocumentValidator
{
    private readonly IReadOnlyList<IDocumentValidator> _validators;
    private readonly bool _runAllValidators;

    /// <summary>
    /// Creates a composite validator that stops on first error.
    /// </summary>
    public CompositeValidator(params IDocumentValidator[] validators)
        : this(validators, runAllValidators: false)
    {
    }

    /// <summary>
    /// Creates a composite validator with specified behavior.
    /// </summary>
    /// <param name="validators">The validators to run.</param>
    /// <param name="runAllValidators">If true, runs all validators even after errors.</param>
    public CompositeValidator(IEnumerable<IDocumentValidator> validators, bool runAllValidators)
    {
        _validators = validators.ToList();
        _runAllValidators = runAllValidators;
    }

    public async Task<ValidationResult> ValidateAsync(RlmDocument document, CancellationToken cancellationToken = default)
    {
        List<string> allErrors = [];
        List<string> allWarnings = [];

        foreach (IDocumentValidator validator in _validators)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ValidationResult result = await validator.ValidateAsync(document, cancellationToken);

            allErrors.AddRange(result.Errors);
            allWarnings.AddRange(result.Warnings);

            // Stop on first error unless configured to run all
            if (!result.IsValid && !_runAllValidators)
            {
                break;
            }
        }

        return allErrors.Count > 0
            ? ValidationResult.Failure(allErrors, allWarnings)
            : allWarnings.Count > 0
                ? ValidationResult.SuccessWithWarnings([.. allWarnings])
                : ValidationResult.Success();
    }

    /// <summary>
    /// Creates a default composite validator with syntactic and range validation.
    /// </summary>
    public static CompositeValidator CreateDefault() => new(
        new SyntacticValidator(),
        new RangeValidator()
    );
}
