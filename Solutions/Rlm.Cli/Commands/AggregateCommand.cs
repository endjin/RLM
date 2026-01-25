// <copyright file="AggregateCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Text.Json;
using Rlm.Cli.Core.Output;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Rlm.Cli.Commands;

/// <summary>
/// Aggregates all stored results into a combined output for final synthesis.
/// </summary>
public sealed class AggregateCommand(IAnsiConsole console, ISessionStore sessionStore) : AsyncCommand<AggregateCommand.Settings>
{
    public sealed class Settings : RlmCommandSettings
    {
        [CommandOption("-s|--separator <separator>")]
        [Description("Separator between results (default: newline with dashes)")]
        public string? Separator { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output in JSON format for machine parsing")]
        public bool Json { get; set; }

        [CommandOption("-f|--final")]
        [Description("Wrap output with FINAL signal for completion detection")]
        public bool Final { get; set; }

        [CommandOption("--raw")]
        [Description("Output raw content for piping/scripts.")]
        public bool Raw { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        RlmSession session = await sessionStore.LoadAsync(settings.SessionId, cancellationToken);

        if (session.Results.Count == 0)
        {
            if (settings.Raw)
            {
                return 0;
            }

            if (settings.Json)
            {
                console.WriteLine("{\"error\": \"No results to aggregate\"}");
            }
            else
            {
                console.MarkupLine("[yellow]No results to aggregate.[/]");
                console.MarkupLine("Use [cyan]rlm store <key> <value>[/] to store results first.");
            }
            return 0;
        }

        ResultBuffer buffer = session.ToResultBuffer();
        string separator = settings.Separator ?? "\n\n---\n\n";
        string combined = buffer.GetCombined(separator);

        if (settings.Raw)
        {
            Console.WriteLine(combined);
            return 0;
        }

        if (settings.Json)
        {
            AggregateOutput output = new()
            {
                ResultCount = session.Results.Count,
                Combined = combined,
                Signal = settings.Final ? "FINAL" : "PARTIAL",
                Results = new Dictionary<string, string>(session.Results)
            };

            string json = JsonSerializer.Serialize(output, RlmJsonContext.Default.AggregateOutput);
            console.WriteLine(json);
            return 0;
        }

        // Output aggregation info
        console.MarkupLine($"[cyan]Aggregating {session.Results.Count} results[/]");
        console.WriteLine();

        if (settings.Final)
        {
            console.MarkupLine("[green]FINAL([/]");
            console.WriteLine(combined);
            console.MarkupLine("[green])[/]");
        }
        else
        {
            console.WriteLine(combined);
        }

        return 0;
    }
}