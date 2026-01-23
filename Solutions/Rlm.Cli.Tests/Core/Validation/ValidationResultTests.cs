// <copyright file="ValidationResultTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Validation;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Validation;

[TestClass]
public sealed class ValidationResultTests
{
    [TestMethod]
    public void Success_ReturnsValidResult()
    {
        // Act
        ValidationResult result = ValidationResult.Success();

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty();
    }

    [TestMethod]
    public void SuccessWithWarnings_ReturnsValidResultWithWarnings()
    {
        // Act
        ValidationResult result = ValidationResult.SuccessWithWarnings("Warning 1", "Warning 2");

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Warnings.Count.ShouldBe(2);
        result.Warnings.ShouldContain("Warning 1");
        result.Warnings.ShouldContain("Warning 2");
    }

    [TestMethod]
    public void Failure_ReturnsInvalidResultWithErrors()
    {
        // Act
        ValidationResult result = ValidationResult.Failure("Error 1", "Error 2");

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldContain("Error 1");
        result.Errors.ShouldContain("Error 2");
        result.Warnings.ShouldBeEmpty();
    }

    [TestMethod]
    public void Failure_WithWarnings_ReturnsInvalidResultWithBoth()
    {
        // Arrange
        IReadOnlyList<string> errors = ["Error 1"];
        IReadOnlyList<string> warnings = ["Warning 1"];

        // Act
        ValidationResult result = ValidationResult.Failure(errors, warnings);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Warnings.Count.ShouldBe(1);
        result.Errors.ShouldContain("Error 1");
        result.Warnings.ShouldContain("Warning 1");
    }

    [TestMethod]
    public void SuccessWithWarnings_EmptyArray_ReturnsValidResultWithNoWarnings()
    {
        // Act
        ValidationResult result = ValidationResult.SuccessWithWarnings();

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Warnings.ShouldBeEmpty();
    }

    [TestMethod]
    public void Failure_EmptyArray_ReturnsInvalidResultWithNoErrors()
    {
        // Act
        ValidationResult result = ValidationResult.Failure();

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldBeEmpty();
    }
}
