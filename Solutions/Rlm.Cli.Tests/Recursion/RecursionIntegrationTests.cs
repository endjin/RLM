using Rlm.Cli.Commands;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Shouldly;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Spectre.IO.Testing;
using Spectre.IO;

namespace Rlm.Cli.Tests.Recursion;

[TestClass]
public sealed class RecursionIntegrationTests
{
    private FakeEnvironment environment = null!;
    private FakeFileSystem fileSystem = null!;
    private SessionStore sessionStore = null!;
    private TestConsole console = null!;

    [TestInitialize]
    public void Setup()
    {
        environment = FakeEnvironment.CreateLinuxEnvironment();
        environment.SetWorkingDirectory("/home/user");
        environment.SetEnvironmentVariable("HOME", "/home/user");
        fileSystem = new FakeFileSystem(environment);
        fileSystem.Directory.Create("/home/user");

        sessionStore = new SessionStore(fileSystem, environment);
        console = new TestConsole();
    }

    [TestMethod]
    public async Task ParallelSessions_DoNotInterfere()
    {
        // Arrange
        LoadCommand loadCommand = new(console, fileSystem, sessionStore);
        ChunkCommand chunkCommand = new(console, sessionStore);

        // Create dummy files
        fileSystem.CreateFile("/parent.txt").SetTextContent("Parent Content");
        fileSystem.CreateFile("/child.txt").SetTextContent("Child Content");

        // Act 1: Load Parent
        await loadCommand.ExecuteAsync(CreateContext("load"), 
            new LoadCommand.Settings { Source = "/parent.txt", SessionId = "parent" }, 
            CancellationToken.None);

        // Act 2: Load Child
        await loadCommand.ExecuteAsync(CreateContext("load"), 
            new LoadCommand.Settings { Source = "/child.txt", SessionId = "child" }, 
            CancellationToken.None);

        // Assert 1: Verify Parent State
        RlmSession parentSession = await sessionStore.LoadAsync("parent", TestContext.CancellationToken);
        parentSession.Content.ShouldBe("Parent Content");

        // Assert 2: Verify Child State
        RlmSession childSession = await sessionStore.LoadAsync("child", TestContext.CancellationToken);
        childSession.Content.ShouldBe("Child Content");

        // Act 3: Chunk Child
        await chunkCommand.ExecuteAsync(CreateContext("chunk"),
            new ChunkCommand.Settings { Strategy = "uniform", Size = 5, SessionId = "child" },
            CancellationToken.None);

        // Assert 3: Verify Child has chunks, Parent does not
        childSession = await sessionStore.LoadAsync("child", TestContext.CancellationToken);
        childSession.ChunkBuffer.ShouldNotBeEmpty();

        parentSession = await sessionStore.LoadAsync("parent", TestContext.CancellationToken);
        parentSession.ChunkBuffer.ShouldBeEmpty();
    }

    private static CommandContext CreateContext(string name)
    {
        return new CommandContext([], new MockIRemainingArguments(), name, null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed => Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }

    public TestContext TestContext { get; set; }
}