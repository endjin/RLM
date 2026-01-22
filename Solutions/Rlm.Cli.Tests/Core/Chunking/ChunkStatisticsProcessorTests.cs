// <copyright file="ChunkStatisticsProcessorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Chunking;

[TestClass]
public sealed class ChunkStatisticsProcessorTests
{
    private ChunkStatisticsProcessor processor = null!;

    [TestInitialize]
    public void Setup()
    {
        processor = new ChunkStatisticsProcessor();
    }

    [TestMethod]
    public async Task ProcessAsync_AddsWordCount()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("Hello world this is a test")
            .Build();

        // Act
        ContentChunk result = await processor.ProcessAsync(chunk);

        // Assert
        result.Metadata.ShouldContainKey("wordCount");
        result.Metadata["wordCount"].ShouldBe("6");
    }

    [TestMethod]
    public async Task ProcessAsync_AddsLineCount()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("Line 1\nLine 2\nLine 3")
            .Build();

        // Act
        ContentChunk result = await processor.ProcessAsync(chunk);

        // Assert
        result.Metadata.ShouldContainKey("lineCount");
        result.Metadata["lineCount"].ShouldBe("3");
    }

    [TestMethod]
    public async Task ProcessAsync_AddsCharCount()
    {
        // Arrange
        const string content = "Hello World";
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent(content)
            .Build();

        // Act
        ContentChunk result = await processor.ProcessAsync(chunk);

        // Assert
        result.Metadata.ShouldContainKey("charCount");
        result.Metadata["charCount"].ShouldBe(content.Length.ToString());
    }

    [TestMethod]
    public async Task ProcessAsync_AddsCharCountNoWhitespace()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("Hello World")
            .Build();

        // Act
        ContentChunk result = await processor.ProcessAsync(chunk);

        // Assert
        result.Metadata.ShouldContainKey("charCountNoWhitespace");
        result.Metadata["charCountNoWhitespace"].ShouldBe("10"); // "HelloWorld" without space
    }

    [TestMethod]
    public async Task ProcessAsync_EmptyContent_ReturnsZeroCounts()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("")
            .Build();

        // Act
        ContentChunk result = await processor.ProcessAsync(chunk);

        // Assert
        result.Metadata["wordCount"].ShouldBe("0");
        result.Metadata["lineCount"].ShouldBe("1"); // Empty string still has one "line"
        result.Metadata["charCount"].ShouldBe("0");
        result.Metadata["charCountNoWhitespace"].ShouldBe("0");
    }

    [TestMethod]
    public async Task ProcessAsync_PreservesExistingMetadata()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("Test content")
            .WithMetadata("existingKey", "existingValue")
            .Build();

        // Act
        ContentChunk result = await processor.ProcessAsync(chunk);

        // Assert
        result.Metadata.ShouldContainKey("existingKey");
        result.Metadata["existingKey"].ShouldBe("existingValue");
    }

    [TestMethod]
    public async Task ProcessAsync_ContentWithPunctuation_CountsWordsCorrectly()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("Hello, world! How are you?")
            .Build();

        // Act
        ContentChunk result = await processor.ProcessAsync(chunk);

        // Assert
        result.Metadata["wordCount"].ShouldBe("5"); // Hello, world, How, are, you
    }

    [TestMethod]
    public async Task ProcessAsync_ContentWithMultipleNewlines_CountsLinesCorrectly()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("Line 1\n\nLine 3\n\n\nLine 6")
            .Build();

        // Act
        ContentChunk result = await processor.ProcessAsync(chunk);

        // Assert
        result.Metadata["lineCount"].ShouldBe("6");
    }

    [TestMethod]
    public async Task ProcessAsync_ContentWithTabs_CountsWhitespaceCorrectly()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("Hello\tWorld\t!")
            .Build();

        // Act
        ContentChunk result = await processor.ProcessAsync(chunk);

        // Assert
        result.Metadata["charCountNoWhitespace"].ShouldBe("11"); // "HelloWorld!"
    }

    [TestMethod]
    public async Task ProcessAsync_CancellationRequested_ThrowsOperationCancelledException()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default().Build();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => processor.ProcessAsync(chunk, cts.Token));
    }

    [TestMethod]
    public async Task ProcessAsync_LargeContent_ProcessesSuccessfully()
    {
        // Arrange
        string content = string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"Line {i} with some words"));
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent(content)
            .Build();

        // Act
        ContentChunk result = await processor.ProcessAsync(chunk);

        // Assert
        result.Metadata["lineCount"].ShouldBe("1000");
        int.Parse(result.Metadata["wordCount"]).ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public async Task ProcessAsync_PreservesChunkProperties()
    {
        // Arrange
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithIndex(5)
            .WithContent("Test content")
            .WithPositions(100, 200)
            .Build();

        // Act
        ContentChunk result = await processor.ProcessAsync(chunk);

        // Assert
        result.Index.ShouldBe(5);
        result.Content.ShouldBe("Test content");
        result.StartPosition.ShouldBe(100);
        result.EndPosition.ShouldBe(200);
    }
}
