// <copyright file="NextCommandTests.cs" company="Endjin Limited">
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
public sealed class NextCommandTests
{
    private TestConsole console = null!;
    private IFileSystem fileSystem = null!;
    private ISessionStore sessionStore = null!;
    private NextCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        this.console = new TestConsole();
        this.fileSystem = Substitute.For<IFileSystem>();
        this.sessionStore = Substitute.For<ISessionStore>();
        this.command = new NextCommand(console, sessionStore);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoChunks_ReturnsErrorCode()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new RlmSession()));

        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, new NextCommand.Settings(), CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("Error");
        console.Output.ShouldContain("No chunks available");
    }

    [TestMethod]
    public async Task ExecuteAsync_HasMoreChunks_AdvancesToNextChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3)
            .WithCurrentChunkIndex(0)
            .Build();

        RlmSession? savedSession = null;
        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));
        sessionStore.SaveAsync(Arg.Do<RlmSession>(s => savedSession = s), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, new NextCommand.Settings(), CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        savedSession.ShouldNotBeNull();
        savedSession.CurrentChunkIndex.ShouldBe(1);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtLastChunk_ReturnsSuccessWithMessage()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3)
            .WithCurrentChunkIndex(2) // Last index
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, new NextCommand.Settings(), CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("No more chunks");
        console.Output.ShouldContain("Total chunks: 3");
    }

    [TestMethod]
    public async Task ExecuteAsync_DisplaysChunkInfo()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3)
            .WithCurrentChunkIndex(0)
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new NextCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Index");
        console.Output.ShouldContain("Position");
        console.Output.ShouldContain("Length");
        console.Output.ShouldContain("Tokens");
    }

    [TestMethod]
    public async Task ExecuteAsync_ShowsRemainingChunks()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5)
            .WithCurrentChunkIndex(0)
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new NextCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Remaining");
    }

    [TestMethod]
    public async Task ExecuteAsync_AtLastChunk_ShowsStoredResultsCount()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3)
            .WithCurrentChunkIndex(2)
            .WithResult("key1", "value1")
            .WithResult("key2", "value2")
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new NextCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Stored results: 2");
    }

    [TestMethod]
    public async Task ExecuteAsync_SuggestsAggregateCommand()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3)
            .WithCurrentChunkIndex(2)
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new NextCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("aggregate");
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new CommandContext([], remaining, "next", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}