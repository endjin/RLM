// <copyright file="SessionStoreTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Rlm.Cli.Tests.Builders;
using Shouldly;
using Spectre.IO;
using Path = System.IO.Path;
using File = System.IO.File;
using Environment = System.Environment;

namespace Rlm.Cli.Tests.Infrastructure;

/// <summary>
/// Tests for SessionStore that use the actual file system.
/// These tests are non-parallelizable because they share the session file in the home directory.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class SessionStoreTests
{
    private static readonly string SessionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".rlm-session.json");

    private IFileSystem fileSystem = null!;
    private SessionStore sessionStore = null!;

    [TestInitialize]
    public void Setup()
    {
        // Ensure clean state before each test
        if (File.Exists(SessionPath))
        {
            File.Delete(SessionPath);
        }

        fileSystem = new FileSystem();
        sessionStore = new SessionStore(fileSystem);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Clean up after each test
        if (File.Exists(SessionPath))
        {
            File.Delete(SessionPath);
        }
    }

    [TestMethod]
    public async Task LoadAsync_FileDoesNotExist_ReturnsNewSession()
    {
        // Arrange - session file already deleted in Setup

        // Act
        RlmSession session = await sessionStore.LoadAsync(TestContext.CancellationToken);

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
        await sessionStore.SaveAsync(session, TestContext.CancellationToken);
        RlmSession loadedSession = await sessionStore.LoadAsync(TestContext.CancellationToken);

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
        File.WriteAllText(SessionPath, "{}");

        // Act
        sessionStore.Delete();

        // Assert
        File.Exists(SessionPath).ShouldBeFalse();
    }

    [TestMethod]
    public async Task LoadAsync_CorruptedJsonFile_ReturnsNewSession()
    {
        // Arrange
        await File.WriteAllTextAsync(SessionPath, "{ invalid json }}}", TestContext.CancellationToken);

        // Act
        RlmSession session = await sessionStore.LoadAsync(TestContext.CancellationToken);

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
        await sessionStore.SaveAsync(session, TestContext.CancellationToken);
        RlmSession loadedSession = await sessionStore.LoadAsync(TestContext.CancellationToken);

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
        await sessionStore.SaveAsync(session, TestContext.CancellationToken);
        RlmSession loadedSession = await sessionStore.LoadAsync(TestContext.CancellationToken);

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
        await sessionStore.SaveAsync(session1, TestContext.CancellationToken);
        await sessionStore.SaveAsync(session2, TestContext.CancellationToken);
        await sessionStore.SaveAsync(session3, TestContext.CancellationToken);

        RlmSession loadedSession = await sessionStore.LoadAsync(TestContext.CancellationToken);

        // Assert - Last save should win
        loadedSession.Results.ShouldContainKey("key3");
        loadedSession.Results.Count.ShouldBe(1);
    }

    public TestContext TestContext { get; set; } = null!;
}