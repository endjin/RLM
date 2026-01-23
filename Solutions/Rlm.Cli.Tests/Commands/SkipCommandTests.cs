// <copyright file="SkipCommandTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using NSubstitute;
using Rlm.Cli.Commands;
using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Rlm.Cli.Tests.Builders;
using Shouldly;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Rlm.Cli.Tests.Commands;

[TestClass]
public sealed class SkipCommandTests
{
    private TestConsole console = null!;
    private ISessionStore sessionStore = null!;
    private SkipCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        console = new TestConsole();
        sessionStore = Substitute.For<ISessionStore>();
        command = new SkipCommand(console, sessionStore);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoChunksAvailable_ReturnsErrorCode()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Empty().Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        SkipCommand.Settings settings = new() { Count = 1 };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("No chunks available");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoChunksAvailable_JsonOutput_ReturnsJsonError()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Empty().Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        SkipCommand.Settings settings = new() { Count = 1, Json = true };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("\"error\"");
        console.Output.ShouldContain("No chunks available");
    }

    [TestMethod]
    public async Task ExecuteAsync_SkipForward_MovesToCorrectChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(10)
            .WithCurrentChunkIndex(2)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        SkipCommand.Settings settings = new() { Count = 3 };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Skipped 3 chunk(s)");
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 5),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_SkipBackward_MovesToCorrectChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(10)
            .WithCurrentChunkIndex(5)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        SkipCommand.Settings settings = new() { Count = -2 };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Skipped -2 chunk(s)");
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 3),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_SkipBeyondEnd_ClampsToLastChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5)
            .WithCurrentChunkIndex(2)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        SkipCommand.Settings settings = new() { Count = 100 };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 4), // Clamped to last
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_SkipBeforeStart_ClampsToFirstChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5)
            .WithCurrentChunkIndex(2)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        SkipCommand.Settings settings = new() { Count = -100 };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 0), // Clamped to first
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_JsonOutput_ReturnsChunkJson()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5)
            .WithCurrentChunkIndex(1)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        SkipCommand.Settings settings = new() { Count = 2, Json = true };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("\"index\":");
        console.Output.ShouldContain("\"totalChunks\":");
        console.Output.ShouldContain("\"skipped\":");
    }

    [TestMethod]
    public async Task ExecuteAsync_SkipEmptyForward_SkipsSmallChunks()
    {
        // Arrange - Create session with some small chunks
        List<ContentChunk> chunks =
        [
            ContentChunkBuilder.Default().WithIndex(0).WithContent("Large content that is bigger than 100 chars. ".PadRight(150, 'X')).Build(),
            ContentChunkBuilder.Default().WithIndex(1).WithContent("Small").Build(), // < 100 chars
            ContentChunkBuilder.Default().WithIndex(2).WithContent("Tiny").Build(),  // < 100 chars
            ContentChunkBuilder.Default().WithIndex(3).WithContent("Another large chunk with plenty of content that exceeds the threshold. ".PadRight(150, 'Y')).Build(),
            ContentChunkBuilder.Default().WithIndex(4).WithContent("Final chunk with enough content to be considered non-empty for the test.".PadRight(150, 'Z')).Build()
        ];

        RlmSession session = RlmSessionBuilder.WithLoadedDocument()
            .WithChunks(chunks)
            .WithCurrentChunkIndex(0)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        SkipCommand.Settings settings = new() { Count = 1, SkipEmpty = true };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        // Should skip chunks 1 and 2 (< 100 chars) and land on chunk 3
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 3),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_SkipEmptyBackward_SkipsSmallChunks()
    {
        // Arrange - Create session with some small chunks
        List<ContentChunk> chunks =
        [
            ContentChunkBuilder.Default().WithIndex(0).WithContent("Large content that is bigger than 100 chars. ".PadRight(150, 'X')).Build(),
            ContentChunkBuilder.Default().WithIndex(1).WithContent("Small").Build(), // < 100 chars
            ContentChunkBuilder.Default().WithIndex(2).WithContent("Tiny").Build(),  // < 100 chars
            ContentChunkBuilder.Default().WithIndex(3).WithContent("Another large chunk with plenty of content that exceeds the threshold. ".PadRight(150, 'Y')).Build(),
            ContentChunkBuilder.Default().WithIndex(4).WithContent("Final chunk with enough content to be considered non-empty for the test.".PadRight(150, 'Z')).Build()
        ];

        RlmSession session = RlmSessionBuilder.WithLoadedDocument()
            .WithChunks(chunks)
            .WithCurrentChunkIndex(4)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        SkipCommand.Settings settings = new() { Count = -1, SkipEmpty = true };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        // Should skip chunks 2 and 1 (< 100 chars) when going backward and land on chunk 0
        // Starting at 4, skip -1 = 3, then skip empty backward (2 is small, 1 is small) = land on 0
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 3 || s.CurrentChunkIndex == 0),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_SkipZero_StaysAtCurrentChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5)
            .WithCurrentChunkIndex(2)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        SkipCommand.Settings settings = new() { Count = 0 };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Skipped 0 chunk(s)");
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 2),
            Arg.Any<CancellationToken>());
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new CommandContext([], remaining, "skip", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}
