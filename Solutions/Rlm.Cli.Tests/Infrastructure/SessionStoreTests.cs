// <copyright file="SessionStoreTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Rlm.Cli.Tests.Builders;
using Shouldly;
using Spectre.IO;
using Spectre.IO.Testing;

namespace Rlm.Cli.Tests.Infrastructure;

/// <summary>
/// Tests for SessionStore that use a fake file system.
/// These tests can run in parallel since each test has its own isolated file system.
/// </summary>
[TestClass]
public sealed class SessionStoreTests
{
    private FakeEnvironment environment = null!;
    private FakeFileSystem fileSystem = null!;
    private SessionStore sessionStore = null!;

    private FilePath GetSessionPath(string? sessionId = null)
    {
        string filename = string.IsNullOrWhiteSpace(sessionId)
            ? ".rlm-session.json"
            : $"rlm-session-{sessionId}.json";

        return new DirectoryPath("/home/user").CombineWithFilePath(filename);
    }

    [TestInitialize]
    public void Setup()
    {
        environment = FakeEnvironment.CreateLinuxEnvironment();
        environment.SetWorkingDirectory("/home/user");
        environment.SetEnvironmentVariable("HOME", "/home/user");
        fileSystem = new FakeFileSystem(environment);
        fileSystem.Directory.Create("/home/user");
        sessionStore = new SessionStore(fileSystem, environment);
    }

    [TestMethod]
    public async Task SaveAsync_WithDifferentSessionIds_CreatesDifferentFiles()
    {
        // Arrange
        RlmSession sessionA = RlmSessionBuilder.Default().WithContent("Content A").Build();
        RlmSession sessionB = RlmSessionBuilder.Default().WithContent("Content B").Build();

        string sessionNameA = "test-session-a";
        string sessionNameB = "test-session-b";

        // Act
        await sessionStore.SaveAsync(sessionA, sessionNameA, TestContext.CancellationToken);
        await sessionStore.SaveAsync(sessionB, sessionNameB, TestContext.CancellationToken);

        // Assert
        fileSystem.File.Exists(GetSessionPath(sessionNameA)).ShouldBeTrue();
        fileSystem.File.Exists(GetSessionPath(sessionNameB)).ShouldBeTrue();

        RlmSession loadedA = await sessionStore.LoadAsync(sessionNameA, TestContext.CancellationToken);
        RlmSession loadedB = await sessionStore.LoadAsync(sessionNameB, TestContext.CancellationToken);

        loadedA.Content.ShouldBe("Content A");
        loadedB.Content.ShouldBe("Content B");
    }

    [TestMethod]
    public async Task DeleteAll_DeletesMultipleSessions()
    {
        // Arrange
        await sessionStore.SaveAsync(new RlmSession(), "test-del-1", TestContext.CancellationToken);
        await sessionStore.SaveAsync(new RlmSession(), "test-del-2", TestContext.CancellationToken);
        await sessionStore.SaveAsync(new RlmSession(), cancellationToken: TestContext.CancellationToken); // Default

        fileSystem.File.Exists(GetSessionPath("test-del-1")).ShouldBeTrue();
        fileSystem.File.Exists(GetSessionPath("test-del-2")).ShouldBeTrue();
        fileSystem.File.Exists(GetSessionPath()).ShouldBeTrue();

        // Act
        sessionStore.DeleteAll();

        // Assert
        fileSystem.File.Exists(GetSessionPath("test-del-1")).ShouldBeFalse();
        fileSystem.File.Exists(GetSessionPath("test-del-2")).ShouldBeFalse();
        fileSystem.File.Exists(GetSessionPath()).ShouldBeFalse();
    }

    [TestMethod]
    public async Task LoadAsync_FileDoesNotExist_ReturnsNewSession()
    {
        // Arrange - session file already deleted in Setup

        // Act
        RlmSession session = await sessionStore.LoadAsync(null, TestContext.CancellationToken);

        // Assert
        session.ShouldNotBeNull();
        session.Content.ShouldBeNull();
        session.Metadata.ShouldBeNull();
        session.ChunkBuffer.ShouldBeEmpty();
        session.Results.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task SaveAsync_ThenLoadAsync_PreservesSession()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Test content")
            .WithMetadata(m => m.WithSource("/test/file.txt").WithTotalLength(12).WithLineCount(1))
            .WithResult("key1", "value1")
            .Build();

        // Act
        await sessionStore.SaveAsync(session, null, TestContext.CancellationToken);
        RlmSession loadedSession = await sessionStore.LoadAsync(null, TestContext.CancellationToken);

        // Assert
        loadedSession.Content.ShouldBe("Test content");
        loadedSession.Metadata.ShouldNotBeNull();
        loadedSession.Metadata!.Source.ShouldBe("/test/file.txt");
        loadedSession.Results["key1"].ShouldBe("value1");
    }

    [TestMethod]
    public void Delete_RemovesSessionFile()
    {
        // Arrange
        FilePath path = GetSessionPath();
        fileSystem.CreateFile(path).SetTextContent("{}");

        // Act
        sessionStore.Delete();

        // Assert
        fileSystem.File.Exists(path).ShouldBeFalse();
    }

    [TestMethod]
    public void Delete_NamedSession_RemovesSpecificFile()
    {
        // Arrange
        string name = "test-delete-named";
        FilePath path = GetSessionPath(name);
        fileSystem.CreateFile(path).SetTextContent("{}");

        // Act
        sessionStore.Delete(name);

        // Assert
        fileSystem.File.Exists(path).ShouldBeFalse();
    }

    [TestMethod]
    public async Task LoadAsync_CorruptedJsonFile_ReturnsNewSession()
    {
        // Arrange
        FilePath path = GetSessionPath();
        fileSystem.CreateFile(path).SetTextContent("{ invalid json }}}");

        // Act
        RlmSession session = await sessionStore.LoadAsync(sessionId: null, TestContext.CancellationToken);

        // Assert
        session.ShouldNotBeNull();
        session.Content.ShouldBeNull(); // Returns fresh session on corrupted file
    }

    [TestMethod]
    public async Task SaveAsync_WithChunks_SerializesCorrectly()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.WithLoadedChunks(3).Build();

        // Act
        await sessionStore.SaveAsync(session, null, TestContext.CancellationToken);
        RlmSession loadedSession = await sessionStore.LoadAsync(null, TestContext.CancellationToken);

        // Assert
        loadedSession.ChunkBuffer.Count.ShouldBe(3);
    }

    [TestMethod]
    public void Delete_FileDoesNotExist_DoesNotThrow()
    {
        // Arrange - session file already deleted in Setup

        // Act & Assert - should not throw
        Should.NotThrow(() => sessionStore.Delete());
    }

    [TestMethod]
    public async Task SaveAsync_WithRecursionDepth_PreservesRecursionDepth()
    {
        // Arrange
        RlmSession session = RlmSessionBuilder.Default()
            .WithContent("Test content")
            .Build();
        session.RecursionDepth = 3;

        // Act
        await sessionStore.SaveAsync(session, null, TestContext.CancellationToken);
        RlmSession loadedSession = await sessionStore.LoadAsync(null, TestContext.CancellationToken);

        // Assert
        loadedSession.RecursionDepth.ShouldBe(3);
    }

    [TestMethod]
    public async Task SaveAsync_MultipleTimes_DoesNotCorruptFile()
    {
        // Arrange - This implicitly tests that retries work for successful operations
        RlmSession session1 = RlmSessionBuilder.Default().WithResult("key1", "value1").Build();
        RlmSession session2 = RlmSessionBuilder.Default().WithResult("key2", "value2").Build();
        RlmSession session3 = RlmSessionBuilder.Default().WithResult("key3", "value3").Build();

        // Act - Rapid successive saves
        await sessionStore.SaveAsync(session1, null, TestContext.CancellationToken);
        await sessionStore.SaveAsync(session2, null, TestContext.CancellationToken);
        await sessionStore.SaveAsync(session3, null, TestContext.CancellationToken);

        RlmSession loadedSession = await sessionStore.LoadAsync(null, TestContext.CancellationToken);

        // Assert - Last save should win
        loadedSession.Results.ShouldContainKey("key3");
        loadedSession.Results.Count.ShouldBe(1);
    }

    public TestContext TestContext { get; set; } = null!;
}