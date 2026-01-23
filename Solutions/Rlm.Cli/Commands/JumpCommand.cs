// <copyright file="JumpCommand.cs" company="Endjin Limited">
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
/// Jumps to a specific chunk index.
/// </summary>
public sealed class JumpCommand(IAnsiConsole console, ISessionStore sessionStore) : AsyncCommand<JumpCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<index>")]
        [Description("Target chunk index (1-based) or percentage with % suffix (e.g., 50%)")]
        public string Target { get; set; } = "1";

        [CommandOption("-j|--json")]
        [Description("Output in JSON format for machine parsing")]
        public bool Json { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        RlmSession session = await sessionStore.LoadAsync(cancellationToken);

        if (!session.HasChunks)
        {
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

        int targetIndex;

        // Handle percentage notation (e.g., "50%")
        if (settings.Target.EndsWith('%'))
        {
            if (!int.TryParse(settings.Target.TrimEnd('%'), out int percentage))
            {
                console.MarkupLine("[red]Error:[/] Invalid percentage format. Use a number followed by % (e.g., 50%).");
                return 1;
            }

            percentage = Math.Clamp(percentage, 0, 100);
            targetIndex = (int)Math.Round(session.ChunkBuffer.Count * (percentage / 100.0)) - 1;
        }
        else
        {
            // Handle absolute index (1-based)
            if (!int.TryParse(settings.Target, out int index))
            {
                console.MarkupLine("[red]Error:[/] Invalid index format. Use a number or percentage (e.g., 50%).");
                return 1;
            }

            targetIndex = index - 1; // Convert to 0-based
        }

        // Clamp to valid range
        targetIndex = Math.Clamp(targetIndex, 0, session.ChunkBuffer.Count - 1);

        int previousIndex = session.CurrentChunkIndex;
        session.CurrentChunkIndex = targetIndex;
        await sessionStore.SaveAsync(session, cancellationToken);

        ContentChunk chunk = session.CurrentChunk!;

        if (settings.Json)
        {
            _ = chunk.Metadata.TryGetValue("tokenCount", out string? tokenCountStr);
            int? tokenCount = int.TryParse(tokenCountStr, out int tc) ? tc : null;

            ChunkOutput output = new()
            {
                Index = chunk.Index,
                TotalChunks = session.ChunkBuffer.Count,
                StartPosition = chunk.StartPosition,
                EndPosition = chunk.EndPosition,
                Length = chunk.Length,
                TokenEstimate = chunk.TokenEstimate,
                TokenCount = tokenCount,
                HasMore = session.HasMoreChunks,
                Content = chunk.Content,
                Metadata = new Dictionary<string, string>(chunk.Metadata)
                {
                    ["jumpedFrom"] = (previousIndex + 1).ToString()
                }
            };

            string jsonOutput = JsonSerializer.Serialize(output, RlmJsonContext.Default.ChunkOutput);
            console.WriteLine(jsonOutput);
            return 0;
        }

        console.MarkupLine($"[dim]Jumped from chunk {previousIndex + 1} to {targetIndex + 1}[/]");
        console.WriteLine();
        OutputChunk(chunk, session.ChunkBuffer.Count, session.HasMoreChunks);

        return 0;
    }

    private void OutputChunk(ContentChunk chunk, int totalChunks, bool hasMore)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("Index", $"{chunk.Index + 1} of {totalChunks}");
        table.AddRow("Position", $"{chunk.StartPosition:N0}..{chunk.EndPosition:N0}");
        table.AddRow("Length", $"{chunk.Length:N0} chars");

        if (chunk.Metadata.TryGetValue("tokenCount", out string? tokenCount))
        {
            table.AddRow("Tokens", tokenCount);
        }
        else
        {
            table.AddRow("Tokens (est)", $"~{chunk.TokenEstimate:N0}");
        }

        table.AddRow("Remaining", hasMore ? $"{totalChunks - chunk.Index - 1}" : "[green]Last chunk[/]");

        if (chunk.Metadata.TryGetValue("sectionHeader", out string? header))
        {
            table.AddRow("Section", header);
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
