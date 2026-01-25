// <copyright file="ResultsCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Rlm.Cli.Commands;

/// <summary>
/// Lists all stored partial results.
/// </summary>
public sealed class ResultsCommand(IAnsiConsole console, ISessionStore sessionStore) : AsyncCommand<ResultsCommand.Settings>
{
    public sealed class Settings : RlmCommandSettings;

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        RlmSession session = await sessionStore.LoadAsync(settings.SessionId, cancellationToken);

        if (session.Results.Count == 0)
        {
            console.MarkupLine("[yellow]No results stored.[/]");
            console.MarkupLine("Use [cyan]rlm store <key> <value>[/] to store results.");
            return 0;
        }

        console.MarkupLine($"[cyan]Stored results:[/] {session.Results.Count}");
        console.WriteLine();

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Key")
            .AddColumn("Value (truncated)");

        foreach ((string key, string value) in session.Results.OrderBy(kv => kv.Key))
        {
            string truncated = value.Length > 100
                ? value[..100] + "..."
                : value;
            table.AddRow(key, Markup.Escape(truncated));
        }

        console.Write(table);

        return 0;
    }
}