// <copyright file="RlmSessionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Session;

[TestClass]
public sealed class RlmSessionTests
{
    [TestMethod]
    public void HasDocument_NullContent_ReturnsFalse()
    {
        // Arrange
        RlmSession session = new();

        // Act & Assert
        session.HasDocument.ShouldBeFalse();
    }

    [TestMethod]
    public void HasDocument_WithContent_ReturnsTrue()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedDocument().Build();

        // Act & Assert
        session.HasDocument.ShouldBeTrue();
    }

    [TestMethod]
    public void HasChunks_EmptyBuffer_ReturnsFalse()
    {
        // Arrange
        RlmSession session = new();

        // Act & Assert
        session.HasChunks.ShouldBeFalse();
    }

    [TestMethod]
    public void HasChunks_WithChunks_ReturnsTrue()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3).Build();

        // Act & Assert
        session.HasChunks.ShouldBeTrue();
    }

    [TestMethod]
    public void HasMoreChunks_AtBeginning_ReturnsTrue()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3).Build();

        // Act & Assert
        session.HasMoreChunks.ShouldBeTrue();
    }

    [TestMethod]
    public void HasMoreChunks_AtLastChunk_ReturnsFalse()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3)
            .WithCurrentChunkIndex(2)
            .Build();

        // Act & Assert
        session.HasMoreChunks.ShouldBeFalse();
    }

    [TestMethod]
    public void HasMoreChunks_EmptyBuffer_ReturnsFalse()
    {
        // Arrange
        RlmSession session = new();

        // Act & Assert
        session.HasMoreChunks.ShouldBeFalse();
    }

    [TestMethod]
    public void CurrentChunk_ValidIndex_ReturnsChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3)
            .WithCurrentChunkIndex(1)
            .Build();

        // Act
        ContentChunk? chunk = session.CurrentChunk;

        // Assert
        chunk.ShouldNotBeNull();
        chunk.Index.ShouldBe(1);
    }

    [TestMethod]
    public void CurrentChunk_IndexOutOfRange_ReturnsNull()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3)
            .WithCurrentChunkIndex(5)
            .Build();

        // Act
        ContentChunk? chunk = session.CurrentChunk;

        // Assert
        chunk.ShouldBeNull();
    }

    [TestMethod]
    public void CurrentChunk_EmptyBuffer_ReturnsNull()
    {
        // Arrange
        RlmSession session = new();

        // Act
        ContentChunk? chunk = session.CurrentChunk;

        // Assert
        chunk.ShouldBeNull();
    }

    [TestMethod]
    public void ToDocument_WithMetadata_ReturnsDocument()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedDocument().Build();

        // Act
        RlmDocument document = session.ToDocument();

        // Assert
        document.ShouldNotBeNull();
        document.Content.ShouldBe(session.Content);
        document.Metadata.ShouldBe(session.Metadata);
        document.Id.ShouldBe(session.Metadata!.Source);
    }

    [TestMethod]
    public void ToDocument_NoMetadata_CreatesDefaultMetadata()
    {
        // Arrange
        RlmSession session = new() { Content = "Test content" };

        // Act
        RlmDocument document = session.ToDocument();

        // Assert
        document.Id.ShouldBe("session");
        document.Metadata.Source.ShouldBe("memory");
        document.Metadata.TotalLength.ShouldBe(12);
    }

    [TestMethod]
    public void ToDocument_NullContent_ReturnsEmptyContent()
    {
        // Arrange
        RlmSession session = new();

        // Act
        RlmDocument document = session.ToDocument();

        // Assert
        document.Content.ShouldBeEmpty();
    }

    [TestMethod]
    public void LoadDocument_SetsContentAndMetadata()
    {
        // Arrange
        RlmSession session = new();
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("New content")
            .Build();

        // Act
        session.LoadDocument(document);

        // Assert
        session.Content.ShouldBe("New content");
        session.Metadata.ShouldBe(document.Metadata);
    }

    [TestMethod]
    public void LoadDocument_ClearsChunkBuffer()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3).Build();
        RlmDocument document = RlmDocumentBuilder.Default().Build();

        // Act
        session.LoadDocument(document);

        // Assert
        session.ChunkBuffer.ShouldBeEmpty();
        session.CurrentChunkIndex.ShouldBe(0);
    }

    [TestMethod]
    public void Clear_ResetsAllState()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithStoredResults().Build();

        // Act
        session.Clear();

        // Assert
        session.Content.ShouldBeNull();
        session.Metadata.ShouldBeNull();
        session.ChunkBuffer.ShouldBeEmpty();
        session.CurrentChunkIndex.ShouldBe(0);
        session.Results.ShouldBeEmpty();
    }

    [TestMethod]
    public void ToResultBuffer_CopiesAllResults()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("key1", "value1")
            .WithResult("key2", "value2")
            .Build();

        // Act
        ResultBuffer buffer = session.ToResultBuffer();

        // Assert
        buffer.Count.ShouldBe(2);
        buffer.Get("key1").ShouldBe("value1");
        buffer.Get("key2").ShouldBe("value2");
    }

    [TestMethod]
    public void ToResultBuffer_EmptyResults_ReturnsEmptyBuffer()
    {
        // Arrange
        RlmSession session = new();

        // Act
        ResultBuffer buffer = session.ToResultBuffer();

        // Assert
        buffer.Count.ShouldBe(0);
    }

    [TestMethod]
    public void Builder_Empty_CreatesEmptySession()
    {
        // Act
        RlmSession session = RlmSessionBuilder.Empty().Build();

        // Assert
        session.Content.ShouldBeNull();
        session.Metadata.ShouldBeNull();
        session.ChunkBuffer.ShouldBeEmpty();
        session.Results.ShouldBeEmpty();
    }

    [TestMethod]
    public void Builder_WithLoadedDocument_CreatesSessionWithDocument()
    {
        // Act
        RlmSession session = RlmSessionBuilder.WithLoadedDocument().Build();

        // Assert
        session.HasDocument.ShouldBeTrue();
        session.Content.ShouldNotBeNullOrEmpty();
        session.Metadata.ShouldNotBeNull();
    }

    [TestMethod]
    public void Builder_WithChunks_CreatesSpecifiedNumberOfChunks()
    {
        // Act
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5).Build();

        // Assert
        session.ChunkBuffer.Count.ShouldBe(5);
    }

    [TestMethod]
    public void Builder_WithStoredResults_CreatesSessionWithResults()
    {
        // Act
        RlmSession session = RlmSessionBuilder.WithStoredResults().Build();

        // Assert
        session.Results.Count.ShouldBe(3);
    }

    [TestMethod]
    public void DefaultValues_AreInitializedCorrectly()
    {
        // Act
        RlmSession session = new();

        // Assert
        session.ChunkBuffer.ShouldNotBeNull();
        session.ChunkBuffer.ShouldBeEmpty();
        session.Results.ShouldNotBeNull();
        session.Results.ShouldBeEmpty();
        session.CurrentChunkIndex.ShouldBe(0);
    }

    [TestMethod]
    public void RecursionDepth_DefaultValue_IsZero()
    {
        // Act
        RlmSession session = new();

        // Assert
        session.RecursionDepth.ShouldBe(0);
    }

    [TestMethod]
    public void MaxRecursionDepth_IsFive()
    {
        // Assert
        RlmSession.MaxRecursionDepth.ShouldBe(5);
    }

    [TestMethod]
    public void IncrementRecursionDepth_IncrementsDepth()
    {
        // Arrange
        RlmSession session = new();

        // Act
        session.IncrementRecursionDepth();

        // Assert
        session.RecursionDepth.ShouldBe(1);
    }

    [TestMethod]
    public void IncrementRecursionDepth_ReturnsFalse_WhenBelowLimit()
    {
        // Arrange
        RlmSession session = new();

        // Act
        bool exceeded = session.IncrementRecursionDepth();

        // Assert
        exceeded.ShouldBeFalse();
    }

    [TestMethod]
    public void IncrementRecursionDepth_ReturnsTrue_WhenLimitExceeded()
    {
        // Arrange
        RlmSession session = new() { RecursionDepth = RlmSession.MaxRecursionDepth };

        // Act
        bool exceeded = session.IncrementRecursionDepth();

        // Assert
        exceeded.ShouldBeTrue();
        session.RecursionDepth.ShouldBe(RlmSession.MaxRecursionDepth + 1);
    }

    [TestMethod]
    public void DecrementRecursionDepth_DecrementsDepth()
    {
        // Arrange
        RlmSession session = new() { RecursionDepth = 3 };

        // Act
        session.DecrementRecursionDepth();

        // Assert
        session.RecursionDepth.ShouldBe(2);
    }

    [TestMethod]
    public void DecrementRecursionDepth_DoesNotGoBelowZero()
    {
        // Arrange
        RlmSession session = new() { RecursionDepth = 0 };

        // Act
        session.DecrementRecursionDepth();

        // Assert
        session.RecursionDepth.ShouldBe(0);
    }

    [TestMethod]
    public void Clear_ResetsRecursionDepth()
    {
        // Arrange
        RlmSession session = new() { RecursionDepth = 3 };

        // Act
        session.Clear();

        // Assert
        session.RecursionDepth.ShouldBe(0);
    }
}