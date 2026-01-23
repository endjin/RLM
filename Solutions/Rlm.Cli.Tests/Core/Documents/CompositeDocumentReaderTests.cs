// <copyright file="CompositeDocumentReaderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using NSubstitute;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Tests.Builders;
using Shouldly;

namespace Rlm.Cli.Tests.Core.Documents;

[TestClass]
public sealed class CompositeDocumentReaderTests
{
    [TestMethod]
    public void CanRead_NoReaders_ReturnsFalse()
    {
        // Arrange
        CompositeDocumentReader reader = new();
        Uri source = "/test/file.txt".ToSourceUri();

        // Act
        bool result = reader.CanRead(source);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void CanRead_FirstReaderCanRead_ReturnsTrue()
    {
        // Arrange
        IDocumentReader? reader1 = Substitute.For<IDocumentReader>();
        IDocumentReader? reader2 = Substitute.For<IDocumentReader>();

        reader1.CanRead(Arg.Any<Uri>()).Returns(true);
        reader2.CanRead(Arg.Any<Uri>()).Returns(false);

        CompositeDocumentReader compositeReader = new(reader1, reader2);

        // Act
        bool result = compositeReader.CanRead("/test/file.txt");

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void CanRead_SecondReaderCanRead_ReturnsTrue()
    {
        // Arrange
        IDocumentReader? reader1 = Substitute.For<IDocumentReader>();
        IDocumentReader? reader2 = Substitute.For<IDocumentReader>();

        reader1.CanRead(Arg.Any<Uri>()).Returns(false);
        reader2.CanRead(Arg.Any<Uri>()).Returns(true);

        CompositeDocumentReader compositeReader = new(reader1, reader2);

        // Act
        bool result = compositeReader.CanRead("/test/file.txt");

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void CanRead_NoReaderCanRead_ReturnsFalse()
    {
        // Arrange
        IDocumentReader? reader1 = Substitute.For<IDocumentReader>();
        IDocumentReader? reader2 = Substitute.For<IDocumentReader>();

        reader1.CanRead(Arg.Any<Uri>()).Returns(false);
        reader2.CanRead(Arg.Any<Uri>()).Returns(false);

        CompositeDocumentReader compositeReader = new(reader1, reader2);

        // Act
        bool result = compositeReader.CanRead("/test/file.txt");

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public async Task ReadAsync_NoReaders_ReturnsNull()
    {
        // Arrange
        CompositeDocumentReader reader = new();

        // Act
        RlmDocument? result = await reader.ReadAsync("/test/file.txt", TestContext.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public async Task ReadAsync_NoReaderCanRead_ReturnsNull()
    {
        // Arrange
        IDocumentReader? reader1 = Substitute.For<IDocumentReader>();
        IDocumentReader? reader2 = Substitute.For<IDocumentReader>();

        reader1.CanRead(Arg.Any<Uri>()).Returns(false);
        reader2.CanRead(Arg.Any<Uri>()).Returns(false);

        CompositeDocumentReader compositeReader = new(reader1, reader2);

        // Act
        RlmDocument? result = await compositeReader.ReadAsync("/test/file.txt", TestContext.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public async Task ReadAsync_FirstReaderCanRead_UsesFirstReader()
    {
        // Arrange
        RlmDocument expectedDocument = RlmDocumentBuilder.Default()
            .WithId("from-reader-1")
            .Build();

        IDocumentReader? reader1 = Substitute.For<IDocumentReader>();
        IDocumentReader? reader2 = Substitute.For<IDocumentReader>();

        reader1.CanRead(Arg.Any<Uri>()).Returns(true);
        reader1.ReadAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<RlmDocument?>(expectedDocument));
        reader2.CanRead(Arg.Any<Uri>()).Returns(true);

        CompositeDocumentReader compositeReader = new(reader1, reader2);

        // Act
        RlmDocument? result = await compositeReader.ReadAsync("/test/file.txt", TestContext.CancellationToken);

        // Assert
        result.ShouldBe(expectedDocument);
        await reader2.DidNotReceive().ReadAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ReadAsync_SecondReaderCanRead_UsesSecondReader()
    {
        // Arrange
        RlmDocument expectedDocument = RlmDocumentBuilder.Default()
            .WithId("from-reader-2")
            .Build();

        IDocumentReader? reader1 = Substitute.For<IDocumentReader>();
        IDocumentReader? reader2 = Substitute.For<IDocumentReader>();

        reader1.CanRead(Arg.Any<Uri>()).Returns(false);
        reader2.CanRead(Arg.Any<Uri>()).Returns(true);
        reader2.ReadAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RlmDocument?>(expectedDocument));

        CompositeDocumentReader compositeReader = new(reader1, reader2);

        // Act
        RlmDocument? result = await compositeReader.ReadAsync("/test/file.txt", TestContext.CancellationToken);

        // Assert
        result.ShouldBe(expectedDocument);
        await reader1.DidNotReceive().ReadAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ReadAsync_ReaderReturnsNull_ReturnsNull()
    {
        // Arrange
        IDocumentReader? reader = Substitute.For<IDocumentReader>();
        reader.CanRead(Arg.Any<Uri>()).Returns(true);
        reader.ReadAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<RlmDocument?>(null));

        CompositeDocumentReader compositeReader = new(reader);

        // Act
        RlmDocument? result = await compositeReader.ReadAsync("/test/file.txt", TestContext.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public async Task ReadAsync_PassesCancellationToken_ToSelectedReader()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        IDocumentReader? reader = Substitute.For<IDocumentReader>();
        reader.CanRead(Arg.Any<Uri>()).Returns(true);
        reader.ReadAsync(Arg.Any<Uri>(), cts.Token).Returns(Task.FromResult<RlmDocument?>(RlmDocumentBuilder.Default().Build()));

        CompositeDocumentReader compositeReader = new(reader);

        // Act
        await compositeReader.ReadAsync("/test/file.txt", cts.Token);

        // Assert
        await reader.Received(1).ReadAsync(Arg.Any<Uri>(), cts.Token);
    }

    public TestContext TestContext { get; set; }
}
