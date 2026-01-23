// <copyright file="DocumentMetadataTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Documents;

[TestClass]
public sealed class DocumentMetadataTests
{
    [TestMethod]
    public void Constructor_ValidParameters_SetsProperties()
    {
        // Arrange & Act
        DocumentMetadata metadata = new()
        {
            Source = "/test/file.txt",
            TotalLength = 1000,
            TokenEstimate = 250,
            LineCount = 50
        };

        // Assert
        metadata.Source.ShouldBe("/test/file.txt");
        metadata.TotalLength.ShouldBe(1000);
        metadata.TokenEstimate.ShouldBe(250);
        metadata.LineCount.ShouldBe(50);
    }

    [TestMethod]
    public void LoadedAt_DefaultValue_IsCloseToUtcNow()
    {
        // Arrange
        DateTimeOffset beforeCreation = DateTimeOffset.UtcNow;

        // Act
        DocumentMetadata metadata = new()
        {
            Source = "/test/file.txt",
            TotalLength = 100,
            TokenEstimate = 25,
            LineCount = 10
        };

        DateTimeOffset afterCreation = DateTimeOffset.UtcNow;

        // Assert
        metadata.LoadedAt.ShouldBeGreaterThanOrEqualTo(beforeCreation);
        metadata.LoadedAt.ShouldBeLessThanOrEqualTo(afterCreation);
    }

    [TestMethod]
    public void LoadedAt_ExplicitValue_IsSet()
    {
        // Arrange
        DateTimeOffset specificTime = new(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        DocumentMetadata metadata = new()
        {
            Source = "/test/file.txt",
            TotalLength = 100,
            TokenEstimate = 25,
            LineCount = 10,
            LoadedAt = specificTime
        };

        // Assert
        metadata.LoadedAt.ShouldBe(specificTime);
    }

    [TestMethod]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        DateTimeOffset loadedAt = DateTimeOffset.UtcNow;
        DocumentMetadata metadata1 = new()
        {
            Source = "/test/file.txt",
            TotalLength = 100,
            TokenEstimate = 25,
            LineCount = 10,
            LoadedAt = loadedAt
        };

        DocumentMetadata metadata2 = new()
        {
            Source = "/test/file.txt",
            TotalLength = 100,
            TokenEstimate = 25,
            LineCount = 10,
            LoadedAt = loadedAt
        };

        // Assert
        metadata1.ShouldBe(metadata2);
    }

    [TestMethod]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        // Arrange
        DocumentMetadata metadata1 = DocumentMetadataBuilder.Default()
            .WithSource("/test/file1.txt")
            .Build();

        DocumentMetadata metadata2 = DocumentMetadataBuilder.Default()
            .WithSource("/test/file2.txt")
            .Build();

        // Assert
        metadata1.ShouldNotBe(metadata2);
    }

    [TestMethod]
    public void Builder_Default_CreatesValidMetadata()
    {
        // Act
        DocumentMetadata metadata = DocumentMetadataBuilder.Default().Build();

        // Assert
        metadata.Source.ShouldNotBeNullOrEmpty();
        metadata.TotalLength.ShouldBeGreaterThan(0);
        metadata.TokenEstimate.ShouldBeGreaterThan(0);
        metadata.LineCount.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public void Builder_WithTotalLength_AlsoSetsTokenEstimate()
    {
        // Act
        DocumentMetadata metadata = DocumentMetadataBuilder.Default()
            .WithTotalLength(400)
            .Build();

        // Assert
        metadata.TotalLength.ShouldBe(400);
        metadata.TokenEstimate.ShouldBe(100); // 400 / 4
    }
}