// <copyright file="ChunkCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Diagnostics;
using Rlm.Cli.Core.Chunking;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Rlm.Cli.Commands;

/// <summary>
/// Chunks the loaded document using the specified strategy.
/// </summary>
public sealed class ChunkCommand(IAnsiConsole console, ISessionStore sessionStore) : AsyncCommand<ChunkCommand.Settings>
{
    public sealed class Settings : RlmCommandSettings
    {
        [CommandOption("-s|--strategy <strategy>")]
        [Description("Chunking strategy: uniform, filter, semantic, token, recursive, auto")]
        [DefaultValue("uniform")]
        public string Strategy { get; set; } = "uniform";

        [CommandOption("--size <size>")]
        [Description("Chunk size for uniform strategy (default: 50000)")]
        [DefaultValue(50000)]
        public int Size { get; set; } = 50000;

        [CommandOption("--overlap <overlap>")]
        [Description("Overlap size for uniform strategy (default: 0)")]
        [DefaultValue(0)]
        public int Overlap { get; set; }

        [CommandOption("-p|--pattern <pattern>")]
        [Description("Regex pattern for filter strategy (or hybrid filter with semantic)")]
        public string? Pattern { get; set; }

        [CommandOption("-c|--context <context>")]
        [Description("Context size around matches for filter strategy (default: 500)")]
        [DefaultValue(500)]
        public int Context { get; set; } = 500;

        [CommandOption("--min-level <level>")]
        [Description("Minimum header level for semantic strategy (default: 1)")]
        [DefaultValue(1)]
        public int MinLevel { get; set; } = 1;

        [CommandOption("--max-level <level>")]
        [Description("Maximum header level for semantic strategy (default: 3)")]
        [DefaultValue(3)]
        public int MaxLevel { get; set; } = 3;

        [CommandOption("--min-size <size>")]
        [Description("Minimum chunk size for semantic strategy. Smaller chunks will be merged (default: 0)")]
        [DefaultValue(0)]
        public int MinSize { get; set; }

        [CommandOption("--max-size <size>")]
        [Description("Maximum chunk size for semantic strategy. Larger chunks will be split (default: 0)")]
        [DefaultValue(0)]
        public int MaxSize { get; set; }

        [CommandOption("--merge-small")]
        [Description("Merge consecutive small sections (use with --min-size)")]
        public bool MergeSmall { get; set; }

        [CommandOption("--max-tokens <tokens>")]
        [Description("Maximum tokens per chunk for token strategy (default: 512)")]
        [DefaultValue(512)]
        public int MaxTokens { get; set; } = 512;

        [CommandOption("--overlap-tokens <tokens>")]
        [Description("Overlap tokens for token strategy (default: 50)")]
        [DefaultValue(50)]
        public int OverlapTokens { get; set; } = 50;

        [CommandOption("-q|--query <query>")]
        [Description("Query text for auto strategy selection")]
        public string? Query { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        RlmSession session = await sessionStore.LoadAsync(settings.SessionId, cancellationToken);

        if (!session.HasDocument)
        {
            console.MarkupLine("[red]Error:[/] No document loaded. Use [cyan]rlm load <file>[/] first.");
            return 1;
        }

        // Validate filter strategy requires pattern
        if (settings.Strategy.ToLowerInvariant() == "filter" && string.IsNullOrEmpty(settings.Pattern))
        {
            console.MarkupLine("[red]Error:[/] Filter strategy requires --pattern option.");
            return 1;
        }

        RlmDocument document = session.ToDocument();

        string strategy = settings.Strategy.ToLowerInvariant();

        // Auto strategy selection based on query keywords
        if (strategy == "auto")
        {
            strategy = SelectStrategy(settings.Query, document.Content, settings.Pattern);
            console.MarkupLine($"[dim]Auto-selected strategy:[/] [cyan]{strategy}[/]");
        }

        // Determine if hybrid mode (semantic + filter)
        bool isHybridMode = strategy == "semantic" && !string.IsNullOrEmpty(settings.Pattern);
        if (isHybridMode)
        {
            console.MarkupLine($"[dim]Hybrid mode:[/] semantic + filter pattern [cyan]{settings.Pattern}[/]");
        }

        IChunker chunker = strategy switch
        {
            "filter" => new FilteringChunker(settings.Pattern!, settings.Context),
            "semantic" => new SemanticChunker(
                settings.MinLevel,
                settings.MaxLevel,
                settings.MinSize,
                settings.MaxSize,
                settings.MergeSmall,
                settings.Pattern), // Pass filter pattern for hybrid mode
            "token" => new TokenBasedChunker(settings.MaxTokens, settings.OverlapTokens),
            "recursive" => new RecursiveChunker(settings.Size),
            _ => new UniformChunker(settings.Size, settings.Overlap)
        };

        // Collect chunks
        List<ContentChunk> chunks = [];
        await foreach (ContentChunk chunk in chunker.ChunkAsync(document, cancellationToken))
        {
            chunks.Add(chunk);
        }

        if (chunks.Count == 0)
        {
            console.MarkupLine("[yellow]No chunks created.[/] Check your pattern or document content.");
            return 0;
        }

        // Update session
        session.ChunkBuffer = chunks;
        session.CurrentChunkIndex = 0;
        await sessionStore.SaveAsync(session, settings.SessionId, cancellationToken);

        stopwatch.Stop();

        // Output summary with chunk statistics
        int totalChars = chunks.Sum(c => c.Content.Length);
        int avgSize = totalChars / chunks.Count;
        int minChunkSize = chunks.Min(c => c.Content.Length);
        int maxChunkSize = chunks.Max(c => c.Content.Length);
        int totalTokens = chunks.Sum(c => c.TokenEstimate);

        console.MarkupLine($"[green]Created {chunks.Count} chunk(s)[/] using [cyan]{settings.Strategy}[/] strategy.");
        console.MarkupLine($"[dim]Stats: avg={avgSize:N0}, min={minChunkSize:N0}, max={maxChunkSize:N0} chars[/]");
        console.MarkupLine($"[dim]Total: {totalChars:N0} chars, ~{totalTokens:N0} tokens | Time: {stopwatch.ElapsedMilliseconds:N0} ms[/]");
        console.WriteLine();

        // Show first chunk
        OutputChunk(chunks[0], session.ChunkBuffer.Count);

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

        // Show actual token count if available, otherwise estimate
        if (chunk.Metadata.TryGetValue("tokenCount", out string? tokenCount))
        {
            table.AddRow("Tokens", tokenCount);
        }
        else
        {
            table.AddRow("Tokens (est)", $"~{chunk.TokenEstimate:N0}");
        }

        // Add metadata
        if (chunk.Metadata.TryGetValue("sectionHeader", out string? header))
        {
            table.AddRow("Section", header);
        }
        if (chunk.Metadata.TryGetValue("matchCount", out string? matchCount))
        {
            table.AddRow("Matches", matchCount);
        }

        console.Write(table);
        console.WriteLine();
        console.WriteLine(chunk.Content);
    }

    /// <summary>
    /// Selects the appropriate chunking strategy based on query keywords and content structure.
    /// </summary>
    private static string SelectStrategy(string? query, string content, string? pattern = null)
    {
        string lowerQuery = query?.ToLowerInvariant() ?? string.Empty;

        // If a pattern is provided, prefer filter or semantic+filter hybrid
        if (!string.IsNullOrEmpty(pattern))
        {
            // Check if content has markdown structure for hybrid mode
            if (content.Contains("\n# ") || content.Contains("\n## ") || content.Contains("\n### "))
            {
                return "semantic"; // Will use hybrid mode with the pattern
            }
            return "filter";
        }

        // Needle-in-haystack indicators: filter strategy
        // These indicate looking for specific items
        string[] needleIndicators =
        [
            "find", "locate", "where is", "where are", "what is the",
            "specific", "search", "look for", "extract", "identify"
        ];

        // Aggregation/comprehensive indicators: uniform or token strategy
        // These indicate processing the entire document systematically
        string[] aggregationIndicators =
        [
            "all", "count", "list", "summarize", "total", "how many",
            "every", "entire", "full", "comprehensive", "complete"
        ];

        // Comparison/analysis indicators: semantic strategy
        // These indicate structural analysis or section-based work
        string[] analysisIndicators =
        [
            "compare", "contrast", "difference", "differences", "similar",
            "versus", "vs", "sections", "chapters", "structure", "outline",
            "hierarchy", "organization", "topics", "categories"
        ];

        // Implementation/pattern indicators: semantic + filter hybrid
        // These indicate looking for code patterns or implementations
        string[] implementationIndicators =
        [
            "implement", "implementation", "pattern", "patterns", "example",
            "examples", "code", "function", "method", "class", "interface",
            "api", "endpoint", "configuration", "config"
        ];

        // Score each strategy based on keyword matches
        int filterScore = 0;
        int aggregationScore = 0;
        int semanticScore = 0;
        int implementationScore = 0;

        filterScore += needleIndicators.Count(indicator => lowerQuery.Contains(indicator)) * 2;
        aggregationScore += aggregationIndicators.Count(indicator => lowerQuery.Contains(indicator)) * 2;
        semanticScore += analysisIndicators.Count(indicator => lowerQuery.Contains(indicator)) * 2;
        implementationScore += implementationIndicators.Count(indicator => lowerQuery.Contains(indicator)) * 2;

        // Check content structure for markdown headers
        bool hasMarkdownStructure = content.Contains("\n# ") ||
                                    content.Contains("\n## ") ||
                                    content.Contains("\n### ");

        // Bonus for structured content
        if (hasMarkdownStructure)
        {
            semanticScore += 1;
        }

        // Determine winner
        if (filterScore > 0 && filterScore >= semanticScore && filterScore >= aggregationScore)
        {
            return "filter";
        }

        if (semanticScore > 0 && semanticScore >= aggregationScore)
        {
            return "semantic";
        }

        if (aggregationScore > 0)
        {
            return "token";
        }

        // Implementation patterns with structured content suggest semantic
        if (implementationScore > 0 && hasMarkdownStructure)
        {
            return "semantic";
        }

        // Default based on content structure
        return hasMarkdownStructure ? "semantic" : "token";
    }
}