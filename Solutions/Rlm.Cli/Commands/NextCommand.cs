// <copyright file="NextCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Text.Json;
using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Output;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Rlm.Cli.Commands;

/// <summary>
/// Advances to and displays the next chunk from the buffer.
/// </summary>
public sealed class NextCommand(IAnsiConsole console, ISessionStore sessionStore) : AsyncCommand<NextCommand.Settings>
{
    public sealed class Settings : RlmCommandSettings
    {
        [CommandOption("-j|--json")]
        [Description("Output in JSON format for machine parsing")]
        public bool Json { get; set; }

        [CommandOption("--raw")]
        [Description("Output raw content for piping/scripts.")]
        public bool Raw { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        RlmSession session = await sessionStore.LoadAsync(settings.SessionId, cancellationToken);

        if (!session.HasChunks)
        {
            if (settings.Raw) return 1;

            if (settings.Json)
            {
                console.WriteLine("{\"error\": \"No chunks available\"}");
            }
            else
            {
                console.MarkupLine("[red]Error:[/] No chunks available. Use [cyan]rlm chunk[/] first.");
            }
            return 1;
        }

        if (!session.HasMoreChunks)
        {
            if (settings.Raw) return 0;

            if (settings.Json)
            {
                console.WriteLine("{\"done\": true, \"message\": \"No more chunks\"}");
            }
            else
            {
                console.MarkupLine("[yellow]No more chunks.[/] All chunks have been processed.");
                console.MarkupLine($"Total chunks: {session.ChunkBuffer.Count}");
                console.MarkupLine($"Stored results: {session.Results.Count}");
                console.MarkupLine("Use [cyan]rlm aggregate[/] to combine results.");
            }
            return 0;
        }

        // Advance to next chunk
        session.CurrentChunkIndex++;
        await sessionStore.SaveAsync(session, settings.SessionId, cancellationToken);

        ContentChunk chunk = session.CurrentChunk!;
        
        if (settings.Raw)
        {
            Console.WriteLine(chunk.Content);
            return 0;
        }

        OutputChunk(chunk, session.ChunkBuffer.Count, session.HasMoreChunks, settings.Json);

        return 0;
    }

    private void OutputChunk(ContentChunk chunk, int totalChunks, bool hasMore, bool json)
    {
        if (json)
        {
            _ = chunk.Metadata.TryGetValue("tokenCount", out string? tokenCountStr);
            int? tokenCount = int.TryParse(tokenCountStr, out int tc) ? tc : null;

            ChunkOutput output = new()
            {
                Index = chunk.Index,
                TotalChunks = totalChunks,
                StartPosition = chunk.StartPosition,
                EndPosition = chunk.EndPosition,
                Length = chunk.Length,
                TokenEstimate = chunk.TokenEstimate,
                TokenCount = tokenCount,
                HasMore = hasMore,
                Content = chunk.Content,
                Metadata = new Dictionary<string, string>(chunk.Metadata)
            };

            string jsonOutput = JsonSerializer.Serialize(output, RlmJsonContext.Default.ChunkOutput);
            console.WriteLine(jsonOutput);
            return;
        }

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("Index", $"{chunk.Index + 1} of {totalChunks}");
        table.AddRow("Position", $"{chunk.StartPosition:N0}..{chunk.EndPosition:N0}");
        table.AddRow("Length", $"{chunk.Length:N0} chars");

        // Show actual token count if available, otherwise estimate
        if (chunk.Metadata.TryGetValue("tokenCount", out string? tokenCount2))
        {
            table.AddRow("Tokens", tokenCount2);
        }
        else
        {
            table.AddRow("Tokens (est)", $"~{chunk.TokenEstimate:N0}");
        }

        table.AddRow("Remaining", hasMore ? $"{totalChunks - chunk.Index - 1}" : "[green]Last chunk[/]");

        // Add metadata
        if (chunk.Metadata.TryGetValue("sectionHeader", out string? header))
        {
            table.AddRow("Section", header);
        }
        if (chunk.Metadata.TryGetValue("matchCount", out string? matchCount))
        {
            table.AddRow("Matches", matchCount);
        }
        if (chunk.Metadata.TryGetValue("headerPath", out string? path))
        {
            table.AddRow("Path", path);
        }

        console.Write(table);
        console.WriteLine();
        console.WriteLine(chunk.Content);
    }
}