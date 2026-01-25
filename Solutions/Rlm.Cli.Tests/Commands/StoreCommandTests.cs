// <copyright file="StoreCommandTests.cs" company="Endjin Limited">
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
public sealed class StoreCommandTests
{
    private TestConsole console = null!;
    private IFileSystem fileSystem = null!;
    private ISessionStore sessionStore = null!;
    private StoreCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        console = new();
        fileSystem = Substitute.For<IFileSystem>();
        sessionStore = Substitute.For<ISessionStore>();
        command = new(console, sessionStore);
    }

    [TestMethod]
    public async Task ExecuteAsync_StoresResult()
    {
        // Arrange
        RlmSession session = new();
        RlmSession? savedSession = null;

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));
        sessionStore.SaveAsync(Arg.Do<RlmSession>(s => savedSession = s), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        StoreCommand.Settings settings = new() { Key = "chunk_0", Value = "Result value" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        savedSession.ShouldNotBeNull();
        savedSession.Results["chunk_0"].ShouldBe("Result value");
    }

    [TestMethod]
    public async Task ExecuteAsync_OverwritesExistingKey()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("key", "old value")
            .Build();

        RlmSession? savedSession = null;
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));
        sessionStore.SaveAsync(Arg.Do<RlmSession>(s => savedSession = s), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        StoreCommand.Settings settings = new() { Key = "key", Value = "new value" };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        savedSession.ShouldNotBeNull();
        savedSession.Results["key"].ShouldBe("new value");
    }

    [TestMethod]
    public async Task ExecuteAsync_DisplaysStoredConfirmation()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RlmSession()));

        StoreCommand.Settings settings = new() { Key = "my_key", Value = "my value" };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Stored:");
        console.Output.ShouldContain("my_key");
    }

    [TestMethod]
    public async Task ExecuteAsync_DisplaysTotalResultsCount()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithResult("existing", "value")
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        StoreCommand.Settings settings = new() { Key = "new_key", Value = "new value" };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Total results: 2");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMoreChunks_SuggestsNextCommand()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3)
            .WithCurrentChunkIndex(0)
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        StoreCommand.Settings settings = new() { Key = "key", Value = "value" };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("rlm next");
    }

    [TestMethod]
    public async Task ExecuteAsync_AtLastChunk_SuggestsAggregateCommand()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3)
            .WithCurrentChunkIndex(2) // Last chunk
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        StoreCommand.Settings settings = new() { Key = "key", Value = "value" };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("aggregate");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoChunks_NoSuggestion()
    {
        // Arrange
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RlmSession()));

        StoreCommand.Settings settings = new() { Key = "key", Value = "value" };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldNotContain("rlm next");
        console.Output.ShouldNotContain("All chunks processed");
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new([], remaining, "store", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}