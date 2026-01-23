# RLM CLI (Recursive Language Model Context Tool)

A .NET CLI tool for processing large documents that exceed LLM context windows. RLM implements a data ingestion pattern, enabling streaming content decomposition, multi-turn processing, and result aggregation.

## Key Features

- **Multi-format support** - Markdown, PDF, HTML, JSON, Word (.docx), plain text
- **6 chunking strategies** - Uniform, filtering, semantic, token-based, recursive, auto
- **Stateful sessions** - Persistent session state for multi-turn processing
- **AOT-compatible** - Native ahead-of-time compilation support
- **Streaming architecture** - `IAsyncEnumerable<T>` for memory-efficient processing

## Projects in Solution

| Project           | Description                          | Framework                         |
|-------------------|--------------------------------------|-----------------------------------|
| **Rlm.Cli**       | Console application with 12 commands | .NET 10, AOT-compatible           |
| **Rlm.Cli.Tests** | Unit tests                           | MSTest 4.0, Shouldly, NSubstitute |

## Quick Start

### Build and Run

```bash
# Build
dotnet build Solutions/Rlm.slnx

# Run directly
dotnet run --project Solutions/Rlm.Cli -- load document.md

# Run tests
dotnet test --solution Solutions/Rlm.slnx
```

### Install as Global Tool

```bash
cd Solutions/Rlm.Cli
dotnet pack
dotnet tool install --global --add-source ./bin/Release rlm
```

### Basic Workflow

```bash
# Load a document
rlm load large-document.md

# Check document info
rlm info

# Chunk for processing
rlm chunk --strategy uniform --size 50000

# Process chunks iteratively
rlm next                          # Get next chunk
rlm store chunk_0 "result..."     # Store partial result
rlm next                          # Continue until done

# Aggregate results
rlm aggregate
```

## Supported Formats

| Format     | Extensions         | Features                                   |
|------------|--------------------|--------------------------------------------|
| Markdown   | `.md`, `.markdown` | YAML frontmatter, code blocks, headers     |
| PDF        | `.pdf`             | Text extraction, page count, title, author |
| HTML       | `.html`, `.htm`    | Converts to Markdown, preserves structure  |
| JSON       | `.json`            | Pretty-printing, element count             |
| Word       | `.docx`            | Paragraph extraction, document properties  |
| Plain text | `.txt`, etc.       | Basic text loading                         |

## Commands Reference

| Command             | Description                 | Example                         |
|---------------------|-----------------------------|---------------------------------|
| `load <file>`       | Load document into session  | `rlm load corpus.txt`           |
| `load <dir>`        | Load directory of documents | `rlm load ./docs/`              |
| `load -`            | Load from stdin             | `cat file.txt \| rlm load -`    |
| `info`              | Show document metadata      | `rlm info --progress`           |
| `slice <range>`     | View document section       | `rlm slice 0:1000`              |
| `chunk [opts]`      | Apply chunking strategy     | `rlm chunk --strategy semantic` |
| `filter <pattern>`  | Filter by regex pattern     | `rlm filter "email\|@"`         |
| `next`              | Get next chunk              | `rlm next --json`               |
| `skip <count>`      | Skip forward/backward       | `rlm skip 10`                   |
| `jump <index>`      | Jump to chunk index or %    | `rlm jump 50%`                  |
| `store <key> <val>` | Store partial result        | `rlm store chunk_0 "result"`    |
| `results`           | List stored results         | `rlm results`                   |
| `aggregate`         | Combine all results         | `rlm aggregate --final`         |
| `clear`             | Reset session               | `rlm clear`                     |

## Chunking Strategies

| Strategy        | Use Case                    | Command                                            |
|-----------------|-----------------------------|----------------------------------------------------|
| **Uniform**     | Summarization, aggregation  | `rlm chunk --strategy uniform --size 50000`        |
| **Filtering**   | Needle-in-haystack search   | `rlm filter "pattern"`                             |
| **Semantic**    | Document structure analysis | `rlm chunk --strategy semantic`                    |
| **Token-based** | Precise token budgeting     | `rlm chunk --strategy token --max-tokens 512`      |
| **Recursive**   | Complex mixed documents     | `rlm chunk --strategy recursive`                   |
| **Auto**        | Query-based selection       | `rlm chunk --strategy auto --query "find API key"` |

## Architecture Overview

```
Solutions/Rlm.Cli/
├── Commands/          # 12 CLI commands (load, chunk, next, etc.)
├── Core/
│   ├── Documents/     # Multi-format readers (PDF, HTML, Word, etc.)
│   ├── Chunking/      # 6 chunking strategies
│   ├── Validation/    # Input validation framework
│   ├── Processing/    # Chunk post-processors
│   ├── Output/        # JSON output models
│   └── Session/       # Persistent state management
└── Infrastructure/    # Session store, DI, JSON context
```

## Dependencies

### CLI Framework
- Spectre.Console / Spectre.Console.Cli - Rich console output and command parsing
- Spectre.IO - Testable file system operations

### Document Processing
- PdfPig - PDF text extraction
- ReverseMarkdown - HTML to Markdown conversion
- DocumentFormat.OpenXml - Word document processing

### Tokenization
- Microsoft.ML.Tokenizers - Accurate GPT-4 tokenization (cl100k_base)

### Infrastructure
- Microsoft.Extensions.DependencyInjection - DI container
- Polly - Retry resilience for file operations

## Documentation

Detailed documentation is available in `.claude/skills/rlm/`:

| Document                                                    | Description                            |
|-------------------------------------------------------------|----------------------------------------|
| [SKILL.md](.claude/skills/rlm/SKILL.md)                     | Overview and workflow guide            |
| [reference.md](.claude/skills/rlm/reference.md)             | Technical architecture and JSON models |
| [examples.md](.claude/skills/rlm/examples.md)               | Real-world workflow scenarios          |
| [strategies.md](.claude/skills/rlm/strategies.md)           | Chunking strategies deep-dive          |
| [troubleshooting.md](.claude/skills/rlm/troubleshooting.md) | Tips, errors, and edge cases           |

## License

Apache-2.0