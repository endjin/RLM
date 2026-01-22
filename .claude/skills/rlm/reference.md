# RLM CLI Technical Reference

## Architecture

The RLM CLI implements the Data Ingestion Building Blocks pattern from `dotnet-data-ingestion-guidelines.md` combined with the Recursive Language Models specification from `recursive-language-models-specification.md`.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     RLM Pipeline (Data Ingestion Pattern)                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌──────────────┐    ┌──────────────────┐    ┌─────────────────┐            │
│  │   Document   │    │    Document      │    │   Ingestion     │            │
│  │   Sources    │───▶│    Reader        │───▶│   Document      │            │
│  │  (files,     │    │  (rlm load)      │    │  (Content +     │            │
│  │   stdin)     │    │                  │    │   metadata)     │            │
│  └──────────────┘    └──────────────────┘    └────────┬────────┘            │
│                                                        │                    │
│                                                        ▼                    │
│  ┌──────────────┐    ┌──────────────────┐    ┌─────────────────┐            │
│  │   Session    │◀───│    Result        │◀───│    Chunker      │            │
│  │   Store      │    │    Buffer        │    │  (rlm chunk/    │            │
│  │  (persist)   │    │  (rlm store)     │    │   filter)       │            │
│  └──────────────┘    └──────────────────┘    └─────────────────┘            │
│         │                                            │                      │
│         ▼                                            ▼                      │
│  ┌─────────────────────────────────────────────────────────────┐            │
│  │           IAsyncEnumerable<ContentChunk>                     │           │
│  │      (Streaming chunks to Claude for processing)            │            │
│  └─────────────────────────────────────────────────────────────┘            │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Key Patterns

### From Data Ingestion Guidelines

| Data Ingestion Pattern          | RLM CLI Implementation                                |
|---------------------------------|-------------------------------------------------------|
| `IngestionDocument`             | `RlmDocument` with Markdown content + metadata        |
| `IngestionDocumentReader`       | `IDocumentReader` for file/stdin loading              |
| `IngestionChunker<T>`           | `IChunker` with filtering/uniform/semantic strategies |
| `IAsyncEnumerable<T>` streaming | Streaming chunks for memory efficiency                |
| Pipeline composition            | Composable reader → chunker → output                  |
| Metadata preservation           | Section headers, positions in chunk metadata          |

### From RLM Specification

| RLM Concept              | CLI Implementation                                             |
|--------------------------|----------------------------------------------------------------|
| ContextVariable          | Session with loaded document content                           |
| LLMQuery                 | Claude processes output, stores result via `rlm store`         |
| OutputBuffer             | `ResultBuffer` for accumulating partial results                |
| FINAL(answer)            | `rlm aggregate --final` with FINAL signal                      |
| Decomposition strategies | `--strategy uniform\|filter\|semantic\|token\|recursive\|auto` |
| RecursionDepth           | Session tracks depth, max 5 to prevent infinite loops          |

## Core Abstractions

### RlmDocument
```csharp
public sealed class RlmDocument
{
    public required string Id { get; init; }
    public required string Content { get; init; }
    public required DocumentMetadata Metadata { get; init; }
}

public sealed record DocumentMetadata
{
    // Core fields
    public required string Source { get; init; }
    public required int TotalLength { get; init; }
    public required int TokenEstimate { get; init; }
    public required int LineCount { get; init; }
    public DateTimeOffset LoadedAt { get; init; }

    // Format-specific fields
    public string? ContentType { get; init; }        // MIME type (e.g., "text/markdown")
    public string? OriginalFormat { get; init; }     // Format before conversion (e.g., "text/html")
    public string? Title { get; init; }              // Document title
    public string? Author { get; init; }             // Document author
    public int? PageCount { get; init; }             // PDF page count
    public int? ElementCount { get; init; }          // JSON element count

    // Content analysis fields
    public int? WordCount { get; init; }
    public int? HeaderCount { get; init; }
    public int? CodeBlockCount { get; init; }
    public IReadOnlyList<string>? CodeLanguages { get; init; }  // Detected languages
    public int? EstimatedReadingTimeMinutes { get; init; }

    // Extensible metadata (e.g., YAML frontmatter)
    public IReadOnlyDictionary<string, string>? ExtendedMetadata { get; init; }
}
```

### ContentChunk
```csharp
public sealed record ContentChunk
{
    public required int Index { get; init; }
    public required string Content { get; init; }
    public required int StartPosition { get; init; }
    public required int EndPosition { get; init; }
    public IReadOnlyDictionary<string, object?> Metadata { get; init; }
}
```

### IChunker
```csharp
public interface IChunker
{
    IAsyncEnumerable<ContentChunk> ChunkAsync(
        RlmDocument document,
        CancellationToken cancellationToken = default);
}
```

## Document Readers

The CLI supports multiple document formats through specialized readers:

### IDocumentReader
```csharp
public interface IDocumentReader
{
    bool CanRead(string source);
    Task<RlmDocument> ReadAsync(string source, CancellationToken ct = default);
    IAsyncEnumerable<RlmDocument> ReadManyAsync(string source, string? pattern = null, CancellationToken ct = default);
}
```

### Format-Specific Readers

| Reader                    | Extensions         | Features                                                            |
|---------------------------|--------------------|---------------------------------------------------------------------|
| `MarkdownDocumentReader`  | `.md`, `.markdown` | YAML frontmatter extraction, code block detection, language hints   |
| `PdfDocumentReader`       | `.pdf`             | Text extraction with PdfPig, page count, title, author metadata     |
| `HtmlDocumentReader`      | `.html`, `.htm`    | HTML→Markdown conversion using ReverseMarkdown, preserves structure |
| `JsonDocumentReader`      | `.json`            | Pretty-printing, element count, structure preservation              |
| `WordDocumentReader`      | `.docx`            | Paragraph extraction with OpenXml, document properties              |
| `PlainTextDocumentReader` | `.txt`, others     | Basic text loading with encoding detection                          |

### Directory Loading

Use `ReadManyAsync` to load multiple documents from a directory:

```csharp
// Load all documents
await foreach (var doc in reader.ReadManyAsync("./docs/"))
{
    // Process each document
}

// Load with glob pattern
await foreach (var doc in reader.ReadManyAsync("./docs/", "*.md"))
{
    // Only markdown files
}
```

CLI usage:
```bash
rlm load ./docs/                    # All supported formats
rlm load ./docs/ --pattern "*.md"   # Only markdown
rlm load ./docs/ --merge false      # Keep documents separate
```

## Validation Framework

Documents are validated before processing using a composable validator chain:

### IDocumentValidator
```csharp
public interface IDocumentValidator
{
    ValidationResult Validate(RlmDocument document);
}

public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
```

### Built-in Validators

| Validator            | Checks                                                         |
|----------------------|----------------------------------------------------------------|
| `SyntacticValidator` | Binary content detection, UTF-8 encoding, balanced code blocks |
| `RangeValidator`     | Size limits (default 5MB), line count limits                   |
| `CompositeValidator` | Chains multiple validators together                            |

### Validation Errors

| Error                     | Cause                            | Solution                     |
|---------------------------|----------------------------------|------------------------------|
| "Binary content detected" | File contains binary data        | Use text-based files only    |
| "File exceeds size limit" | File larger than 5MB             | Split file or increase limit |
| "Invalid UTF-8 encoding"  | File has encoding issues         | Convert to UTF-8             |
| "Unbalanced code blocks"  | Markdown code fences don't match | Fix markdown syntax          |

## Chunk Processor Framework

Post-processing enrichment for chunks:

### IChunkProcessor
```csharp
public interface IChunkProcessor
{
    Task<ContentChunk> ProcessAsync(ContentChunk chunk, CancellationToken ct = default);
}
```

### Built-in Processors

| Processor                  | Enrichment                               |
|----------------------------|------------------------------------------|
| `ChunkStatisticsProcessor` | Word count, line count, character count  |
| `ChunkProcessorChain`      | Composes multiple processors in sequence |

## Chunking Strategies

### UniformChunker
- Splits document into fixed-size chunks with optional overlap
- Best for: aggregation tasks, summarization, counting entities
- Parameters: `chunkSize`, `overlap`

### FilteringChunker
- Extracts segments matching a regex pattern with surrounding context
- Best for: needle-in-haystack, finding specific information
- Parameters: `pattern`, `contextSize`
- Automatically merges overlapping segments

### SemanticChunker
- Splits on Markdown headers, preserving section hierarchy
- Best for: document structure analysis, section comparison
- Parameters: `minLevel`, `maxLevel` (header levels 1-6)
- Includes `headerPath` in metadata (e.g., "Overview > Installation")

### TokenBasedChunker
- Splits document into chunks by accurate token count
- Uses `Microsoft.ML.Tokenizers` with TiktokenTokenizer (cl100k_base/GPT-4)
- Best for: LLM context window compliance, precise token budgeting
- Parameters: `maxTokens` (default: 512), `overlapTokens` (default: 50)
- Metadata: `tokenCount` (actual token count per chunk)

### RecursiveChunker
- Intelligently splits oversized chunks using separator hierarchy
- 7-level hierarchy: `\n## ` → `\n### ` → `\n#### ` → `\n\n` → `\n` → `. ` → ` `
- Best for: complex documents with mixed structure
- Parameters: `targetSize` (characters per chunk)
- Recursively subdivides until all chunks are under target size
- Force-splits when no separators work (preserves word boundaries)
- Metadata: `separatorUsed` (which separator split this chunk)

### AutoChunker
- Analyzes query to automatically select best strategy
- Keyword mapping:
  - "find", "locate", "search" → `FilteringChunker`
  - "compare", "contrast", "sections" → `SemanticChunker`
  - "summarize", "all", "count" → `TokenBasedChunker`
- Also considers document structure (presence of markdown headers)
- Requires `query` parameter

## Session State

The session is persisted to `~/.rlm-session.json` and includes:
- `Content`: The loaded document text
- `Metadata`: Document metadata (source, length, token estimate)
- `ChunkBuffer`: List of chunks from last chunking operation
- `CurrentChunkIndex`: Current position in chunk buffer
- `Results`: Dictionary of stored partial results
- `RecursionDepth`: Current depth in nested RLM calls (reset by `rlm clear`)

## Token Estimation

The CLI estimates tokens as `characters / 4` for basic info display. This is a rough approximation:
- Actual tokenization varies by model and content
- Use for guidance, not precise limits
- For accurate token counting, use `--strategy token` which uses `Microsoft.ML.Tokenizers` with the GPT-4 tokenizer (cl100k_base)

## JSON Output Models

The `--json` flag enables machine-parsable output using these record types:

### SessionInfoOutput
```csharp
public sealed record SessionInfoOutput(
    string Source,
    int TotalLength,
    int TokenEstimate,
    int LineCount,
    int ChunkCount,
    int CurrentChunkIndex,
    int ResultCount);
```

### ChunkOutput
```csharp
public sealed record ChunkOutput(
    int Index,
    string Content,
    int StartPosition,
    int EndPosition,
    IReadOnlyDictionary<string, object?>? Metadata);
```

### AggregateOutput
```csharp
public sealed record AggregateOutput(
    string Content,
    int ResultCount,
    string Signal);  // "FINAL" or "PARTIAL"
```

The `--final` flag sets `Signal` to "FINAL" (default: "PARTIAL"). In text mode, `--final` wraps output with `FINAL(...)`.

## Recursion Depth Tracking

To prevent infinite decomposition loops:
- `MaxRecursionDepth = 5` constant limits nested RLM calls
- Session tracks `RecursionDepth` property
- `rlm clear` resets depth to 0
- Exceeding depth returns error instead of processing

## Error Handling

| Error                     | Cause                | Solution                      |
|---------------------------|----------------------|-------------------------------|
| "No document loaded"      | `load` not called    | Run `rlm load <file>` first   |
| "No chunks available"     | `chunk` not called   | Run `rlm chunk` after loading |
| "No more chunks"          | All chunks processed | Use `rlm aggregate`           |
| "Cannot read source"      | File not found       | Check file path               |
| "Filter requires pattern" | Missing `--pattern`  | Add pattern argument          |

## Project Structure

```
Solutions/Rlm.Cli/
├── Program.cs                 # CLI entry point
├── Commands/
│   ├── LoadCommand.cs         # Load document
│   ├── InfoCommand.cs         # Show session info
│   ├── SliceCommand.cs        # View document slice
│   ├── ChunkCommand.cs        # Apply chunking
│   ├── FilterCommand.cs       # Shorthand for filter chunking
│   ├── NextCommand.cs         # Get next chunk
│   ├── SkipCommand.cs         # Skip chunks forward/backward
│   ├── JumpCommand.cs         # Jump to chunk index
│   ├── StoreCommand.cs        # Store partial result
│   ├── ResultsCommand.cs      # List stored results
│   ├── AggregateCommand.cs    # Combine results
│   └── ClearCommand.cs        # Reset session
├── Core/
│   ├── Documents/
│   │   ├── RlmDocument.cs
│   │   ├── DocumentMetadata.cs
│   │   ├── IDocumentReader.cs
│   │   ├── CompositeDocumentReader.cs
│   │   └── Readers/
│   │       ├── MarkdownDocumentReader.cs
│   │       ├── PdfDocumentReader.cs
│   │       ├── HtmlDocumentReader.cs
│   │       ├── JsonDocumentReader.cs
│   │       ├── WordDocumentReader.cs
│   │       ├── PlainTextDocumentReader.cs
│   │       └── StdinDocumentReader.cs
│   ├── Validation/
│   │   ├── IDocumentValidator.cs
│   │   ├── ValidationResult.cs
│   │   ├── SyntacticValidator.cs
│   │   ├── RangeValidator.cs
│   │   └── CompositeValidator.cs
│   ├── Chunking/
│   │   ├── ContentChunk.cs
│   │   ├── IChunker.cs
│   │   ├── UniformChunker.cs
│   │   ├── FilteringChunker.cs
│   │   ├── SemanticChunker.cs
│   │   ├── TokenBasedChunker.cs
│   │   ├── RecursiveChunker.cs
│   │   └── AutoChunker.cs
│   ├── Processing/
│   │   ├── IChunkProcessor.cs
│   │   ├── ChunkStatisticsProcessor.cs
│   │   └── ChunkProcessorChain.cs
│   ├── Output/
│   │   └── JsonOutput.cs
│   └── Session/
│       ├── RlmSession.cs
│       └── ResultBuffer.cs
└── Infrastructure/
    ├── SessionStore.cs
    ├── RlmJsonContext.cs
    └── TypeRegistrar.cs
```

## Dependencies

### Core
- `Spectre.Console` - Rich console output
- `Spectre.Console.Cli` - Command-line framework
- `Spectre.IO` - Testable file system operations
- `Microsoft.Extensions.DependencyInjection` - DI container
- `Polly` 8.6.0 - Retry resilience for file operations

### Tokenization
- `Microsoft.ML.Tokenizers` 2.0.0 - Accurate tokenization for token-based chunking
- `Microsoft.ML.Tokenizers.Data.Cl100kBase` 2.0.0 - GPT-4 tokenizer data (cl100k_base)

### Document Formats
- `PdfPig` - PDF text extraction and metadata
- `ReverseMarkdown` - HTML to Markdown conversion
- `DocumentFormat.OpenXml` - Word document (.docx) processing

## Building

```bash
cd src/Rlm.Cli
dotnet build

# Run directly
dotnet run -- load document.txt

# Install as tool
dotnet pack
dotnet tool install --global --add-source ./nupkg rlm
```

## Related Skill Documentation

| Topic           | File                                     | Description                          |
|-----------------|------------------------------------------|--------------------------------------|
| Overview        | [SKILL.md](SKILL.md)                     | Quick start and workflow             |
| Strategies      | [strategies.md](strategies.md)           | All chunking strategies with options |
| Examples        | [examples.md](examples.md)               | Real-world workflow scenarios        |
| Troubleshooting | [troubleshooting.md](troubleshooting.md) | Tips, errors, and edge cases         |
