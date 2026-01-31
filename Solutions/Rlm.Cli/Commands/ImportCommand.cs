// <copyright file="ImportCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.IO;

namespace Rlm.Cli.Commands;

/// <summary>
/// Bulk imports results from files matching a pattern.
/// </summary>
public sealed class ImportCommand(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IEnvironment environment,
    ISessionStore sessionStore) : AsyncCommand<ImportCommand.Settings>
{
    public sealed class Settings : RlmCommandSettings
    {
        [CommandArgument(0, "<pattern>")]
        [Description("Glob pattern for result files (e.g., 'results/*.txt')")]
        public string Pattern { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        RlmSession session = await sessionStore.LoadAsync(settings.SessionId, cancellationToken);

        // Split path into directory and pattern using Spectre.IO
        DirectoryPath workingDir = environment.WorkingDirectory;
        FilePath patternPath = new(settings.Pattern);

        FilePath fullPath = patternPath.IsRelative
            ? workingDir.CombineWithFilePath(patternPath)
            : patternPath;

        DirectoryPath? directory = fullPath.GetDirectory();
        string pattern = fullPath.GetFilename().ToString();

        if (directory is null || !fileSystem.Directory.Exists(directory))
        {
            directory = workingDir;
            pattern = settings.Pattern;
        }

        // If pattern looks like session files and directory is CWD,
        // check home directory first (where sessions are actually stored)
        bool isSessionPattern = (pattern.StartsWith("rlm-session-", StringComparison.OrdinalIgnoreCase)
                              || pattern.StartsWith(".rlm-session", StringComparison.OrdinalIgnoreCase))
                             && pattern.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        if (isSessionPattern && directory.FullPath == workingDir.FullPath)
        {
            DirectoryPath homeDir = sessionStore.GetSessionDirectory();
            IDirectory homeDirectory = fileSystem.GetDirectory(homeDir);

            if (homeDirectory.Exists)
            {
                IFile[] homeFiles = homeDirectory.GetFiles(pattern, SearchScope.Current).ToArray();
                if (homeFiles.Length > 0)
                {
                    directory = homeDir;
                    console.MarkupLine($"[dim]Found session files in home directory[/]");
                }
            }
        }

        if (string.IsNullOrEmpty(pattern))
        {
            console.MarkupLine($"[red]Error:[/] Invalid pattern: {settings.Pattern}");
            return 1;
        }

        IFile[] files;
        try
        {
            IDirectory dir = fileSystem.GetDirectory(directory);
            files = dir.GetFiles(pattern, SearchScope.Current).ToArray();
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] Failed to search files: {ex.Message}");
            return 1;
        }

        if (files.Length == 0)
        {
            console.MarkupLine($"[yellow]No files found matching:[/] {settings.Pattern}");
            return 0;
        }

        console.MarkupLine($"Found {files.Length} file(s). Importing...");

        int importedCount = 0;
        foreach (IFile file in files)
        {
            try
            {
                string content = await file.ReadAllTextAsync();
                string key = file.Path.GetFilenameWithoutExtension().ToString();

                session.Results[key] = content;
                importedCount++;
                console.MarkupLine($"  [green]+[/] {key}");
            }
            catch (Exception ex)
            {
                console.MarkupLine($"  [red]![/] Failed to read {file.Path.GetFilename()}: {ex.Message}");
            }
        }

        await sessionStore.SaveAsync(session, settings.SessionId, cancellationToken);

        console.WriteLine();
        console.MarkupLine($"[green]Imported {importedCount} result(s).[/]");
        console.MarkupLine($"[cyan]Total results:[/] {session.Results.Count}");

        return 0;
    }
}
