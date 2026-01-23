// <copyright file="JumpCommandTests.cs" company="Endjin Limited">
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

namespace Rlm.Cli.Tests.Commands;

[TestClass]
public sealed class JumpCommandTests
{
    private TestConsole console = null!;
    private ISessionStore sessionStore = null!;
    private JumpCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        console = new TestConsole();
        sessionStore = Substitute.For<ISessionStore>();
        command = new JumpCommand(console, sessionStore);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoChunksAvailable_ReturnsErrorCode()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Empty().Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        JumpCommand.Settings settings = new() { Target = "1" };
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

        JumpCommand.Settings settings = new() { Target = "1", Json = true };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("\"error\"");
        console.Output.ShouldContain("No chunks available");
    }

    [TestMethod]
    public async Task ExecuteAsync_ValidIndex_JumpsToTargetChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5)
            .WithCurrentChunkIndex(0)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        JumpCommand.Settings settings = new() { Target = "3" }; // Jump to chunk 3 (1-based)
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Jumped from chunk 1 to 3");
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 2), // 0-based index
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_PercentageTarget_JumpsToCalculatedChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(10)
            .WithCurrentChunkIndex(0)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        JumpCommand.Settings settings = new() { Target = "50%" }; // Jump to 50%
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        // 50% of 10 chunks = chunk 5 (1-based), which is index 4 (0-based)
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 4),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidPercentageFormat_ReturnsErrorCode()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5).Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        JumpCommand.Settings settings = new() { Target = "abc%" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("Invalid percentage format");
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidIndexFormat_ReturnsErrorCode()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5).Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        JumpCommand.Settings settings = new() { Target = "abc" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("Invalid index format");
    }

    [TestMethod]
    public async Task ExecuteAsync_IndexExceedsRange_ClampsToLastChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5)
            .WithCurrentChunkIndex(0)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        JumpCommand.Settings settings = new() { Target = "100" }; // Way beyond range
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 4), // Clamped to last chunk (index 4)
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_NegativeIndex_ClampsToFirstChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5)
            .WithCurrentChunkIndex(2)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        JumpCommand.Settings settings = new() { Target = "-5" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 0), // Clamped to first chunk
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_JsonOutput_ReturnsChunkJson()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(5)
            .WithCurrentChunkIndex(0)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        JumpCommand.Settings settings = new() { Target = "3", Json = true };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("\"index\":");
        console.Output.ShouldContain("\"totalChunks\":");
        console.Output.ShouldContain("\"jumpedFrom\":");
    }

    [TestMethod]
    public async Task ExecuteAsync_ZeroPercentage_JumpsToFirstChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(10)
            .WithCurrentChunkIndex(5)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        JumpCommand.Settings settings = new() { Target = "0%" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 0),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_100Percentage_JumpsToLastChunk()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(10)
            .WithCurrentChunkIndex(0)
            .Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(session);

        JumpCommand.Settings settings = new() { Target = "100%" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        await sessionStore.Received(1).SaveAsync(
            Arg.Is<RlmSession>(s => s.CurrentChunkIndex == 9), // Last chunk
            Arg.Any<CancellationToken>());
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new CommandContext([], remaining, "jump", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}
