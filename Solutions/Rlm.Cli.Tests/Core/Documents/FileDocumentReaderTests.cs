// <copyright file="FileDocumentReaderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Documents;
using Shouldly;
using Spectre.IO.Testing;

namespace Rlm.Cli.Tests.Core.Documents;

[TestClass]
public sealed class FileDocumentReaderTests
{
    private FakeFileSystem fileSystem = null!;
    private FileDocumentReader reader = null!;

    [TestInitialize]
    public void Setup()
    {
        FakeEnvironment environment = FakeEnvironment.CreateLinuxEnvironment();
        fileSystem = new(environment);
        reader = new(fileSystem);
    }

    [TestMethod]
    public void CanRead_FileExists_ReturnsTrue()
    {
        // Arrange
        fileSystem.CreateFile("/test/file.txt").SetTextContent("content");

        // Act
        bool result = reader.CanRead("/test/file.txt");

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void CanRead_FileDoesNotExist_ReturnsFalse()
    {
        // Arrange - file not created

        // Act
        bool result = reader.CanRead("/test/nonexistent.txt");

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public async Task ReadAsync_FileDoesNotExist_ReturnsNull()
    {
        // Arrange - file not created

        // Act
        RlmDocument? result = await reader.ReadAsync("/test/nonexistent.txt", TestContext.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public async Task ReadAsync_FileExists_ReturnsDocument()
    {
        // Arrange
        const string content = "Line 1\nLine 2\nLine 3";
        fileSystem.CreateFile("/test/document.txt").SetTextContent(content);

        // Act
        RlmDocument? result = await reader.ReadAsync("/test/document.txt", TestContext.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Content.ShouldBe(content);
        result.Id.ShouldBe("document.txt");
        result.Metadata.Source.ShouldBe("file:///test/document.txt");
        result.Metadata.TotalLength.ShouldBe(content.Length);
        result.Metadata.LineCount.ShouldBe(3);
        result.Metadata.TokenEstimate.ShouldBe(content.Length / 4);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyFile_ReturnsDocumentWithEmptyContent()
    {
        // Arrange
        fileSystem.CreateFile("/test/empty.txt").SetTextContent("");

        // Act
        RlmDocument? result = await reader.ReadAsync("/test/empty.txt", TestContext.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Content.ShouldBe("");
        result.Metadata.TotalLength.ShouldBe(0);
        result.Metadata.LineCount.ShouldBe(1); // Empty string split by \n gives 1
    }

    [TestMethod]
    public async Task ReadAsync_LargeFile_CalculatesTokenEstimateCorrectly()
    {
        // Arrange
        string content = new('x', 4000);
        fileSystem.CreateFile("/test/large.txt").SetTextContent(content);

        // Act
        RlmDocument? result = await reader.ReadAsync("/test/large.txt", TestContext.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.TotalLength.ShouldBe(4000);
        result.Metadata.TokenEstimate.ShouldBe(1000); // 4000 / 4
    }

    public TestContext TestContext { get; set; }
}