// <copyright file="LoadCommandTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using NSubstitute;
using Rlm.Cli.Commands;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Shouldly;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Spectre.IO.Testing;

namespace Rlm.Cli.Tests.Commands;

[TestClass]
public sealed class LoadCommandTests
{
    private TestConsole console = null!;
    private FakeFileSystem fileSystem = null!;
    private ISessionStore sessionStore = null!;
    private LoadCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        console = new();
        FakeEnvironment environment = FakeEnvironment.CreateLinuxEnvironment();
        fileSystem = new(environment);
        sessionStore = Substitute.For<ISessionStore>();

        command = new(console, fileSystem, sessionStore);
    }

    [TestMethod]
    public async Task ExecuteAsync_FileDoesNotExist_ReturnsErrorCode()
    {
        // Arrange - file doesn't exist in fake file system
        LoadCommand.Settings settings = new() { Source = "/nonexistent/file.txt" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("Error");
        console.Output.ShouldContain("Cannot read source");
    }

    [TestMethod]
    public async Task ExecuteAsync_ValidFile_LoadsDocument()
    {
        // Arrange
        const string content = "Test file content\nLine 2\nLine 3";
        fileSystem.CreateFile("/test/file.txt").SetTextContent(content);

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RlmSession()));

        LoadCommand.Settings settings = new() { Source = "/test/file.txt" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Document loaded successfully");
        await sessionStore.Received(1).SaveAsync(Arg.Any<RlmSession>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_ValidFile_DisplaysMetadataTable()
    {
        // Arrange
        const string content = "Test content";
        fileSystem.CreateFile("/test/file.txt").SetTextContent(content);

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new RlmSession()));

        LoadCommand.Settings settings = new() { Source = "/test/file.txt" };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Source");
        console.Output.ShouldContain("Length");
        console.Output.ShouldContain("Tokens");
        console.Output.ShouldContain("Lines");
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyFile_LoadsDocument()
    {
        // Arrange - empty file should still be loadable
        fileSystem.CreateFile("/test/empty.txt").SetTextContent("");

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new RlmSession()));

        LoadCommand.Settings settings = new() { Source = "/test/empty.txt" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Document loaded successfully");
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new([], remaining, "load", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}