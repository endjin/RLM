// <copyright file="AggregateCommandTests.cs" company="Endjin Limited">
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
public sealed class AggregateCommandTests
{
    private TestConsole console = null!;
    private IFileSystem fileSystem = null!;
    private ISessionStore sessionStore = null!;
    private AggregateCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        console = new();
        fileSystem = Substitute.For<IFileSystem>();
        sessionStore = Substitute.For<ISessionStore>();
        command = new(console, sessionStore);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoResults_ReturnsSuccessWithMessage()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RlmSession()));

        AggregateCommand.Settings settings = new();
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("No results to aggregate");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithResults_CombinesResults()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("chunk_0", "First result")
            .WithResult("chunk_1", "Second result")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        AggregateCommand.Settings settings = new();
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("First result");
        console.Output.ShouldContain("Second result");
    }

    [TestMethod]
    public async Task ExecuteAsync_DefaultSeparator_UsesDoubleDash()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("a", "Result A")
            .WithResult("b", "Result B")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        AggregateCommand.Settings settings = new();
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("---");
    }

    [TestMethod]
    public async Task ExecuteAsync_CustomSeparator_UsesSeparator()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("a", "Result A")
            .WithResult("b", "Result B")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        AggregateCommand.Settings settings = new() { Separator = "|||CUSTOM|||" };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("|||CUSTOM|||");
    }

    [TestMethod]
    public async Task ExecuteAsync_DisplaysAggregationCount()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("a", "1")
            .WithResult("b", "2")
            .WithResult("c", "3")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        AggregateCommand.Settings settings = new();
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Aggregating 3 results");
    }

    [TestMethod]
    public async Task ExecuteAsync_IncludesKeysInOutput()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("my_key", "My value")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        AggregateCommand.Settings settings = new();
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("[my_key]");
    }

    [TestMethod]
    public async Task ExecuteAsync_SuggestsStoreCommand_WhenNoResults()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RlmSession()));

        AggregateCommand.Settings settings = new();
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("rlm store");
    }

    [TestMethod]
    public async Task ExecuteAsync_JsonOutput_ReturnsValidJson()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("chunk_0", "First result")
            .WithResult("chunk_1", "Second result")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        AggregateCommand.Settings settings = new() { Json = true };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("\"resultCount\"");
        console.Output.ShouldContain("\"combined\"");
    }

    [TestMethod]
    public async Task ExecuteAsync_JsonOutput_NoResults_ReturnsErrorJson()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RlmSession()));

        AggregateCommand.Settings settings = new() { Json = true };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("\"error\"");
    }

    [TestMethod]
    public async Task ExecuteAsync_FinalSignal_WrapsFinalOutput()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("key", "value")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        AggregateCommand.Settings settings = new() { Final = true };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("FINAL(");
    }

    [TestMethod]
    public async Task ExecuteAsync_JsonWithFinal_IncludesFinalSignal()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("key", "value")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        AggregateCommand.Settings settings = new() { Json = true, Final = true };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("\"signal\": \"FINAL\"");
    }

    [TestMethod]
    public async Task ExecuteAsync_JsonWithoutFinal_IncludesPartialSignal()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("key", "value")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        AggregateCommand.Settings settings = new() { Json = true, Final = false };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("\"signal\": \"PARTIAL\"");
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new([], remaining, "aggregate", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}