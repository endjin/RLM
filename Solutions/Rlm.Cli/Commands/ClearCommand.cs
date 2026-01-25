// <copyright file="ClearCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Rlm.Cli.Commands;

/// <summary>
/// Clears the current session state.
/// </summary>
public sealed class ClearCommand(IAnsiConsole console, ISessionStore sessionStore) : AsyncCommand<ClearCommand.Settings>
{
    public sealed class Settings : RlmCommandSettings
    {
        [CommandOption("--all")]
        [Description("Delete ALL RLM sessions found in the storage location.")]
        public bool All { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.All)
        {
            sessionStore.DeleteAll();
            console.MarkupLine("[green]All RLM sessions cleared.[/]");
            return 0;
        }

        RlmSession session = await sessionStore.LoadAsync(settings.SessionId, cancellationToken);

        bool hadDocument = session.HasDocument;
        bool hadChunks = session.HasChunks;
        bool hadResults = session.Results.Count > 0;

        session.Clear();
        sessionStore.Delete(settings.SessionId);

        if (hadDocument || hadChunks || hadResults)
        {
            string sessionMsg = settings.SessionId is null ? "Session" : $"Session '{settings.SessionId}'";
            console.MarkupLine($"[green]{sessionMsg} cleared.[/]");
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
            string sessionMsg = settings.SessionId is null ? "Session" : $"Session '{settings.SessionId}'";
            console.MarkupLine($"[yellow]{sessionMsg} was already empty.[/]");
        }

        return 0;
    }
}