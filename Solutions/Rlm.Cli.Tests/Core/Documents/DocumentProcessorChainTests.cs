// <copyright file="DocumentProcessorChainTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using NSubstitute;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Documents;

[TestClass]
public sealed class DocumentProcessorChainTests
{
    [TestMethod]
    public async Task ProcessAsync_EmptyChain_ReturnsOriginalDocument()
    {
        // Arrange
        DocumentProcessorChain chain = new([]);
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("Test content")
            .Build();

        // Act
        RlmDocument result = await chain.ProcessAsync(document);

        // Assert
        result.ShouldBe(document);
    }

    [TestMethod]
    public async Task ProcessAsync_SingleProcessor_AppliesProcessor()
    {
        // Arrange
        IDocumentProcessor processor = Substitute.For<IDocumentProcessor>();
        RlmDocument inputDoc = RlmDocumentBuilder.Default()
            .WithContent("Original")
            .Build();
        RlmDocument processedDoc = RlmDocumentBuilder.Default()
            .WithContent("Processed")
            .Build();

        processor.ProcessAsync(inputDoc, Arg.Any<CancellationToken>())
            .Returns(processedDoc);

        DocumentProcessorChain chain = new(processor);

        // Act
        RlmDocument result = await chain.ProcessAsync(inputDoc);

        // Assert
        result.ShouldBe(processedDoc);
        await processor.Received(1).ProcessAsync(inputDoc, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessAsync_MultipleProcessors_AppliesInOrder()
    {
        // Arrange
        IDocumentProcessor processor1 = Substitute.For<IDocumentProcessor>();
        IDocumentProcessor processor2 = Substitute.For<IDocumentProcessor>();
        IDocumentProcessor processor3 = Substitute.For<IDocumentProcessor>();

        RlmDocument doc0 = RlmDocumentBuilder.Default().WithId("doc0").Build();
        RlmDocument doc1 = RlmDocumentBuilder.Default().WithId("doc1").Build();
        RlmDocument doc2 = RlmDocumentBuilder.Default().WithId("doc2").Build();
        RlmDocument doc3 = RlmDocumentBuilder.Default().WithId("doc3").Build();

        processor1.ProcessAsync(doc0, Arg.Any<CancellationToken>()).Returns(doc1);
        processor2.ProcessAsync(doc1, Arg.Any<CancellationToken>()).Returns(doc2);
        processor3.ProcessAsync(doc2, Arg.Any<CancellationToken>()).Returns(doc3);

        DocumentProcessorChain chain = new(processor1, processor2, processor3);

        // Act
        RlmDocument result = await chain.ProcessAsync(doc0);

        // Assert
        result.ShouldBe(doc3);
        await processor1.Received(1).ProcessAsync(doc0, Arg.Any<CancellationToken>());
        await processor2.Received(1).ProcessAsync(doc1, Arg.Any<CancellationToken>());
        await processor3.Received(1).ProcessAsync(doc2, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ProcessAsync_CancellationRequested_ThrowsOperationCancelledException()
    {
        // Arrange
        IDocumentProcessor processor = Substitute.For<IDocumentProcessor>();
        RlmDocument document = RlmDocumentBuilder.Default().Build();
        CancellationTokenSource cts = new();
        cts.Cancel();

        DocumentProcessorChain chain = new(processor);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => chain.ProcessAsync(document, cts.Token));
    }

    [TestMethod]
    public void Count_ReturnsNumberOfProcessors()
    {
        // Arrange
        IDocumentProcessor p1 = Substitute.For<IDocumentProcessor>();
        IDocumentProcessor p2 = Substitute.For<IDocumentProcessor>();
        IDocumentProcessor p3 = Substitute.For<IDocumentProcessor>();

        DocumentProcessorChain chain = new(p1, p2, p3);

        // Act & Assert
        chain.Count.ShouldBe(3);
    }

    [TestMethod]
    public void Count_EmptyChain_ReturnsZero()
    {
        // Arrange
        DocumentProcessorChain chain = new([]);

        // Act & Assert
        chain.Count.ShouldBe(0);
    }

    [TestMethod]
    public void CreateDefault_ReturnsChainWithCleaningAndMetadata()
    {
        // Act
        DocumentProcessorChain chain = DocumentProcessorChain.CreateDefault();

        // Assert
        chain.Count.ShouldBe(2);
    }

    [TestMethod]
    public async Task CreateDefault_ProcessesDocumentWithCleaningAndMetadata()
    {
        // Arrange
        DocumentProcessorChain chain = DocumentProcessorChain.CreateDefault();
        RlmDocument document = RlmDocumentBuilder.Default()
            .WithContent("""
                # My Document

                Some    extra    spaces.

                <!-- Hidden comment -->

                Word count test.
                """)
            .WithMetadata(m => m.WithSource("/test/file.md"))
            .Build();

        // Act
        RlmDocument result = await chain.ProcessAsync(document);

        // Assert
        // Content should be cleaned
        result.Content.ShouldNotContain("<!-- Hidden comment -->");
        result.Content.ShouldNotContain("    ");

        // Metadata should be extracted
        result.Metadata.WordCount.ShouldNotBeNull();
        result.Metadata.HeaderCount.ShouldNotBeNull();
    }

    [TestMethod]
    public void Constructor_WithIEnumerable_CreatesChain()
    {
        // Arrange
        List<IDocumentProcessor> processors =
        [
            Substitute.For<IDocumentProcessor>(),
            Substitute.For<IDocumentProcessor>()
        ];

        // Act
        DocumentProcessorChain chain = new(processors);

        // Assert
        chain.Count.ShouldBe(2);
    }
}
