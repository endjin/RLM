---
name: rlm
description: Process and analyze massive documents (PDF, Word, HTML, JSON, Markdown) that exceed context limits. Use when a document is too large to fit in context, when you need to search large files, find specific text in big documents, summarize lengthy content, extract data from large corpora, aggregate information across documents, compare sections, or chunk documents for processing. Triggers on phrases like "too big", "exceeds context", "large document", "chunk this file", "needle in haystack", "exceeds maximum allowed tokens", "File content exceeds", "file too large to read".
allowed-tools: Bash(rlm:*)
metadata:
  author: dotnet-knowledge-base
  version: 2.0.0
license: Apache-2.0
---

# RLM - Recursive Language Model Context Tool

## Prerequisites

> **IMPORTANT:** RLM must be installed before use. Verify installation first:

```bash
which rlm || dotnet tool install -g rlm
```

**Requirements:**
- .NET 10+ runtime

```bash
# Install from NuGet
dotnet tool install -g rlm

# Update to latest version
dotnet tool update -g rlm

# Verify installation
rlm --version
```

## Overview

RLM CLI implements the Data Ingestion Building Blocks pattern for processing documents that exceed your context window. It streams content using IAsyncEnumerable and maintains session state for multi-turn processing.

**Use this skill when:**
- Input exceeds your context window
- You need to find specific information in large documents (needle-in-haystack)
- You need to summarize or aggregate data from massive corpora
- You need to compare sections across large documents

**Key limits:**
- **Max recursion depth:** 5 levels (prevents infinite decomposition)

## Quick Start

```bash
# Ensure RLM is installed, then load document
dotnet tool install -g rlm 2>/dev/null; rlm load document.md
rlm chunk --strategy uniform --size 50000
# Process current chunk, then:
rlm store result_0 "extracted info"
rlm next                              # Get next chunk
rlm store result_1 "more info"
rlm aggregate                         # Combine all results
```

## Documentation

| Topic               | File                                     | Description                                 |
|---------------------|------------------------------------------|---------------------------------------------|
| **Agent Guide**     | [agent-guide.md](agent-guide.md)         | Parallel processing with sub-agents         |
| **Strategies**      | [strategies.md](strategies.md)           | All chunking strategies with decision tree  |
| **Examples**        | [examples.md](examples.md)               | Real-world workflow scenarios               |
| **Reference**       | [reference.md](reference.md)             | Complete command reference and JSON formats |
| **Troubleshooting** | [troubleshooting.md](troubleshooting.md) | Common errors and solutions                 |

## Supported Formats

| Format     | Extension(s)       | Features                                                        |
|------------|--------------------|-----------------------------------------------------------------|
| Markdown   | `.md`, `.markdown` | YAML frontmatter, code blocks, headers                          |
| PDF        | `.pdf`             | Text extraction, page count, title, author                      |
| HTML       | `.html`, `.htm`    | Converts to Markdown, preserves structure                       |
| JSON       | `.json`            | Pretty-prints, element count                                    |
| Word       | `.docx`            | Heading preservation, paragraph extraction, document properties |
| Plain text | `.txt`, etc.       | Basic text loading                                              |

## Core Workflow

### 1. Load Document

```bash
rlm load document.md                    # Single file
rlm load ./docs/                        # Directory (merged)
rlm load ./docs/ --pattern "**/*.md"    # Recursive glob
rlm load ./docs/ --merge false          # Keep separate
cat huge-file.txt | rlm load -          # From stdin
```

### 2. Check Document Info

```bash
rlm info                # Size, tokens, metadata
rlm info --progress     # Processing progress bar
```

### 3. Choose Decomposition Strategy

| Task               | Strategy    | Command                                             |
|--------------------|-------------|-----------------------------------------------------|
| Find specific info | `filter`    | `rlm filter "pattern"`                              |
| Summarize document | `uniform`   | `rlm chunk --strategy uniform --size 50000`         |
| Analyze structure  | `semantic`  | `rlm chunk --strategy semantic`                     |
| Token-precise      | `token`     | `rlm chunk --strategy token --max-tokens 512`       |
| Complex documents  | `recursive` | `rlm chunk --strategy recursive --size 50000`       |
| Unknown task       | `auto`      | `rlm chunk --strategy auto --query "your question"` |

See [strategies.md](strategies.md) for detailed options and selection guide.

### 4. Process Chunks

```bash
rlm store chunk_0 "Finding from first chunk"
rlm next                                    # Get next chunk
rlm store chunk_1 "Finding from second chunk"
# Continue until "No more chunks"
```

### 5. Navigate Efficiently

```bash
rlm skip 10       # Skip forward 10 chunks
rlm skip -5       # Skip backward 5 chunks
rlm jump 50       # Jump to chunk 50 (1-based)
rlm jump 50%      # Jump to 50% position
```

### 6. Aggregate Results

```bash
rlm aggregate                       # Combine all stored results
rlm aggregate --separator "\n---\n" # Custom separator
```

### 7. Clear Session

```bash
rlm clear         # Clear default session
rlm clear --all   # Clear all sessions
```

## Parallel Processing

For documents with 10+ chunks, use parallel processing with sub-agents:

```bash
# 1. Parent initializes with named session
rlm load massive.pdf --session parent
rlm chunk --strategy uniform --size 30000 --session parent

# 2. Parent extracts chunks and spawns workers
rlm next --raw --session parent > chunk_0.txt
# SPAWN: rlm-worker with "Process chunk_0.txt, session=child_0"

# 3. After workers complete, import and aggregate
rlm import "rlm-session-child_*.json" --session parent
rlm aggregate --session parent
```

**Key Rules:**
- Parent uses `--session parent`
- Each worker uses unique `--session child_N`
- Workers store results with key `result`

**Recursive Delegation:** Workers can spawn their own child workers for very large chunks.
See [agent-guide.md](agent-guide.md) for the complete recursive delegation protocol.

## Commands Quick Reference

| Command                    | Description                            |
|----------------------------|----------------------------------------|
| `rlm load <file\|dir\|->`  | Load document(s) into session          |
| `rlm info [--progress]`    | Show document metadata or progress     |
| `rlm slice <range>`        | View section (e.g., `0:1000`, `-500:`) |
| `rlm chunk [--strategy]`   | Apply chunking strategy                |
| `rlm filter <pattern>`     | Filter by regex                        |
| `rlm next [--raw\|--json]` | Get next chunk                         |
| `rlm skip <count>`         | Skip forward/backward                  |
| `rlm jump <index\|%>`      | Jump to chunk index or percentage      |
| `rlm store <key> <value>`  | Store partial result                   |
| `rlm import <glob>`        | Import child session results           |
| `rlm results`              | List stored results                    |
| `rlm aggregate`            | Combine all results                    |
| `rlm clear [--all]`        | Reset session(s)                       |

For complete command options and JSON output formats, see [reference.md](reference.md).

## Best Practices

1. **Start with `info`** - Check document size before choosing strategy
2. **Filter first** - For search tasks, use filter to reduce content
3. **Store incrementally** - Save results after each chunk, not in batches
4. **Navigate efficiently** - Use `skip` and `jump` instead of repeated `next`
5. **Merge small chunks** - Use `--min-size --merge-small` for semantic chunking
6. **Clear between tasks** - Run `rlm clear` when starting fresh

## Permissions

This skill restricts tool access to `Bash(rlm:*)` only - Claude can only execute `rlm` commands when this skill is active.

For common errors and solutions, see [troubleshooting.md](troubleshooting.md).