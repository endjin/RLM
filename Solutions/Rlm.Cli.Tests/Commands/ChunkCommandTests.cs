// <copyright file="ChunkCommandTests.cs" company="Endjin Limited">
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
public sealed class ChunkCommandTests
{
    private TestConsole console = null!;
    private IFileSystem fileSystem = null!;
    private ISessionStore sessionStore = null!;
    private ChunkCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        console = new TestConsole();
        fileSystem = Substitute.For<IFileSystem>();
        sessionStore = Substitute.For<ISessionStore>();
        command = new ChunkCommand(console, sessionStore);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoDocument_ReturnsErrorCode()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RlmSession()));

        ChunkCommand.Settings settings = new() { Strategy = "uniform" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("Error");
        console.Output.ShouldContain("No document loaded");
    }

    [TestMethod]
    public async Task ExecuteAsync_FilterWithoutPattern_ReturnsErrorCode()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedDocument().Build();
        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        ChunkCommand.Settings settings = new() { Strategy = "filter", Pattern = null };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("Error");
        console.Output.ShouldContain("pattern");
    }

    [TestMethod]
    public async Task ExecuteAsync_UniformStrategy_CreatesChunks()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent(new string('x', 150))
            .WithMetadata(m => m.WithTotalLength(150).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        ChunkCommand.Settings settings = new() { Strategy = "uniform", Size = 100 };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Created");
        console.Output.ShouldContain("chunk");
        await sessionStore.Received(1).SaveAsync(Arg.Any<RlmSession>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ExecuteAsync_FilterStrategy_WithMatches_CreatesChunks()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Email: test@example.com is here")
            .WithMetadata(m => m.WithTotalLength(31).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        ChunkCommand.Settings settings = new()
        {
            Strategy = "filter",
            Pattern = @"[\w.-]+@[\w.-]+\.\w+",
            Context = 10
        };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Created");
    }

    [TestMethod]
    public async Task ExecuteAsync_SemanticStrategy_CreatesChunks()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("# Header\n\nContent here\n\n## Sub Header\n\nMore content")
            .WithMetadata(m => m.WithTotalLength(50).WithLineCount(7))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        ChunkCommand.Settings settings = new() { Strategy = "semantic" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Created");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoChunksCreated_ReturnsSuccessWithWarning()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("No emails here")
            .WithMetadata(m => m.WithTotalLength(14).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        ChunkCommand.Settings settings = new()
        {
            Strategy = "filter",
            Pattern = @"[\w.-]+@[\w.-]+\.\w+"
        };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("No chunks created");
    }

    [TestMethod]
    public async Task ExecuteAsync_UpdatesSessionChunkBuffer()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Test content")
            .WithMetadata(m => m.WithTotalLength(12).WithLineCount(1))
            .Build();

        RlmSession? savedSession = null;
        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));
        sessionStore.SaveAsync(Arg.Do<RlmSession>(s => savedSession = s), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        ChunkCommand.Settings settings = new() { Strategy = "uniform", Size = 100 };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        savedSession.ShouldNotBeNull();
        savedSession.ChunkBuffer.Count.ShouldBeGreaterThan(0);
        savedSession.CurrentChunkIndex.ShouldBe(0);
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenStrategy_CreatesChunks()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("This is a test document with some content for tokenization.")
            .WithMetadata(m => m.WithTotalLength(60).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        ChunkCommand.Settings settings = new() { Strategy = "token", MaxTokens = 512 };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Created");
        console.Output.ShouldContain("token");
    }

    [TestMethod]
    public async Task ExecuteAsync_RecursiveStrategy_CreatesChunks()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("## Section 1\nContent here\n\n## Section 2\nMore content")
            .WithMetadata(m => m.WithTotalLength(55).WithLineCount(5))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        ChunkCommand.Settings settings = new() { Strategy = "recursive", Size = 100 };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Created");
        console.Output.ShouldContain("recursive");
    }

    [TestMethod]
    public async Task ExecuteAsync_AutoStrategy_SelectsFilterForNeedleQuery()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Test content with needle here")
            .WithMetadata(m => m.WithTotalLength(30).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        ChunkCommand.Settings settings = new()
        {
            Strategy = "auto",
            Query = "find the needle",
            Pattern = "needle"
        };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Auto-selected strategy");
        console.Output.ShouldContain("filter");
    }

    [TestMethod]
    public async Task ExecuteAsync_AutoStrategy_SelectsSemanticForStructuredContent()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("# Header\nContent\n\n## Section\nMore content")
            .WithMetadata(m => m.WithTotalLength(45).WithLineCount(5))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        ChunkCommand.Settings settings = new()
        {
            Strategy = "auto",
            Query = "compare sections"
        };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Auto-selected strategy");
        console.Output.ShouldContain("semantic");
    }

    [TestMethod]
    public async Task ExecuteAsync_AutoStrategy_SelectsTokenForAggregation()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("# Header\nContent here to summarize all items")
            .WithMetadata(m => m.WithTotalLength(45).WithLineCount(2))
            .Build();

        sessionStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        ChunkCommand.Settings settings = new()
        {
            Strategy = "auto",
            Query = "summarize all content"
        };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Auto-selected strategy");
        console.Output.ShouldContain("token");
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new CommandContext([], remaining, "chunk", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}