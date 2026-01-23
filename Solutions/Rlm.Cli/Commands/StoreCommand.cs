// <copyright file="StoreCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Rlm.Cli.Commands;

/// <summary>
/// Stores a partial result from processing a chunk.
/// </summary>
public sealed class StoreCommand(IAnsiConsole console, ISessionStore sessionStore) : AsyncCommand<StoreCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<key>")]
        [Description("Key to identify this result (e.g., 'chunk_0', 'section_intro')")]
        public string Key { get; set; } = string.Empty;

        [CommandArgument(1, "<value>")]
        [Description("The result value to store")]
        public string Value { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        RlmSession session = await sessionStore.LoadAsync(cancellationToken);

        // Store the result
        session.Results[settings.Key] = settings.Value;
        await sessionStore.SaveAsync(session, cancellationToken);

        console.MarkupLine($"[green]Stored:[/] {settings.Key}");
        console.MarkupLine($"[cyan]Total results:[/] {session.Results.Count}");

        // Show what's next
        if (session.HasMoreChunks)
        {
            console.MarkupLine($"[dim]Use [cyan]rlm next[/] to get the next chunk.[/]");
        }
        else if (session.HasChunks)
        {
            console.MarkupLine($"[dim]All chunks processed. Use [cyan]rlm aggregate[/] to combine results.[/]");
        }

        return 0;
    }
}