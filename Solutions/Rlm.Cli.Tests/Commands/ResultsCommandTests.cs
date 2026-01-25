// <copyright file="ResultsCommandTests.cs" company="Endjin Limited">
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
public sealed class ResultsCommandTests
{
    private TestConsole console = null!;
    private IFileSystem fileSystem = null!;
    private ISessionStore sessionStore = null!;
    private ResultsCommand command = null!;

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
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new RlmSession()));

        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, new(), CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("No results stored");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithResults_DisplaysResultsCount()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("a", "1")
            .WithResult("b", "2")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Stored results: 2");
    }

    [TestMethod]
    public async Task ExecuteAsync_DisplaysTable()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("key1", "value1")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Key");
        console.Output.ShouldContain("Value");
    }

    [TestMethod]
    public async Task ExecuteAsync_TruncatesLongValues()
    {
        // Arrange
        string longValue = new('x', 200);
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("key", longValue)
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("...");
        // Value should be truncated (not showing the full 200-char string)
        console.Output.ShouldNotContain(longValue);
    }

    [TestMethod]
    public async Task ExecuteAsync_OrdersResultsByKey()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("z_key", "z value")
            .WithResult("a_key", "a value")
            .WithResult("m_key", "m value")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new(), CancellationToken.None);

        // Assert
        string output = console.Output;
        int aIndex = output.IndexOf("a_key", StringComparison.Ordinal);
        int mIndex = output.IndexOf("m_key", StringComparison.Ordinal);
        int zIndex = output.IndexOf("z_key", StringComparison.Ordinal);

        aIndex.ShouldBeLessThan(mIndex);
        mIndex.ShouldBeLessThan(zIndex);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuggestsStoreCommand_WhenNoResults()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new RlmSession()));

        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, new(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("rlm store");
    }

    [TestMethod]
    public async Task ExecuteAsync_EscapesMarkupInValues()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("key", "[bold]This has markup[/]")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        CommandContext context = CreateCommandContext();

        // Act
        // Should not throw due to unescaped markup
        await command.ExecuteAsync(context, new(), CancellationToken.None);

        // Assert
        console.Output.ShouldContain("bold");
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new([], remaining, "results", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}