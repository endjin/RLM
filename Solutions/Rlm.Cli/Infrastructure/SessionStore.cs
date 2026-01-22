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
public sealed class SessionStore(IFileSystem fileSystem) : ISessionStore
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

    private FilePath GetSessionPath()
    {
        string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        return new DirectoryPath(home).CombineWithFilePath(SessionFileName);
    }

    /// <summary>
    /// Loads the current session from disk, or creates a new one if none exists.
    /// </summary>
    public async Task<RlmSession> LoadAsync(CancellationToken cancellationToken = default)
    {
        FilePath path = GetSessionPath();

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
    public async Task SaveAsync(RlmSession session, CancellationToken cancellationToken = default)
    {
        FilePath path = GetSessionPath();
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
    public void Delete()
    {
        FilePath path = GetSessionPath();
        if (fileSystem.File.Exists(path))
        {
            RetryPipeline.Execute(() =>
            {
                fileSystem.File.Delete(path);
            });
        }
    }
}