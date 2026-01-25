// <copyright file="FilterCommandTests.cs" company="Endjin Limited">
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
public sealed class FilterCommandTests
{
    private TestConsole console = null!;
    private IFileSystem fileSystem = null!;
    private ISessionStore sessionStore = null!;
    private FilterCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        console = new();
        fileSystem = Substitute.For<IFileSystem>();
        sessionStore = Substitute.For<ISessionStore>();
        command = new(console, sessionStore);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoDocument_ReturnsErrorCode()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RlmSession()));

        FilterCommand.Settings settings = new() { Pattern = "test" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("Error");
        console.Output.ShouldContain("No document loaded");
    }

    [TestMethod]
    public async Task ExecuteAsync_PatternMatches_CreatesChunks()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Find keyword here and keyword there")
            .WithMetadata(m => m.WithTotalLength(35).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        FilterCommand.Settings settings = new() { Pattern = "keyword", Context = 10 };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Found");
        console.Output.ShouldContain("matching segment");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoMatches_ReturnsSuccessWithMessage()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("This text has no matches")
            .WithMetadata(m => m.WithTotalLength(24).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        FilterCommand.Settings settings = new() { Pattern = "xyz123" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("No matches found");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithContext_IncludesContextInChunks()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("before keyword after")
            .WithMetadata(m => m.WithTotalLength(20).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        FilterCommand.Settings settings = new() { Pattern = "keyword", Context = 100 };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("before");
        console.Output.ShouldContain("after");
    }

    [TestMethod]
    public async Task ExecuteAsync_UpdatesSessionWithChunks()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Find test in content")
            .WithMetadata(m => m.WithTotalLength(20).WithLineCount(1))
            .Build();

        RlmSession? savedSession = null;
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(session));
        sessionStore.SaveAsync(Arg.Do<RlmSession>(s => savedSession = s), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        FilterCommand.Settings settings = new() { Pattern = "test" };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        savedSession.ShouldNotBeNull();
        savedSession.ChunkBuffer.Count.ShouldBeGreaterThan(0);
        savedSession.CurrentChunkIndex.ShouldBe(0);
    }

    [TestMethod]
    public async Task ExecuteAsync_DisplaysChunkTable()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Email: test@example.com")
            .WithMetadata(m => m.WithTotalLength(23).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        FilterCommand.Settings settings = new() { Pattern = @"[\w.-]+@[\w.-]+\.\w+" };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Index");
        console.Output.ShouldContain("Position");
        console.Output.ShouldContain("Length");
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new([], remaining, "filter", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}