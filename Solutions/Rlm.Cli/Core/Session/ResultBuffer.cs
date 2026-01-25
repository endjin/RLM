// <copyright file="ResultBuffer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Rlm.Cli.Core.Session;

/// <summary>
/// Accumulates partial results during RLM processing, following the OutputBuffer pattern from RLM spec.
/// </summary>
public sealed class ResultBuffer
{
    private readonly Dictionary<string, string> results = [];

    /// <summary>
    /// Stores a result with the given key.
    /// </summary>
    /// <param name="key">The key to identify this result (e.g., "chunk_0").</param>
    /// <param name="value">The result value.</param>
    public void Store(string key, string value) => results[key] = value;

    /// <summary>
    /// Gets a result by key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The result value, or null if not found.</returns>
    public string? Get(string key) => results.GetValueOrDefault(key);

    /// <summary>
    /// Gets all stored results.
    /// </summary>
    /// <returns>A read-only dictionary of all results.</returns>
    public IReadOnlyDictionary<string, string> GetAll() => results;

    /// <summary>
    /// Combines all results into a single string with separators.
    /// </summary>
    /// <param name="separator">Separator between results.</param>
    /// <returns>Combined string of all results.</returns>
    public string GetCombined(string separator = "\n\n---\n\n")
        => string.Join(separator, results.OrderBy(kv => kv.Key).Select(kv => $"[{kv.Key}]\n{kv.Value}"));

    /// <summary>
    /// Number of stored results.
    /// </summary>
    public int Count => results.Count;

    /// <summary>
    /// Whether the buffer has any results.
    /// </summary>
    public bool HasResults => results.Count > 0;

    /// <summary>
    /// Clears all stored results.
    /// </summary>
    public void Clear() => results.Clear();

    /// <summary>
    /// Removes a specific result.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>True if the key was found and removed.</returns>
    public bool Remove(string key) => results.Remove(key);
}