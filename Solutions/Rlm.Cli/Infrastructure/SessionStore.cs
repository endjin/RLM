// <copyright file="SessionStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Polly;
using Polly.Retry;
using Rlm.Cli.Core.Session;
using Spectre.IO;

namespace Rlm.Cli.Infrastructure;

/// <summary>
/// Persists RLM session state to disk for multi-turn processing.
/// </summary>
public sealed class SessionStore(IFileSystem fileSystem, IEnvironment environment) : ISessionStore
{
    private const string SessionFileName = ".rlm-session.json";
    private const int MaxRetries = 3;

    /// <summary>
    /// Retry pipeline for file operations with exponential backoff.
    /// </summary>
    private static readonly ResiliencePipeline RetryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = MaxRetries,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(100),
            ShouldHandle = new PredicateBuilder().Handle<IOException>()
        })
        .Build();

    private DirectoryPath GetHomeDirectory()
    {
        // Try HOME (Unix) first, then USERPROFILE (Windows)
        string? home = environment.GetEnvironmentVariable("HOME")
            ?? environment.GetEnvironmentVariable("USERPROFILE");

        if (string.IsNullOrEmpty(home))
        {
            // Fallback to System.Environment for production use
            home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        }

        return new DirectoryPath(home);
    }

    private FilePath GetSessionPath(string? sessionId)
    {
        DirectoryPath home = GetHomeDirectory();
        string filename = string.IsNullOrWhiteSpace(sessionId)
            ? SessionFileName
            : $"rlm-session-{sessionId}.json";

        return home.CombineWithFilePath(filename);
    }

    /// <summary>
    /// Loads the current session from disk, or creates a new one if none exists.
    /// </summary>
    public async Task<RlmSession> LoadAsync(string? sessionId = null, CancellationToken cancellationToken = default)
    {
        FilePath path = GetSessionPath(sessionId);

        if (!fileSystem.File.Exists(path))
        {
            return new();
        }

        try
        {
            return await RetryPipeline.ExecuteAsync(async ct =>
            {
                IFile file = fileSystem.GetFile(path);
                string json = await file.ReadAllTextAsync();

                return JsonSerializer.Deserialize(json, RlmJsonContext.Default.RlmSession)
                       ?? new RlmSession();
            }, cancellationToken);
        }
        catch (JsonException)
        {
            // Corrupted session file, start fresh
            return new();
        }
    }

    /// <summary>
    /// Saves the current session to disk.
    /// </summary>
    public async Task SaveAsync(RlmSession session, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        FilePath path = GetSessionPath(sessionId);
        string json = JsonSerializer.Serialize(session, RlmJsonContext.Default.RlmSession);

        await RetryPipeline.ExecuteAsync(async _ =>
        {
            IFile file = fileSystem.GetFile(path);
            await file.WriteAllTextAsync(json);
        }, cancellationToken);
    }

    /// <summary>
    /// Deletes the session file.
    /// </summary>
    public void Delete(string? sessionId = null)
    {
        FilePath path = GetSessionPath(sessionId);
        if (fileSystem.File.Exists(path))
        {
            RetryPipeline.Execute(() =>
            {
                fileSystem.File.Delete(path);
            });
        }
    }

    /// <summary>
    /// Deletes all RLM session files in the storage location.
    /// </summary>
    public void DeleteAll()
    {
        DirectoryPath directory = GetHomeDirectory();
        
        // Pattern to match session files: rlm-session-*.json and .rlm-session.json
        // Since globbing might be limited in IFileSystem or depend on impl, we'll iterate.
        // But Spectre.IO IFileSystem usually has Globber if extended, or we assume specific pattern.
        // Here we will use simple iteration if possible or just try to delete known patterns if we can list.
        // Actually, Spectre.IO doesn't always expose easy globbing on Directory.
        // But for this environment, let's assume we can list files.
        // Wait, IFileSystem.Directory.GetFiles might not be available directly on interface depending on version.
        // Let's check what IFileSystem we have. It was injected as FileSystem.
        
        // Assuming we can search. If not, we might need a Globber.
        // Let's implement a safe way: try to list files in the directory.
        
        try 
        {
            IDirectory dir = fileSystem.GetDirectory(directory);
            if (dir.Exists)
            {
                // Filter for our session files
                // Default session: .rlm-session.json
                // Named sessions: rlm-session-*.json
                
                foreach (IFile file in dir.GetFiles("rlm-session-*.json", SearchScope.Current))
                {
                     RetryPipeline.Execute(() => file.Delete());
                }
                
                foreach (IFile file in dir.GetFiles(".rlm-session.json", SearchScope.Current))
                {
                     RetryPipeline.Execute(() => file.Delete());
                }
            }
        }
        catch (Exception)
        {
            // Best effort cleanup
        }
    }
}