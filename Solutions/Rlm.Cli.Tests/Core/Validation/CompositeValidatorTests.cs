// <copyright file="CompositeValidatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using NSubstitute;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Core.Validation;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Validation;

[TestClass]
public sealed class CompositeValidatorTests
{
    [TestMethod]
    public async Task ValidateAsync_AllValidatorsPass_ReturnsSuccess()
    {
        // Arrange
        IDocumentValidator validator1 = Substitute.For<IDocumentValidator>();
        IDocumentValidator validator2 = Substitute.For<IDocumentValidator>();

        validator1.ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.Success());
        validator2.ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.Success());

        CompositeValidator composite = new(validator1, validator2);
        RlmDocument document = RlmDocumentBuilder.Default().Build();

        // Act
        ValidationResult result = await composite.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task ValidateAsync_FirstValidatorFails_StopsAndReturnsError()
    {
        // Arrange
        IDocumentValidator validator1 = Substitute.For<IDocumentValidator>();
        IDocumentValidator validator2 = Substitute.For<IDocumentValidator>();

        validator1.ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.Failure("Error from validator 1"));
        validator2.ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.Success());

        CompositeValidator composite = new(validator1, validator2);
        RlmDocument document = RlmDocumentBuilder.Default().Build();

        // Act
        ValidationResult result = await composite.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("Error from validator 1");

        // Validator 2 should not be called when first fails
        await validator2.DidNotReceive().ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ValidateAsync_RunAllValidators_ContinuesAfterError()
    {
        // Arrange
        IDocumentValidator validator1 = Substitute.For<IDocumentValidator>();
        IDocumentValidator validator2 = Substitute.For<IDocumentValidator>();

        validator1.ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.Failure("Error 1"));
        validator2.ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.Failure("Error 2"));

        CompositeValidator composite = new([validator1, validator2], runAllValidators: true);
        RlmDocument document = RlmDocumentBuilder.Default().Build();

        // Act
        ValidationResult result = await composite.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("Error 1");
        result.Errors.ShouldContain("Error 2");

        // Both validators should be called
        await validator1.Received(1).ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>());
        await validator2.Received(1).ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ValidateAsync_AggregatesWarnings()
    {
        // Arrange
        IDocumentValidator validator1 = Substitute.For<IDocumentValidator>();
        IDocumentValidator validator2 = Substitute.For<IDocumentValidator>();

        validator1.ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.SuccessWithWarnings("Warning 1"));
        validator2.ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.SuccessWithWarnings("Warning 2"));

        CompositeValidator composite = new(validator1, validator2);
        RlmDocument document = RlmDocumentBuilder.Default().Build();

        // Act
        ValidationResult result = await composite.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Warnings.Count.ShouldBe(2);
        result.Warnings.ShouldContain("Warning 1");
        result.Warnings.ShouldContain("Warning 2");
    }

    [TestMethod]
    public async Task ValidateAsync_MixedErrorsAndWarnings_ReturnsAll()
    {
        // Arrange
        IDocumentValidator validator1 = Substitute.For<IDocumentValidator>();
        IDocumentValidator validator2 = Substitute.For<IDocumentValidator>();

        validator1.ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.Failure(["Error 1"], ["Warning 1"]));
        validator2.ValidateAsync(Arg.Any<RlmDocument>(), Arg.Any<CancellationToken>())
            .Returns(ValidationResult.SuccessWithWarnings("Warning 2"));

        CompositeValidator composite = new([validator1, validator2], runAllValidators: true);
        RlmDocument document = RlmDocumentBuilder.Default().Build();

        // Act
        ValidationResult result = await composite.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("Error 1");
        result.Warnings.ShouldContain("Warning 1");
        result.Warnings.ShouldContain("Warning 2");
    }

    [TestMethod]
    public async Task ValidateAsync_EmptyValidatorList_ReturnsSuccess()
    {
        // Arrange
        CompositeValidator composite = new(Array.Empty<IDocumentValidator>());
        RlmDocument document = RlmDocumentBuilder.Default().Build();

        // Act
        ValidationResult result = await composite.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [TestMethod]
    public async Task ValidateAsync_CancellationRequested_ThrowsOperationCancelledException()
    {
        // Arrange
        IDocumentValidator validator = Substitute.For<IDocumentValidator>();
        CompositeValidator composite = new(validator);
        RlmDocument document = RlmDocumentBuilder.Default().Build();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => composite.ValidateAsync(document, cts.Token));
    }

    [TestMethod]
    public void CreateDefault_ReturnsSyntacticAndRangeValidators()
    {
        // Act
        CompositeValidator composite = CompositeValidator.CreateDefault();

        // Assert - Test by validating a document
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Valid content")
            .Build();

        // Should not throw and should run successfully
        ValidationResult result = composite.ValidateAsync(document, TestContext.CancellationToken).GetAwaiter().GetResult();
        result.IsValid.ShouldBeTrue();
    }

    [TestMethod]
    public async Task CreateDefault_DetectsBinaryContent()
    {
        // Arrange
        CompositeValidator composite = CompositeValidator.CreateDefault();
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Text with\0null bytes")
            .Build();

        // Act
        ValidationResult result = await composite.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("binary"));
    }

    [TestMethod]
    public async Task CreateDefault_DetectsEmptyDocument()
    {
        // Arrange
        CompositeValidator composite = CompositeValidator.CreateDefault();
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("")
            .Build();

        // Act
        ValidationResult result = await composite.ValidateAsync(document, TestContext.CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("too small"));
    }

    public TestContext TestContext { get; set; }
}
