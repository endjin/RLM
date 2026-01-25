using Rlm.Cli.Commands;
using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Rlm.Cli.Tests.Builders;
using Shouldly;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using NSubstitute;

namespace Rlm.Cli.Tests.Commands;

[TestClass]
public sealed class OutputFormatTests
{
    private TestConsole console = null!;
    private ISessionStore sessionStore = null!;
    private NextCommand nextCommand = null!;

    [TestInitialize]
    public void Setup()
    {
        console = new TestConsole();
        sessionStore = Substitute.For<ISessionStore>();
        nextCommand = new NextCommand(console, sessionStore);
    }

    [TestMethod]
    public async Task NextCommand_Raw_OutputsOnlyContent()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Some content")
            .WithCurrentChunkIndex(-1) // Start before first chunk
            .Build();
        
        session.ChunkBuffer = [
            new ContentChunk { 
                Content = "Raw Content", 
                Index = 0, 
                StartPosition = 0, 
                EndPosition = 11,
                Metadata = []
            }
        ];
        
        sessionStore.LoadAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(session));

        // Act
        using StringWriter sw = new StringWriter();
        TextWriter originalOut = Console.Out;
        Console.SetOut(sw);
        
        try 
        {
            await nextCommand.ExecuteAsync(CreateContext(), 
                new NextCommand.Settings { Raw = true }, 
                CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert
        string output = sw.ToString().Trim();
        output.ShouldBe("Raw Content");
        
        // Should NOT contain table borders or metadata headers
        output.ShouldNotContain("Index");
        output.ShouldNotContain("Position");
        output.ShouldNotContain("Length");
        output.ShouldNotContain("─"); // Table border char
    }

    private static CommandContext CreateContext()
    {
        return new CommandContext([], new MockIRemainingArguments(), "next", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }
}
