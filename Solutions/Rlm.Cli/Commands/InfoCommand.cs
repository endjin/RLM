// <copyright file="InfoCommand.cs" company="Endjin Limited">
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
/// Shows information about the current session state.
/// </summary>
public sealed class InfoCommand(
    IAnsiConsole console,
    ISessionStore sessionStore) : AsyncCommand<InfoCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-j|--json")]
        [Description("Output in JSON format for machine parsing")]
        public bool Json { get; set; }

        [CommandOption("--progress")]
        [Description("Show processing progress summary")]
        public bool Progress { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        RlmSession session = await sessionStore.LoadAsync(cancellationToken);

        if (!session.HasDocument)
        {
            if (settings.Json)
            {
                console.WriteLine("{}");
            }
            else
            {
                console.MarkupLine("[yellow]No document loaded.[/] Use [cyan]rlm load <file>[/] to load a document.");
            }
            return 0;
        }

        if (settings.Json)
        {
            int totalChunks = session.ChunkBuffer.Count;
            int processedChunks = session.CurrentChunkIndex + 1;
            int totalChars = session.ChunkBuffer.Sum(c => c.Content.Length);
            int processedChars = session.ChunkBuffer.Take(processedChunks).Sum(c => c.Content.Length);
            int avgChunkSize = totalChunks > 0 ? totalChars / totalChunks : 0;
            int remainingTokens = session.ChunkBuffer.Skip(processedChunks).Sum(c => c.TokenEstimate);
            double progressPercent = totalChunks > 0 ? (double)processedChunks / totalChunks * 100 : 0;

            SessionInfoOutput output = new()
            {
                Source = session.Metadata?.Source ?? "unknown",
                TotalLength = session.Metadata?.TotalLength ?? 0,
                TokenEstimate = session.Metadata?.TokenEstimate ?? 0,
                LineCount = session.Metadata?.LineCount ?? 0,
                LoadedAt = session.Metadata?.LoadedAt.ToString("o") ?? "",
                ChunkCount = session.ChunkBuffer.Count,
                CurrentChunkIndex = session.CurrentChunkIndex,
                RemainingChunks = Math.Max(0, session.ChunkBuffer.Count - session.CurrentChunkIndex - 1),
                ResultCount = session.Results.Count,
                RecursionDepth = session.RecursionDepth,
                MaxRecursionDepth = RlmSession.MaxRecursionDepth,
                ProgressPercent = progressPercent,
                ProcessedChars = processedChars,
                TotalChars = totalChars,
                AverageChunkSize = avgChunkSize,
                RemainingTokenEstimate = remainingTokens
            };

            string json = JsonSerializer.Serialize(output, RlmJsonContext.Default.SessionInfoOutput);
            console.WriteLine(json);
            return 0;
        }

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("Source", session.Metadata?.Source ?? "unknown");
        table.AddRow("Length", $"{session.Metadata?.TotalLength:N0} chars");
        table.AddRow("Tokens (est)", $"~{session.Metadata?.TokenEstimate:N0}");
        table.AddRow("Lines", $"{session.Metadata?.LineCount:N0}");
        table.AddRow("Loaded at", session.Metadata?.LoadedAt.ToString("yyyy-MM-dd HH:mm:ss") ?? "unknown");
        table.AddRow("Recursion depth", $"{session.RecursionDepth} / {RlmSession.MaxRecursionDepth}");

        console.Write(table);

        // Show chunk info if available
        if (session.HasChunks)
        {
            console.WriteLine();

            // Progress mode shows detailed progress bar
            if (settings.Progress)
            {
                OutputProgressDisplay(session);
            }
            else
            {
                console.MarkupLine($"[cyan]Chunks:[/] {session.ChunkBuffer.Count} chunks, at index {session.CurrentChunkIndex + 1}");
                console.MarkupLine($"[cyan]Remaining:[/] {session.ChunkBuffer.Count - session.CurrentChunkIndex - 1} chunks");
            }
        }

        // Show results info
        if (session.Results.Count > 0)
        {
            console.WriteLine();
            console.MarkupLine($"[cyan]Stored results:[/] {session.Results.Count}");

            foreach (string key in session.Results.Keys.Take(5))
            {
                console.MarkupLine($"  - {key}");
            }

            if (session.Results.Count > 5)
            {
                console.MarkupLine($"  ... and {session.Results.Count - 5} more");
            }
        }

        return 0;
    }

    private void OutputProgressDisplay(RlmSession session)
    {
        int totalChunks = session.ChunkBuffer.Count;
        int processedChunks = session.CurrentChunkIndex + 1;
        int remainingChunks = totalChunks - processedChunks;
        double progressPercent = totalChunks > 0 ? (double)processedChunks / totalChunks * 100 : 0;

        // Calculate chunk statistics
        int totalChars = session.ChunkBuffer.Sum(c => c.Content.Length);
        int processedChars = session.ChunkBuffer.Take(processedChunks).Sum(c => c.Content.Length);
        int avgChunkSize = totalChunks > 0 ? totalChars / totalChunks : 0;

        // Progress bar
        int barWidth = 30;
        int filledWidth = (int)Math.Round(barWidth * progressPercent / 100);
        string progressBar = new string('█', filledWidth) + new string('░', barWidth - filledWidth);

        console.MarkupLine($"[cyan]Progress:[/] [{(progressPercent < 50 ? "yellow" : "green")}]{progressBar}[/] {progressPercent:F1}%");
        console.MarkupLine($"[cyan]Chunks:[/] {processedChunks:N0} / {totalChunks:N0} ({remainingChunks:N0} remaining)");
        console.MarkupLine($"[cyan]Characters:[/] {processedChars:N0} / {totalChars:N0} processed");
        console.MarkupLine($"[cyan]Results:[/] {session.Results.Count:N0} stored");
        console.MarkupLine($"[cyan]Avg chunk size:[/] {avgChunkSize:N0} chars");

        // Estimated tokens remaining
        int remainingTokens = session.ChunkBuffer.Skip(processedChunks).Sum(c => c.TokenEstimate);
        console.MarkupLine($"[cyan]Est. tokens remaining:[/] ~{remainingTokens:N0}");
    }
}