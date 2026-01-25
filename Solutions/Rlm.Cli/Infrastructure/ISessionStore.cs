// <copyright file="ISessionStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Session;

namespace Rlm.Cli.Infrastructure;

/// <summary>
/// Interface for session persistence operations.
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Loads the current session from disk, or creates a new one if none exists.
    /// </summary>
    Task<RlmSession> LoadAsync(string? sessionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current session to disk.
    /// </summary>
    Task SaveAsync(RlmSession session, string? sessionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the session file.
    /// </summary>
    void Delete(string? sessionId = null);

    /// <summary>
    /// Deletes all RLM session files in the storage location.
    /// </summary>
    void DeleteAll();
}