// <copyright file="SliceCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Rlm.Cli.Commands;

/// <summary>
/// Extracts a slice of the document content. Supports Python-like slice notation.
/// </summary>
public sealed class SliceCommand(IAnsiConsole console, ISessionStore sessionStore) : AsyncCommand<SliceCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<range>")]
        [Description("Slice range (e.g., '0:1000', '-500:', ':1000')")]
        public string Range { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        RlmSession session = await sessionStore.LoadAsync(cancellationToken);

        if (!session.HasDocument)
        {
            console.MarkupLine("[red]Error:[/] No document loaded. Use [cyan]rlm load <file>[/] first.");
            return 1;
        }

        string content = session.Content!;
        (int start, int end) = ParseRange(settings.Range, content.Length);

        if (start < 0 || end > content.Length || start > end)
        {
            console.MarkupLine($"[red]Error:[/] Invalid range. Document length is {content.Length:N0} chars.");
            return 1;
        }

        string slice = content[start..end];

        // Output slice info
        console.MarkupLine($"[cyan]Slice:[/] {start:N0}..{end:N0} ({slice.Length:N0} chars)");
        console.WriteLine();
        console.WriteLine(slice);

        return 0;
    }

    private static (int Start, int End) ParseRange(string range, int length)
    {
        string[] parts = range.Split(':');

        if (parts.Length != 2)
        {
            return (-1, -1);
        }

        string startStr = parts[0].Trim();
        string endStr = parts[1].Trim();

        // Parse start
        int start;
        if (string.IsNullOrEmpty(startStr))
        {
            start = 0;
        }
        else if (int.TryParse(startStr, out int s))
        {
            start = s < 0 ? length + s : s;
        }
        else
        {
            return (-1, -1);
        }

        // Parse end
        int end;
        if (string.IsNullOrEmpty(endStr))
        {
            end = length;
        }
        else if (int.TryParse(endStr, out int e))
        {
            end = e < 0 ? length + e : e;
        }
        else
        {
            return (-1, -1);
        }

        return (Math.Max(0, start), Math.Min(length, end));
    }
}