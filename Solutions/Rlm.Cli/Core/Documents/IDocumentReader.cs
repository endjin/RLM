// <copyright file="IDocumentReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Spectre.IO;

namespace Rlm.Cli.Core.Documents;

/// <summary>
/// Reads documents from various sources, analogous to IngestionDocumentReader.
/// Supports Uri-based sources for extensibility (file://, future http://).
/// </summary>
public interface IDocumentReader
{
    /// <summary>
    /// Reads a document from the specified source.
    /// </summary>
    /// <param name="source">Uri identifying the source (file://, stdin://, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded document, or null if not found.</returns>
    Task<RlmDocument?> ReadAsync(Uri source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads multiple documents from a directory source with optional glob pattern.
    /// </summary>
    /// <param name="source">Uri identifying the directory source.</param>
    /// <param name="pattern">Optional glob pattern to filter files (e.g., "*.md", "**/*.txt").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of loaded documents.</returns>
    IAsyncEnumerable<RlmDocument> ReadManyAsync(
        Uri source,
        string? pattern = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this reader can handle the specified source.
    /// </summary>
    /// <param name="source">Uri to check.</param>
    /// <returns>True if the reader can handle this source.</returns>
    bool CanRead(Uri source);
}

/// <summary>
/// Extension methods for IDocumentReader to support string-based sources.
/// </summary>
public static class DocumentReaderExtensions
{
    /// <summary>
    /// Converts a string source to a Uri for document reading.
    /// Supports file paths, "-" for stdin, and explicit URIs.
    /// </summary>
    public static Uri ToSourceUri(this string source)
    {
        if (source == "-")
        {
            return new Uri("stdin://input");
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri))
        {
            // If it's already a URI, keep it (including file:// URIs).
            if (!string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase))
            {
                return uri;
            }

            return uri;
        }

        // Treat as a file path.
        // Important: do NOT blindly Path.GetFullPath() rooted POSIX paths like "/test/file.txt".
        // Unit tests use a Linux fake filesystem where those paths must remain unchanged.
        string path = source;

        if (IsWindowsDriveRelativePath(path) || !System.IO.Path.IsPathRooted(path))
        {
            path = System.IO.Path.GetFullPath(path);
        }

        return CreateFileUriFromPath(path);
    }

    /// <summary>
    /// Converts a string source to a Uri for document reading using Spectre.IO.
    /// Supports file paths, "-" for stdin, and explicit URIs.
    /// </summary>
    /// <param name="source">The source string to convert.</param>
    /// <param name="environment">The Spectre.IO environment for path resolution.</param>
    /// <returns>A Uri representing the source.</returns>
    public static Uri ToSourceUri(this string source, IEnvironment environment)
    {
        if (source == "-")
        {
            return new Uri("stdin://input");
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri))
        {
            // If it's already a URI, keep it (including file:// URIs).
            if (!string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase))
            {
                return uri;
            }

            return uri;
        }

        // Treat as a file path using Spectre.IO.
        string path = source;
        FilePath filePath = new(path);

        if (IsWindowsDriveRelativePath(path) || filePath.IsRelative)
        {
            filePath = filePath.MakeAbsolute(environment);
            path = filePath.FullPath;
        }

        return CreateFileUriFromPath(path);
    }

    private static bool IsWindowsDriveRelativePath(string path)
    {
        // "C:foo" is drive-relative; "C:\\foo" and "C:/foo" are rooted.
        if (path.Length < 2)
        {
            return false;
        }

        if (!char.IsLetter(path[0]) || path[1] != ':')
        {
            return false;
        }

        return path.Length == 2 || (path[2] != '\\' && path[2] != '/');
    }

    private static Uri CreateFileUriFromPath(string path)
    {
        string normalized = path.Replace('\\', '/');

        // UNC path: //server/share/path => file://server/share/path
        if (normalized.StartsWith("//", StringComparison.Ordinal))
        {
            string withoutLeadingSlashes = normalized.TrimStart('/');
            int firstSlash = withoutLeadingSlashes.IndexOf('/');

            string host = firstSlash >= 0
                ? withoutLeadingSlashes[..firstSlash]
                : withoutLeadingSlashes;

            string pathPart = firstSlash >= 0
                ? withoutLeadingSlashes[firstSlash..]
                : "/";

            return new UriBuilder("file", host) { Path = pathPart }.Uri;
        }

        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        // Empty host produces file:///...
        return new UriBuilder("file", string.Empty) { Path = normalized }.Uri;
    }

    /// <summary>
    /// Reads a document from a string source (file path, "-" for stdin, or URI).
    /// </summary>
    public static Task<RlmDocument?> ReadAsync(
        this IDocumentReader reader,
        string source,
        CancellationToken cancellationToken = default)
        => reader.ReadAsync(source.ToSourceUri(), cancellationToken);

    /// <summary>
    /// Reads multiple documents from a string source with optional glob pattern.
    /// </summary>
    public static IAsyncEnumerable<RlmDocument> ReadManyAsync(
        this IDocumentReader reader,
        string source,
        string? pattern = null,
        CancellationToken cancellationToken = default)
        => reader.ReadManyAsync(source.ToSourceUri(), pattern, cancellationToken);

    /// <summary>
    /// Checks if this reader can handle a string source.
    /// </summary>
    public static bool CanRead(this IDocumentReader reader, string source)
        => reader.CanRead(source.ToSourceUri());
}
