// <copyright file="ClearCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Rlm.Cli.Commands;

/// <summary>
/// Clears the current session state.
/// </summary>
public sealed class ClearCommand(IAnsiConsole console, ISessionStore sessionStore) : AsyncCommand<ClearCommand.Settings>
{
    public sealed class Settings : CommandSettings;

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        RlmSession session = await sessionStore.LoadAsync(cancellationToken);

        bool hadDocument = session.HasDocument;
        bool hadChunks = session.HasChunks;
        bool hadResults = session.Results.Count > 0;

        session.Clear();
        sessionStore.Delete();

        if (hadDocument || hadChunks || hadResults)
        {
            console.MarkupLine("[green]Session cleared.[/]");
            if (hadDocument)
            {
                console.MarkupLine("[dim]- Document unloaded[/]");
            }
            if (hadChunks)
            {
                console.MarkupLine("[dim]- Chunk buffer cleared[/]");
            }
            if (hadResults)
            {
                console.MarkupLine("[dim]- Results removed[/]");
            }
        }
        else
        {
            console.MarkupLine("[yellow]Session was already empty.[/]");
        }

        return 0;
    }
}