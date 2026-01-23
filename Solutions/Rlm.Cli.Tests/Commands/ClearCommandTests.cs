// <copyright file="ClearCommandTests.cs" company="Endjin Limited">
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
public sealed class ClearCommandTests
{
    private TestConsole console = null!;
    private IFileSystem fileSystem = null!;
    private ISessionStore sessionStore = null!;
    private ClearCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        console = new TestConsole();
        fileSystem = Substitute.For<IFileSystem>();
        sessionStore = Substitute.For<ISessionStore>();
        command = new ClearCommand(console, sessionStore);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptySession_ReturnsSuccessWithMessage()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RlmSession()));

        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, new(), CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Session was already empty");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithDocument_ClearsAndConfirms()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedDocument().Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, new ClearCommand.Settings(), CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Session cleared");
        console.Output.ShouldContain("Document unloaded");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithChunks_NotesChunksCleared()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3).Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new ClearCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Chunk buffer cleared");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithResults_NotesResultsRemoved()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithStoredResults().Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new ClearCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Results removed");
    }

    [TestMethod]
    public async Task ExecuteAsync_CallsSessionClear()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithStoredResults().Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new ClearCommand.Settings(), CancellationToken.None);

        // Assert
        session.Content.ShouldBeNull();
        session.Metadata.ShouldBeNull();
        session.ChunkBuffer.ShouldBeEmpty();
        session.Results.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_DeletesSessionFile()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedDocument().Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new ClearCommand.Settings(), CancellationToken.None);

        // Assert
        sessionStore.Received(1).Delete();
    }

    [TestMethod]
    public async Task ExecuteAsync_AllStateCleared_ShowsAllClearMessages()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Document content")
            .WithMetadata(m => m.WithSource("/test.txt"))
            .WithChunks(2)
            .WithResult("key", "value")
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new ClearCommand.Settings(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Session cleared");
        console.Output.ShouldContain("Document unloaded");
        console.Output.ShouldContain("Chunk buffer cleared");
        console.Output.ShouldContain("Results removed");
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new CommandContext([], remaining, "clear", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, string? (x) => null);
    }
}