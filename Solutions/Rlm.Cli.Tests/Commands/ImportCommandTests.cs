// <copyright file="ImportCommandTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Commands;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Rlm.Cli.Tests.Builders;
using Shouldly;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Spectre.IO;
using Spectre.IO.Testing;

namespace Rlm.Cli.Tests.Commands;

[TestClass]
public sealed class ImportCommandTests
{
    private TestConsole console = null!;
    private FakeEnvironment environment = null!;
    private FakeFileSystem fileSystem = null!;
    private SessionStore sessionStore = null!;
    private ImportCommand command = null!;

    [TestInitialize]
    public void Setup()
    {
        console = new();
        environment = FakeEnvironment.CreateLinuxEnvironment();
        environment.SetEnvironmentVariable("HOME", "/home/user");
        fileSystem = new FakeFileSystem(environment);

        // Create both home and working directories
        fileSystem.Directory.Create("/home/user");
        fileSystem.Directory.Create("/workspaces/project");

        sessionStore = new SessionStore(fileSystem, environment);
        command = new ImportCommand(console, fileSystem, environment, sessionStore);
    }

    /// <summary>
    /// This test proves the fix works:
    /// When import pattern matches session files (rlm-session-*.json),
    /// it should search the home directory where sessions are actually stored.
    /// </summary>
    [TestMethod]
    public async Task ImportSessionFiles_FromDifferentWorkingDirectory_AfterFix_Succeeds()
    {
        // Arrange: Simulate workers creating session files in home directory
        await CreateChildSessionFile("child_0", "Finding from chunk 0");
        await CreateChildSessionFile("child_1", "Finding from chunk 1");
        await CreateChildSessionFile("child_2", "Finding from chunk 2");

        // Create parent session that will receive the imports
        RlmSession parentSession = RlmSessionBuilder.Default()
            .WithContent("Parent document content")
            .Build();
        await sessionStore.SaveAsync(parentSession, "parent", CancellationToken.None);

        // Parent is in a DIFFERENT working directory
        environment.SetWorkingDirectory("/workspaces/project");

        // Act: Import session files (after fix, should find them in ~/)
        ImportCommand.Settings settings = new()
        {
            Pattern = "rlm-session-child_*.json",
            SessionId = "parent"  // Import into parent session
        };
        CommandContext context = CreateCommandContext();

        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert: AFTER FIX - should find and import 3 files
        result.ShouldBe(0);
        console.Output.ShouldContain("Found 3 file(s)");
        console.Output.ShouldContain("Imported 3 result(s)");

        // Verify results were imported into parent session
        RlmSession updatedParent = await sessionStore.LoadAsync("parent", CancellationToken.None);
        updatedParent.Results.Count.ShouldBe(3);
        updatedParent.Results.ShouldContainKey("rlm-session-child_0");
        updatedParent.Results.ShouldContainKey("rlm-session-child_1");
        updatedParent.Results.ShouldContainKey("rlm-session-child_2");
    }

    /// <summary>
    /// Test that absolute paths still work (backwards compatibility).
    /// </summary>
    [TestMethod]
    public async Task ImportSessionFiles_WithAbsolutePath_AlwaysWorks()
    {
        // Arrange
        await CreateChildSessionFile("child_0", "Finding from chunk 0");
        environment.SetWorkingDirectory("/workspaces/project");

        // Act: Import using absolute path (should always work)
        ImportCommand.Settings settings = new()
        {
            Pattern = "/home/user/rlm-session-child_*.json"
        };
        CommandContext context = CreateCommandContext();

        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert
        result.ShouldBe(0);
        console.Output.ShouldContain("Found 1 file(s)");
    }

    /// <summary>
    /// Test that non-session file patterns still use CWD (backwards compatibility).
    /// </summary>
    [TestMethod]
    public async Task ImportNonSessionFiles_UsesWorkingDirectory()
    {
        // Arrange: Create result files in working directory
        environment.SetWorkingDirectory("/workspaces/project");
        fileSystem.CreateFile(new FilePath("/workspaces/project/result_0.txt"))
            .SetTextContent("Result content 0");
        fileSystem.CreateFile(new FilePath("/workspaces/project/result_1.txt"))
            .SetTextContent("Result content 1");

        // Act: Import non-session files (should search CWD as before)
        ImportCommand.Settings settings = new() { Pattern = "result_*.txt" };
        CommandContext context = CreateCommandContext();

        int result = await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert: Should find files in CWD
        result.ShouldBe(0);
        console.Output.ShouldContain("Found 2 file(s)");
    }

    /// <summary>
    /// Test the exact scenario from the logs: wrong pattern used first.
    /// </summary>
    [TestMethod]
    public async Task ImportSessionFiles_WrongPattern_StillFails()
    {
        // Arrange
        await CreateChildSessionFile("child_0", "Finding");
        environment.SetWorkingDirectory("/workspaces/project");

        // Act: Use wrong pattern (missing "rlm-session-" prefix)
        // This is what happened in the logs: rlm import "child_*"
        ImportCommand.Settings settings = new() { Pattern = "child_*.json" };
        CommandContext context = CreateCommandContext();

        await command.ExecuteAsync(context, settings, CancellationToken.None);

        // Assert: Wrong pattern should still fail (not a session file pattern)
        console.Output.ShouldContain("No files found");
    }

    private async Task CreateChildSessionFile(string sessionId, string resultContent)
    {
        // Simulate what rlm-worker does: create session in home directory with a result
        RlmSession childSession = RlmSessionBuilder.Default()
            .WithResult("result", resultContent)
            .Build();
        await sessionStore.SaveAsync(childSession, sessionId, CancellationToken.None);
    }

    private static CommandContext CreateCommandContext()
    {
        MockIRemainingArguments remaining = new();
        return new([], remaining, "import", null);
    }

    private sealed class MockIRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => [];
        public ILookup<string, string?> Parsed =>
            Enumerable.Empty<string>().ToLookup(x => x, x => (string?)null);
    }

    public TestContext TestContext { get; set; } = null!;
}
