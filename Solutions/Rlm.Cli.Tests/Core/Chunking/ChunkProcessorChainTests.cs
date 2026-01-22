// <copyright file="ChunkProcessorChainTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using NSubstitute;
using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Chunking;

[TestClass]
public sealed class ChunkProcessorChainTests
{
    [TestMethod]
    public async Task ProcessAsync_EmptyChain_ReturnsOriginalChunk()
    {
        // Arrange
        ChunkProcessorChain chain = new([]);
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("Test content")
            .Build();

        // Act
        ContentChunk result = await chain.ProcessAsync(chunk);

        // Assert
        result.ShouldBe(chunk);
    }

    [TestMethod]
    public async Task ProcessAsync_SingleProcessor_AppliesProcessor()
    {
        // Arrange
        IChunkProcessor processor = Substitute.For<IChunkProcessor>();
        ContentChunk inputChunk = ContentChunkBuilder.Default()
            .WithContent("Original")
            .Build();
        ContentChunk processedChunk = ContentChunkBuilder.Default()
            .WithContent("Processed")
            .Build();

        processor.ProcessAsync(inputChunk, Arg.Any<CancellationToken>())
            .Returns(processedChunk);

        ChunkProcessorChain chain = new(processor);

        // Act
        ContentChunk result = await chain.ProcessAsync(inputChunk);

        // Assert
        result.ShouldBe(processedChunk);
        await processor.Received(1).ProcessAsync(inputChunk, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessAsync_MultipleProcessors_AppliesInOrder()
    {
        // Arrange
        IChunkProcessor processor1 = Substitute.For<IChunkProcessor>();
        IChunkProcessor processor2 = Substitute.For<IChunkProcessor>();
        IChunkProcessor processor3 = Substitute.For<IChunkProcessor>();

        ContentChunk chunk0 = ContentChunkBuilder.Default().WithContent("Original").Build();
        ContentChunk chunk1 = ContentChunkBuilder.Default().WithContent("After P1").Build();
        ContentChunk chunk2 = ContentChunkBuilder.Default().WithContent("After P2").Build();
        ContentChunk chunk3 = ContentChunkBuilder.Default().WithContent("After P3").Build();

        processor1.ProcessAsync(chunk0, Arg.Any<CancellationToken>()).Returns(chunk1);
        processor2.ProcessAsync(chunk1, Arg.Any<CancellationToken>()).Returns(chunk2);
        processor3.ProcessAsync(chunk2, Arg.Any<CancellationToken>()).Returns(chunk3);

        ChunkProcessorChain chain = new(processor1, processor2, processor3);

        // Act
        ContentChunk result = await chain.ProcessAsync(chunk0);

        // Assert
        result.ShouldBe(chunk3);
        await processor1.Received(1).ProcessAsync(chunk0, Arg.Any<CancellationToken>());
        await processor2.Received(1).ProcessAsync(chunk1, Arg.Any<CancellationToken>());
        await processor3.Received(1).ProcessAsync(chunk2, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessAsync_CancellationRequested_ThrowsOperationCancelledException()
    {
        // Arrange
        IChunkProcessor processor = Substitute.For<IChunkProcessor>();
        ContentChunk chunk = ContentChunkBuilder.Default().Build();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        ChunkProcessorChain chain = new(processor);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => chain.ProcessAsync(chunk, cts.Token));
    }

    [TestMethod]
    public void Count_ReturnsNumberOfProcessors()
    {
        // Arrange
        IChunkProcessor p1 = Substitute.For<IChunkProcessor>();
        IChunkProcessor p2 = Substitute.For<IChunkProcessor>();
        IChunkProcessor p3 = Substitute.For<IChunkProcessor>();

        ChunkProcessorChain chain = new(p1, p2, p3);

        // Act & Assert
        chain.Count.ShouldBe(3);
    }

    [TestMethod]
    public void Count_EmptyChain_ReturnsZero()
    {
        // Arrange
        ChunkProcessorChain chain = new([]);

        // Act & Assert
        chain.Count.ShouldBe(0);
    }

    [TestMethod]
    public void CreateDefault_ReturnsChainWithStatisticsProcessor()
    {
        // Act
        ChunkProcessorChain chain = ChunkProcessorChain.CreateDefault();

        // Assert
        chain.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task CreateDefault_ProcessesChunkWithStatistics()
    {
        // Arrange
        ChunkProcessorChain chain = ChunkProcessorChain.CreateDefault();
        ContentChunk chunk = ContentChunkBuilder.Default()
            .WithContent("Hello world test content")
            .Build();

        // Act
        ContentChunk result = await chain.ProcessAsync(chunk);

        // Assert
        result.Metadata.ShouldContainKey("wordCount");
        result.Metadata.ShouldContainKey("lineCount");
        result.Metadata.ShouldContainKey("charCount");
    }

    [TestMethod]
    public void Constructor_WithIEnumerable_CreatesChain()
    {
        // Arrange
        List<IChunkProcessor> processors =
        [
            Substitute.For<IChunkProcessor>(),
            Substitute.For<IChunkProcessor>()
        ];

        // Act
        ChunkProcessorChain chain = new(processors);

        // Assert
        chain.Count.ShouldBe(2);
    }
}
