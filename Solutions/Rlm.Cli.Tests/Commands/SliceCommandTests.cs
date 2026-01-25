// <copyright file="SliceCommandTests.cs" company="Endjin Limited">
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
public sealed class SliceCommandTests
{
    private TestConsole console = null!;
    private IFileSystem fileSystem = null!;
    private ISessionStore sessionStore = null!;
    private SliceCommand command = null!;

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
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new RlmSession()));

        SliceCommand.Settings settings = new() { Range = "0:10" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("Error");
        console.Output.ShouldContain("No document loaded");
    }

    [TestMethod]
    public async Task ExecuteAsync_ValidRange_ReturnsSlice()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("0123456789ABCDEF")
            .WithMetadata(m => m.WithTotalLength(16).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(session));

        SliceCommand.Settings settings = new() { Range = "0:10" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("0123456789");
    }

    [TestMethod]
    public async Task ExecuteAsync_NegativeStart_SlicesFromEnd()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("0123456789ABCDEF")
            .WithMetadata(m => m.WithTotalLength(16).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(session));

        SliceCommand.Settings settings = new() { Range = "-6:" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("ABCDEF");
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyStart_SlicesFromBeginning()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("0123456789")
            .WithMetadata(m => m.WithTotalLength(10).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(session));

        SliceCommand.Settings settings = new() { Range = ":5" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("01234");
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyEnd_SlicesToEnd()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("0123456789")
            .WithMetadata(m => m.WithTotalLength(10).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(session));

        SliceCommand.Settings settings = new() { Range = "5:" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("56789");
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidRangeFormat_ReturnsErrorCode()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("0123456789")
            .WithMetadata(m => m.WithTotalLength(10).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        SliceCommand.Settings settings = new() { Range = "invalid" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(1);
        console.Output.ShouldContain("Error");
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeExceedsContent_ClampsToContentLength()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("short")
            .WithMetadata(m => m.WithTotalLength(5).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(session));

        SliceCommand.Settings settings = new() { Range = "0:1000" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("short");
    }

    [TestMethod]
    public async Task ExecuteAsync_NegativeEnd_SlicesCorrectly()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("0123456789")
            .WithMetadata(m => m.WithTotalLength(10).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(session));

        SliceCommand.Settings settings = new() { Range = "0:-2" };
        CommandContext context = CreateCommandContext();

        // Act
        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("01234567");
    }

    [TestMethod]
    public async Task ExecuteAsync_DisplaysSliceInfo()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Test content")
            .WithMetadata(m => m.WithTotalLength(12).WithLineCount(1))
            .Build();

        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        SliceCommand.Settings settings = new() { Range = "0:5" };
        CommandContext context = CreateCommandContext();

        // Act
        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        console.Output.ShouldContain("Slice:");
        console.Output.ShouldContain("chars");
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new([], remaining, "slice", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}