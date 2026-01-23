// <copyright file="ContentChunkTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Chunking;

[TestClass]
public sealed class ContentChunkTests
{
    [TestMethod]
    public void Constructor_ValidParameters_SetsProperties()
    {
        // Arrange & Act
        ContentChunk chunk = new()
        {
            Index = 0,
            Content = "Test content",
            StartPosition = 0,
            EndPosition = 12,
            Metadata = new Dictionary<string, string> { ["key"] = "value" }
        };

        // Assert
        chunk.Index.ShouldBe(0);
        chunk.Content.ShouldBe("Test content");
        chunk.StartPosition.ShouldBe(0);
        chunk.EndPosition.ShouldBe(12);
        chunk.Metadata["key"].ShouldBe("value");
    }

    [TestMethod]
    public void Length_ReturnsContentLength()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("12345678901234567890") // 20 characters
            .Build();

        // Act & Assert
        chunk.Length.ShouldBe(20);
    }

    [TestMethod]
    public void TokenEstimate_ReturnsContentLengthDividedByFour()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("12345678901234567890") // 20 characters
            .Build();

        // Act & Assert
        chunk.TokenEstimate.ShouldBe(5); // 20 / 4
    }

    [TestMethod]
    public void TokenEstimate_ShortContent_ReturnsZero()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("abc") // 3 characters
            .Build();

        // Act & Assert
        chunk.TokenEstimate.ShouldBe(0); // 3 / 4 = 0 (integer division)
    }

    [TestMethod]
    public void Metadata_DefaultValue_IsEmptyDictionary()
    {
        // Arrange & Act
        ContentChunk chunk = new()
        {
            Index = 0,
            Content = "Test",
            StartPosition = 0,
            EndPosition = 4
        };

        // Assert
        chunk.Metadata.ShouldNotBeNull();
        chunk.Metadata.ShouldBeEmpty();
    }

    [TestMethod]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange - share the same metadata dictionary instance for equality comparison
        // since Dictionary<> doesn't have value equality
        Dictionary<string, string> metadata = new();
        ContentChunk chunk1 = new()
        {
            Index = 0,
            Content = "Test",
            StartPosition = 0,
            EndPosition = 4,
            Metadata = metadata
        };

        ContentChunk chunk2 = new()
        {
            Index = 0,
            Content = "Test",
            StartPosition = 0,
            EndPosition = 4,
            Metadata = metadata
        };

        // Assert
        chunk1.ShouldBe(chunk2);
    }

    [TestMethod]
    public void RecordEquality_DifferentContent_AreNotEqual()
    {
        // Arrange
        ContentChunk chunk1 = ContentChunkBuilder.Default()
            .WithContent("Content 1")
            .Build();

        ContentChunk chunk2 = ContentChunkBuilder.Default()
            .WithContent("Content 2")
            .Build();

        // Assert
        chunk1.ShouldNotBe(chunk2);
    }

    [TestMethod]
    public void Builder_Default_CreatesValidChunk()
    {
        // Act
        ContentChunk chunk = ContentChunkBuilder.Default().Build();

        // Assert
        chunk.Content.ShouldNotBeNullOrEmpty();
        chunk.Metadata.ShouldNotBeNull();
    }

    [TestMethod]
    public void Builder_WithUniformMetadata_SetsCorrectMetadata()
    {
        // Act
        ContentChunk chunk = ContentChunkBuilder.WithUniformMetadata("doc-1", 5)
            .WithIndex(2)
            .WithContent("Chunk content")
            .Build();

        // Assert
        chunk.Metadata["documentId"].ShouldBe("doc-1");
        chunk.Metadata["totalChunks"].ShouldBe("5");
        chunk.Metadata["strategy"].ShouldBe("uniform");
    }

    [TestMethod]
    public void Builder_WithSemanticMetadata_SetsCorrectMetadata()
    {
        // Act
        ContentChunk chunk = ContentChunkBuilder.WithSemanticMetadata(
                "doc-1",
                "Introduction",
                1,
                "Introduction")
            .Build();

        // Assert
        chunk.Metadata["documentId"].ShouldBe("doc-1");
        chunk.Metadata["sectionHeader"].ShouldBe("Introduction");
        chunk.Metadata["headerLevel"].ShouldBe("1");
        chunk.Metadata["headerPath"].ShouldBe("Introduction");
        chunk.Metadata["strategy"].ShouldBe("semantic");
    }

    [TestMethod]
    public void Builder_WithFilteringMetadata_SetsCorrectMetadata()
    {
        // Act
        ContentChunk chunk = ContentChunkBuilder.WithFilteringMetadata(
                "doc-1",
                "email@test.com",
                1)
            .Build();

        // Assert
        chunk.Metadata["documentId"].ShouldBe("doc-1");
        chunk.Metadata["matchedTerms"].ShouldBe("email@test.com");
        chunk.Metadata["matchCount"].ShouldBe("1");
        chunk.Metadata["strategy"].ShouldBe("filtering");
    }
}