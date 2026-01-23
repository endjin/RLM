// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Rlm.Cli.Commands;
using Rlm.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.IO;

ServiceCollection services = new();

// Register infrastructure
services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);
services.AddSingleton<IFileSystem, FileSystem>();
services.AddSingleton<IEnvironment>(_ => Spectre.IO.Environment.Shared);
services.AddSingleton<ISessionStore, SessionStore>();

TypeRegistrar registrar = new(services);
CommandApp app = new(registrar);

app.Configure(config =>
{
    config.SetApplicationName("rlm");
    config.SetApplicationVersion("1.0.0");

    // Load command - loads a document into the session
    config.AddCommand<LoadCommand>("load")
        .WithDescription("Load a document into the session")
        .WithExample(["load", "document.txt"])
        .WithExample(["load", "-"]);  // stdin

    // Info command - shows session state
    config.AddCommand<InfoCommand>("info")
        .WithDescription("Show information about the current session");

    // Slice command - view a portion of the document
    config.AddCommand<SliceCommand>("slice")
        .WithDescription("View a slice of the document")
        .WithExample(["slice", "0:1000"])
        .WithExample(["slice", "-500:"]);

    // Chunk command - splits document into chunks
    config.AddCommand<ChunkCommand>("chunk")
        .WithDescription("Split document into chunks using a strategy")
        .WithExample(["chunk", "--strategy", "uniform", "--size", "50000"])
        .WithExample(["chunk", "--strategy", "filter", "--pattern", "email|@"])
        .WithExample(["chunk", "--strategy", "semantic"]);

    // Filter command - shorthand for filter chunking
    config.AddCommand<FilterCommand>("filter")
        .WithDescription("Filter document by regex pattern")
        .WithExample(["filter", "alice|email|@"]);

    // Next command - get next chunk
    config.AddCommand<NextCommand>("next")
        .WithDescription("Get the next chunk from the buffer");

    // Skip command - skip multiple chunks
    config.AddCommand<SkipCommand>("skip")
        .WithDescription("Skip a specified number of chunks")
        .WithExample(["skip", "10"])
        .WithExample(["skip", "-5"])
        .WithExample(["skip", "10", "--skip-empty"]);

    // Jump command - jump to a specific chunk
    config.AddCommand<JumpCommand>("jump")
        .WithDescription("Jump to a specific chunk index or percentage")
        .WithExample(["jump", "50"])
        .WithExample(["jump", "50%"]);

    // Store command - store a partial result
    config.AddCommand<StoreCommand>("store")
        .WithDescription("Store a partial result")
        .WithExample(["store", "chunk_0", "Found: alice@example.com"]);

    // Results command - list stored results
    config.AddCommand<ResultsCommand>("results")
        .WithDescription("List all stored results");

    // Aggregate command - combine all results
    config.AddCommand<AggregateCommand>("aggregate")
        .WithDescription("Combine all stored results for final synthesis");

    // Clear command - reset session
    config.AddCommand<ClearCommand>("clear")
        .WithDescription("Clear the session state");
});

return app.Run(args);