// <copyright file="RlmDocumentTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Documents;

[TestClass]
public sealed class RlmDocumentTests
{
    [TestMethod]
    public void Constructor_ValidParameters_SetsProperties()
    {
        // Arrange
        DocumentMetadata metadata = DocumentMetadataBuilder.Default()
            .WithSource("/test/file.txt")
            .WithTotalLength(100)
            .Build();

        // Act
        RlmDocument document = new()
        {
            Id = "test-id",
            Content = "Test content",
            Metadata = metadata
        };

        // Assert
        document.Id.ShouldBe("test-id");
        document.Content.ShouldBe("Test content");
        document.Metadata.ShouldBe(metadata);
    }

    [TestMethod]
    public void Builder_Default_CreatesValidDocument()
    {
        // Act
        RlmDocument document = RlmDocumentBuilder.Default().Build();

        // Assert
        document.Id.ShouldNotBeNullOrEmpty();
        document.Content.ShouldNotBeNullOrEmpty();
        document.Metadata.ShouldNotBeNull();
    }

    [TestMethod]
    public void Builder_WithCustomValues_SetsAllProperties()
    {
        // Arrange & Act
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithId("custom-id")
            .WithContent("Custom content")
            .WithMetadata(m => m.WithSource("/custom/path.txt"))
            .Build();

        // Assert
        document.Id.ShouldBe("custom-id");
        document.Content.ShouldBe("Custom content");
        document.Metadata.Source.ShouldBe("/custom/path.txt");
    }

    [TestMethod]
    public void Builder_WithMarkdownContent_CreatesMarkdownDocument()
    {
        // Act
        RlmDocument document = RlmDocumentBuilder.WithMarkdownContent().Build();

        // Assert
        document.Content.ShouldContain("# Introduction");
        document.Content.ShouldContain("## Background");
        document.Content.ShouldContain("### Details");
    }

    [TestMethod]
    public void Builder_WithSearchableContent_CreatesSearchableDocument()
    {
        // Act
        RlmDocument document = RlmDocumentBuilder.WithSearchableContent().Build();

        // Assert
        document.Content.ShouldContain("email@example.com");
        document.Content.ShouldContain("test@domain.org");
    }
}