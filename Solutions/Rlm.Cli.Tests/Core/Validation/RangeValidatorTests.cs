// <copyright file="RangeValidatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;
using Rlm.Cli.Core.Validation;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Validation;

[TestClass]
public sealed class RangeValidatorTests
{
    private RangeValidator validator = null!;

    [TestInitialize]
    public void Setup()
    {
        validator = new RangeValidator();
    }

    [TestMethod]
    public async Task ValidateAsync_ValidDocument_ReturnsSuccess()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("This is valid content.")
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task ValidateAsync_EmptyDocument_ReturnsError()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("")
            .Build();

        // Act
        ValidationResult result = await validator.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("too small"));
    }

    [TestMethod]
    public async Task ValidateAsync_DocumentExceedsMaxSize_ReturnsError()
    {
        // Arrange - Create a validator with small max size
        RangeValidator smallValidator = new(maxSizeBytes: 100, maxLineCount: 100000, minSizeChars: 1);
        string largeContent = new('X', 200);
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(largeContent)
            .Build();

        // Act
        ValidationResult result = await smallValidator.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("exceeds maximum size"));
    }

    [TestMethod]
    public async Task ValidateAsync_DocumentExceedsMaxLineCount_ReturnsError()
    {
        // Arrange - Create a validator with small max line count
        RangeValidator smallValidator = new(maxSizeBytes: 10_000_000, maxLineCount: 5, minSizeChars: 1);
        string contentWithManyLines = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"Line {i}"));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(contentWithManyLines)
            .WithMetadata(m => m
                .WithSource("/test/file.txt")
                .WithLineCount(10)
                .WithTotalLength(contentWithManyLines.Length))
            .Build();

        // Act
        ValidationResult result = await smallValidator.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("exceeds maximum line count"));
    }

    [TestMethod]
    public async Task ValidateAsync_DocumentApproachingMaxSize_ReturnsWarning()
    {
        // Arrange - Create a validator where content is 85% of max size
        RangeValidator customValidator = new(maxSizeBytes: 1000, maxLineCount: 100000, minSizeChars: 1);
        string content = new('X', 850); // 85% of 1000 bytes
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .Build();

        // Act
        ValidationResult result = await customValidator.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Warnings.ShouldContain(w => w.Contains("approaching size limit"));
    }

    [TestMethod]
    public async Task ValidateAsync_DocumentApproachingMaxLineCount_ReturnsWarning()
    {
        // Arrange - Create a validator where content has 85% of max lines
        RangeValidator customValidator = new(maxSizeBytes: 10_000_000, maxLineCount: 100, minSizeChars: 1);
        string contentWithManyLines = string.Join("\n", Enumerable.Range(1, 85).Select(i => $"Line {i}"));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(contentWithManyLines)
            .WithMetadata(m => m
                .WithSource("/test/file.txt")
                .WithLineCount(85)
                .WithTotalLength(contentWithManyLines.Length))
            .Build();

        // Act
        ValidationResult result = await customValidator.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Warnings.ShouldContain(w => w.Contains("approaching line count limit"));
    }

    [TestMethod]
    public async Task ValidateAsync_CancellationRequested_ThrowsOperationCancelledException()
    {
        // Arrange
        RlmDocument document = RlmDocumentBuilder.Default().Build();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => validator.ValidateAsync(document, cts.Token));
    }

    [TestMethod]
    public void DefaultConstants_HaveExpectedValues()
    {
        // Assert
        RangeValidator.DefaultMaxSizeBytes.ShouldBe(5 * 1024 * 1024); // 5 MB
        RangeValidator.DefaultMaxLineCount.ShouldBe(100_000);
        RangeValidator.DefaultMinSizeChars.ShouldBe(1);
    }

    [TestMethod]
    public async Task ValidateAsync_CustomMinSize_ValidatesCorrectly()
    {
        // Arrange - Require at least 10 characters
        RangeValidator customValidator = new(maxSizeBytes: 10_000_000, maxLineCount: 100_000, minSizeChars: 10);
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Short")
            .Build();

        // Act
        ValidationResult result = await customValidator.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("too small"));
    }

    [TestMethod]
    public async Task ValidateAsync_MultipleViolations_ReturnsMultipleErrors()
    {
        // Arrange - Create a validator with very restrictive limits
        RangeValidator strictValidator = new(maxSizeBytes: 50, maxLineCount: 2, minSizeChars: 100);
        string content = string.Join("\n", Enumerable.Range(1, 5).Select(i => $"Line {i} with content"));
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent(content)
            .WithMetadata(m => m
                .WithSource("/test/file.txt")
                .WithLineCount(5)
                .WithTotalLength(content.Length))
            .Build();

        // Act
        ValidationResult result = await strictValidator.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        // Should have errors for min size and max line count (and possibly max size)
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    public TestContext TestContext { get; set; }
}
