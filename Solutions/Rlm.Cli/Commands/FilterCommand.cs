// <copyright file="FilterCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Rlm.Cli.Commands;

/// <summary>
/// Filters the document using a regex pattern. Shorthand for 'chunk --strategy filter'.
/// </summary>
public sealed class FilterCommand(
    IAnsiConsole console,
    ISessionStore sessionStore) : AsyncCommand<FilterCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<pattern>")]
        [Description("Regex pattern to search for")]
        public string Pattern { get; set; } = string.Empty;

        [CommandOption("-c|--context <context>")]
        [Description("Context size around matches (default: 500)")]
        [DefaultValue(500)]
        public int Context { get; set; } = 500;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        RlmSession session = await sessionStore.LoadAsync(cancellationToken);

        if (!session.HasDocument)
        {
            console.MarkupLine("[red]Error:[/] No document loaded. Use [cyan]rlm load <file>[/] first.");
            return 1;
        }

        RlmDocument document = session.ToDocument();
        FilteringChunker chunker = new(settings.Pattern, settings.Context);

        // Collect chunks
        List<ContentChunk> chunks = [];
        await foreach (ContentChunk chunk in chunker.ChunkAsync(document, cancellationToken))
        {
            chunks.Add(chunk);
        }

        if (chunks.Count == 0)
        {
            console.MarkupLine($"[yellow]No matches found for pattern:[/] {settings.Pattern}");
            return 0;
        }

        // Update session
        session.ChunkBuffer = chunks;
        session.CurrentChunkIndex = 0;
        await sessionStore.SaveAsync(session, cancellationToken);

        // Output summary
        console.MarkupLine($"[green]Found {chunks.Count} matching segment(s)[/]");
        console.WriteLine();

        // Show first chunk
        OutputChunk(chunks[0], chunks.Count);

        return 0;
    }

    private void OutputChunk(ContentChunk chunk, int totalChunks)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("Index", $"{chunk.Index + 1} of {totalChunks}");
        table.AddRow("Position", $"{chunk.StartPosition:N0}..{chunk.EndPosition:N0}");
        table.AddRow("Length", $"{chunk.Length:N0} chars");

        if (chunk.Metadata.TryGetValue("matchCount", out string? matchCount))
        {
            table.AddRow("Matches", matchCount);
        }

        console.Write(table);
        console.WriteLine();
        console.WriteLine(chunk.Content);
    }
}