// <copyright file="InfoCommandTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using NSubstitute;
using Rlm.Cli.Commands;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Rlm.Cli.Tests.Builders;
using Shouldly;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Spectre.IO;

namespace Rlm.Cli.Tests.Commands;

[TestClass]
public sealed class InfoCommandTests
{
    private TestConsole console = null!;
    private IFileSystem fileSystem = null!;
    private ISessionStore sessionStore = null!;
    private InfoCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        console = new TestConsole();
        fileSystem = Substitute.For<IFileSystem>();
        sessionStore = Substitute.For<ISessionStore>();
        command = new InfoCommand(console, sessionStore);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoDocument_ReturnsSuccessWithMessage()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RlmSession()));

        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, new InfoCommand.Settings(), CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("No document loaded");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithDocument_DisplaysMetadata()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Test content")
            .WithMetadata(m => m
                .WithSource("/test/file.txt")
                .WithTotalLength(1000)
                .WithLineCount(50))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new InfoCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Source");
        console.Output.ShouldContain("/test/file.txt");
        console.Output.ShouldContain("Length");
        console.Output.ShouldContain("1,000");
        console.Output.ShouldContain("Lines");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithChunks_DisplaysChunkInfo()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5)
            .WithCurrentChunkIndex(2)
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new InfoCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Chunks:");
        console.Output.ShouldContain("5 chunks");
        console.Output.ShouldContain("index 3"); // 1-based display: CurrentChunkIndex (2) + 1
        console.Output.ShouldContain("Remaining:");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithResults_DisplaysResultsSummary()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedDocument()
            .WithResult("key1", "value1")
            .WithResult("key2", "value2")
            .WithResult("key3", "value3")
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new InfoCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Stored results: 3");
        console.Output.ShouldContain("key1");
        console.Output.ShouldContain("key2");
        console.Output.ShouldContain("key3");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithManyResults_TruncatesList()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedDocument()
            .WithResult("key1", "v1")
            .WithResult("key2", "v2")
            .WithResult("key3", "v3")
            .WithResult("key4", "v4")
            .WithResult("key5", "v5")
            .WithResult("key6", "v6")
            .WithResult("key7", "v7")
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new InfoCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Stored results: 7");
        console.Output.ShouldContain("and 2 more");
    }

    [TestMethod]
    public async Task ExecuteAsync_DisplaysLoadedAtTime()
    {
        // Arrange
        DateTimeOffset loadedAt = new(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Test")
            .WithMetadata(m => m.WithLoadedAt(loadedAt))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new InfoCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Loaded at");
        console.Output.ShouldContain("2025");
    }

    [TestMethod]
    public async Task ExecuteAsync_DisplaysTokenEstimate()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Test content")
            .WithMetadata(m => m.WithTotalLength(400).WithTokenEstimate(100))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new InfoCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Tokens");
        console.Output.ShouldContain("100");
    }

    [TestMethod]
    public async Task ExecuteAsync_SuggestsLoadCommand_WhenNoDocument()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new RlmSession()));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new InfoCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("rlm load");
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new CommandContext([], remaining, "info", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}