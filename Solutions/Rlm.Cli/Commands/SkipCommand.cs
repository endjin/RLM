// <copyright file="SkipCommand.cs" company="Endjin Limited">
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
/// Skips a specified number of chunks forward or backward.
/// </summary>
public sealed class SkipCommand(IAnsiConsole console, ISessionStore sessionStore) : AsyncCommand<SkipCommand.Settings>
{
    public sealed class Settings : RlmCommandSettings
    {
        [CommandArgument(0, "<count>")]
        [Description("Number of chunks to skip (positive = forward, negative = backward)")]
        public int Count { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output in JSON format for machine parsing")]
        public bool Json { get; set; }

        [CommandOption("--skip-empty")]
        [Description("Skip empty or very small chunks (< 100 chars)")]
        public bool SkipEmpty { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        RlmSession session = await sessionStore.LoadAsync(settings.SessionId, cancellationToken);

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

        int targetIndex = session.CurrentChunkIndex + settings.Count;

        // Skip empty chunks if requested
        if (settings.SkipEmpty && settings.Count > 0)
        {
            while (targetIndex < session.ChunkBuffer.Count &&
                   session.ChunkBuffer[targetIndex].Content.Length < 100)
            {
                targetIndex++;
            }
        }
        else if (settings.SkipEmpty && settings.Count < 0)
        {
            while (targetIndex >= 0 &&
                   session.ChunkBuffer[targetIndex].Content.Length < 100)
            {
                targetIndex--;
            }
        }

        // Clamp to valid range
        int previousIndex = session.CurrentChunkIndex;
        targetIndex = Math.Clamp(targetIndex, 0, session.ChunkBuffer.Count - 1);

        session.CurrentChunkIndex = targetIndex;
        await sessionStore.SaveAsync(session, settings.SessionId, cancellationToken);

        ContentChunk chunk = session.CurrentChunk!;
        int skipped = targetIndex - previousIndex;

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
                    ["skipped"] = skipped.ToString()
                }
            };

            string jsonOutput = JsonSerializer.Serialize(output, RlmJsonContext.Default.ChunkOutput);
            console.WriteLine(jsonOutput);
            return 0;
        }

        console.MarkupLine($"[dim]Skipped {skipped} chunk(s)[/]");
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
