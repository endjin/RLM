// <copyright file="LoadCommand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Rlm.Cli.Core.Documents;
using Rlm.Cli.Core.Session;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.IO;

namespace Rlm.Cli.Commands;

/// <summary>
/// Loads a document or directory of documents into the RLM session for processing.
/// Supports multiple formats: Markdown, PDF, HTML, JSON, Word (.docx), and plain text.
/// </summary>
public sealed class LoadCommand(IAnsiConsole console, IFileSystem fileSystem, ISessionStore sessionStore) : AsyncCommand<LoadCommand.Settings>
{
    public sealed class Settings : RlmCommandSettings
    {
        [CommandArgument(0, "<source>")]
        [Description("File path, directory path, or '-' for stdin")]
        public string Source { get; set; } = string.Empty;

        [CommandOption("-p|--pattern")]
        [Description("Glob pattern for filtering files when loading a directory (e.g., '*.md', '**/*.txt')")]
        public string? Pattern { get; set; }

        [CommandOption("--merge")]
        [Description("When loading multiple files, merge them into a single document with separators")]
        [DefaultValue(true)]
        public bool Merge { get; set; } = true;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Create composite reader with all format-specific readers
        CompositeDocumentReader reader = CreateCompositeReader();

        Uri sourceUri = settings.Source.ToSourceUri();

        if (!reader.CanRead(sourceUri))
        {
            console.MarkupLine($"[red]Error:[/] Cannot read source: {settings.Source}");
            return 1;
        }

        // Check if this is a directory (for batch loading)
        bool isDirectory = sourceUri.Scheme == "file" &&
                          fileSystem.Directory.Exists(new DirectoryPath(sourceUri.LocalPath));

        if (isDirectory)
        {
            return await LoadDirectoryAsync(reader, sourceUri, settings, stopwatch, cancellationToken);
        }

        return await LoadSingleFileAsync(reader, sourceUri, settings, stopwatch, cancellationToken);
    }

    private async Task<int> LoadSingleFileAsync(
        IDocumentReader reader,
        Uri sourceUri,
        Settings settings,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        RlmDocument? document = await reader.ReadAsync(sourceUri, cancellationToken);
        if (document is null)
        {
            console.MarkupLine($"[red]Error:[/] Failed to load document from: {settings.Source}");
            return 1;
        }

        RlmSession session = await sessionStore.LoadAsync(settings.SessionId, cancellationToken);
        session.LoadDocument(document);
        await sessionStore.SaveAsync(session, settings.SessionId, cancellationToken);

        stopwatch.Stop();
        DisplayDocumentInfo(document, stopwatch.Elapsed);
        console.MarkupLine($"[green]Document loaded successfully.[/]");

        return 0;
    }

    private async Task<int> LoadDirectoryAsync(
        IDocumentReader reader,
        Uri sourceUri,
        Settings settings,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        List<RlmDocument> documents = [];
        int fileCount = 0;

        await foreach (RlmDocument doc in reader.ReadManyAsync(sourceUri, settings.Pattern, cancellationToken))
        {
            documents.Add(doc);
            fileCount++;
        }

        if (documents.Count == 0)
        {
            console.MarkupLine($"[red]Error:[/] No documents found in: {settings.Source}");
            if (settings.Pattern is not null)
            {
                console.MarkupLine($"[yellow]Pattern:[/] {settings.Pattern}");
            }
            return 1;
        }

        RlmDocument mergedDocument;

        if (documents.Count == 1)
        {
            mergedDocument = documents[0];
        }
        else if (settings.Merge)
        {
            mergedDocument = MergeDocuments(documents, sourceUri);
        }
        else
        {
            // Load only the first document when not merging
            mergedDocument = documents[0];
            console.MarkupLine($"[yellow]Warning:[/] Found {fileCount} files, loaded only the first. Use --merge to combine all.");
        }

        RlmSession session = await sessionStore.LoadAsync(settings.SessionId, cancellationToken);
        session.LoadDocument(mergedDocument);
        await sessionStore.SaveAsync(session, settings.SessionId, cancellationToken);

        stopwatch.Stop();
        DisplayDocumentInfo(mergedDocument, stopwatch.Elapsed, fileCount);
        console.MarkupLine($"[green]{fileCount} document(s) loaded successfully.[/]");

        return 0;
    }

    private static RlmDocument MergeDocuments(List<RlmDocument> documents, Uri sourceUri)
    {
        StringBuilder content = new();
        int totalLength = 0;
        int totalLines = 0;
        int totalWords = 0;

        foreach (RlmDocument doc in documents)
        {
            if (content.Length > 0)
            {
                content.AppendLine();
                content.AppendLine("---");
                content.AppendLine();
            }

            content.AppendLine($"# {doc.Id}");
            content.AppendLine();
            content.Append(doc.Content);

            totalLength += doc.Metadata.TotalLength;
            totalLines += doc.Metadata.LineCount;
            totalWords += doc.Metadata.WordCount ?? 0;
        }

        string mergedContent = content.ToString();

        return new RlmDocument
        {
            Id = $"merged-{documents.Count}-documents",
            Content = mergedContent,
            Metadata = new DocumentMetadata
            {
                Source = sourceUri.ToString(),
                TotalLength = mergedContent.Length,
                TokenEstimate = mergedContent.Length / 4,
                LineCount = mergedContent.Split('\n').Length,
                ContentType = "text/markdown",
                WordCount = totalWords > 0 ? totalWords : null
            }
        };
    }

    private void DisplayDocumentInfo(RlmDocument document, TimeSpan elapsed, int? fileCount = null)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("Source", document.Metadata.Source);
        table.AddRow("Length", $"{document.Metadata.TotalLength:N0} chars");
        table.AddRow("Tokens (est)", $"~{document.Metadata.TokenEstimate:N0}");
        table.AddRow("Lines", $"{document.Metadata.LineCount:N0}");

        if (fileCount.HasValue && fileCount > 1)
        {
            table.AddRow("Files", $"{fileCount:N0}");
        }

        if (document.Metadata.ContentType is not null)
        {
            table.AddRow("Content Type", document.Metadata.ContentType);
        }

        if (document.Metadata.Title is not null)
        {
            table.AddRow("Title", document.Metadata.Title);
        }

        if (document.Metadata.Author is not null)
        {
            table.AddRow("Author", document.Metadata.Author);
        }

        if (document.Metadata.PageCount.HasValue)
        {
            table.AddRow("Pages", $"{document.Metadata.PageCount:N0}");
        }

        if (document.Metadata.WordCount.HasValue)
        {
            table.AddRow("Words", $"{document.Metadata.WordCount:N0}");
        }

        if (document.Metadata.HeaderCount.HasValue)
        {
            table.AddRow("Headers", $"{document.Metadata.HeaderCount:N0}");
        }

        if (document.Metadata.CodeBlockCount.HasValue && document.Metadata.CodeBlockCount > 0)
        {
            table.AddRow("Code Blocks", $"{document.Metadata.CodeBlockCount:N0}");

            if (document.Metadata.CodeLanguages?.Count > 0)
            {
                table.AddRow("Languages", string.Join(", ", document.Metadata.CodeLanguages));
            }
        }

        if (document.Metadata.EstimatedReadingTimeMinutes.HasValue)
        {
            table.AddRow("Reading Time", $"~{document.Metadata.EstimatedReadingTimeMinutes} min");
        }

        table.AddRow("Load Time", $"{elapsed.TotalMilliseconds:F0} ms");

        console.Write(table);
    }

    private CompositeDocumentReader CreateCompositeReader()
    {
        // Order matters: more specific readers first, generic FileDocumentReader last
        return new CompositeDocumentReader(
            new StdinDocumentReader(),
            new MarkdownDocumentReader(fileSystem),
            new PdfDocumentReader(fileSystem),
            new HtmlDocumentReader(fileSystem),
            new JsonDocumentReader(fileSystem),
            new WordDocumentReader(fileSystem),
            new FileDocumentReader(fileSystem)  // Fallback for plain text
        );
    }
}
