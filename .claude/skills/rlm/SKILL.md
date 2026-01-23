---
name: rlm
description: Process queries against massive text inputs using RLM decomposition. Use when analyzing documents exceeding context limits, finding information in large corpora (needle-in-haystack), summarizing massive documents, or aggregating data across large datasets.
allowed-tools: Bash(rlm:*)
metadata:
  author: dotnet-knowledge-base
  version: 1.1.0
license: Apache-2.0
---

# RLM - Recursive Language Model Context Tool

## Overview

RLM CLI implements the Data Ingestion Building Blocks pattern for processing documents that exceed your context window. It streams content using IAsyncEnumerable and maintains session state for multi-turn processing.

Use this skill when:
- Input exceeds your context window
- You need to find specific information in large documents (needle-in-haystack)
- You need to summarize or aggregate data from massive corpora
- You need to compare sections across large documents

## Documentation

| Topic               | File                                     | Description                          |
|---------------------|------------------------------------------|--------------------------------------|
| Strategy Deep-Dive  | [strategies.md](strategies.md)           | All chunking strategies with options |
| Examples            | [examples.md](examples.md)               | Real-world workflow scenarios        |
| Technical Reference | [reference.md](reference.md)             | JSON output formats and session file |
| Troubleshooting     | [troubleshooting.md](troubleshooting.md) | Tips, errors, and edge cases         |

## Supported Formats

| Format     | Extension(s)       | Features                                        |
|------------|--------------------|-------------------------------------------------|
| Markdown   | `.md`, `.markdown` | YAML frontmatter, code blocks, headers          |
| PDF        | `.pdf`             | Text extraction, page count, title, author      |
| HTML       | `.html`, `.htm`    | Converts to Markdown, preserves structure       |
| JSON       | `.json`            | Pretty-prints, element count                    |
| Word       | `.docx`            | Paragraph extraction, document properties       |
| Plain text | `.txt`, etc.       | Basic text loading                              |

## Workflow

### 1. Load Document
```bash
# Load single file
rlm load document.md
# Output: Loaded 10,485,760 chars, ~2,621,440 tokens (150ms)

# Load directory (merges all documents by default)
rlm load ./docs/

# Recursively load all markdown files from a directory with glob pattern
rlm load ./docs/ --pattern "**/*.md"

# Load without merging (keeps documents separate)
rlm load ./docs/ --merge false

# Load specific formats
rlm load report.pdf      # PDF with text extraction
rlm load page.html       # HTML converted to Markdown
rlm load data.json       # JSON pretty-printed
rlm load report.docx     # Word document

# Load from stdin
cat huge-file.txt | rlm load -
```

### 2. Check Document Info
```bash
rlm info
# Shows: source, length, token estimate, lines, chunks, results
# Enhanced metadata (when available):
#   Content-Type: text/markdown
#   Title: Document Title
#   Author: Author Name
#   Pages: 42 (PDF)
#   Words: 15,234
#   Headers: 28
#   Code Blocks: 12 (C#, TypeScript, Bash)
#   Reading Time: ~61 minutes
```

### 3. View Document Slices
```bash
rlm slice 0:1000      # First 1000 chars
rlm slice -500:       # Last 500 chars
rlm slice 1000:2000   # Middle section
```

### 4. Choose Decomposition Strategy

**Filtering (needle-in-haystack):**
```bash
rlm filter "alice|email|@"
# Returns matching sections with surrounding context
```

**Uniform (aggregation/summary):**
```bash
rlm chunk --strategy uniform --size 50000
# Returns first chunk, stores all in buffer
```

**Semantic (document structure):**
```bash
rlm chunk --strategy semantic
# Splits on Markdown headers, preserves hierarchy
```

**Token-Based (accurate token counting):**
```bash
rlm chunk --strategy token --max-tokens 512
# Accurate token counting using GPT-4 tokenizer
```

**Recursive (intelligent splitting):**
```bash
rlm chunk --strategy recursive --size 50000
# Splits using 7-level separator hierarchy
```

**Auto (query-based selection):**
```bash
rlm chunk --strategy auto --query "find the API key"
# Automatically selects best strategy based on query
```

See [strategies.md](strategies.md) for detailed options and selection guide.

### 5. Process Chunks Iteratively

```bash
# Process first chunk (shown after chunking)
rlm store chunk_0 "Found: alice@example.com in line 42"

# Get next chunk
rlm next
rlm store chunk_1 "Found: bob@example.com in line 1523"

# Continue until "No more chunks"
```

### 5a. Navigate Chunks Efficiently

```bash
rlm skip 10           # Skip forward 10 chunks
rlm skip -5           # Skip backward 5 chunks
rlm jump 50           # Jump to chunk 50 (1-based)
rlm jump 50%          # Jump to 50% position
```

### 5b. Check Processing Progress

```bash
rlm info --progress
# Output:
# Progress: ███████████████░░░░░░░░░░░░░░░ 50.8%
# Chunks: 32 / 63 (31 remaining)
# Results: 5 stored
```

### 6. Aggregate Results
```bash
rlm aggregate
# Output: Combined results for final synthesis

# With custom separator
rlm aggregate --separator "\n---\n"
```

### 7. Clear Session
```bash
rlm clear
# Resets document, chunks, and results
```

## Commands Reference

| Command                 | Description                | Example                        |
|-------------------------|----------------------------|--------------------------------|
| `rlm load <file>`       | Load document into session | `rlm load corpus.txt`          |
| `rlm load <dir>`        | Load directory of docs     | `rlm load ./docs/`             |
| `rlm load - `           | Load from stdin            | `cat file.txt \| rlm load -`   |
| `rlm info`              | Show document metadata     | `rlm info`                     |
| `rlm info --progress`   | Show processing progress   | `rlm info --progress`          |
| `rlm slice <range>`     | View section               | `rlm slice 0:1000`             |
| `rlm chunk [opts]`      | Apply chunking strategy    | `rlm chunk --strategy uniform` |
| `rlm filter <pattern>`  | Filter by regex            | `rlm filter "email\|@"`        |
| `rlm next`              | Get next chunk             | `rlm next`                     |
| `rlm skip <count>`      | Skip forward/backward      | `rlm skip 10`, `rlm skip -5`   |
| `rlm jump <index>`      | Jump to chunk index or %   | `rlm jump 50`, `rlm jump 50%`  |
| `rlm store <key> <val>` | Store partial result       | `rlm store chunk_0 "result"`   |
| `rlm results`           | List stored results        | `rlm results`                  |
| `rlm aggregate`         | Combine all results        | `rlm aggregate`                |
| `rlm clear`             | Reset session              | `rlm clear`                    |

### Load Options

| Option      | Description                          | Example                         |
|-------------|--------------------------------------|---------------------------------|
| `--pattern` | Glob pattern for directory loading   | `--pattern "**/*"`              |
| `--merge`   | Merge multiple documents (default: true) | `--merge false`             |

### JSON Output Options

| Command                 | Description                  |
|-------------------------|------------------------------|
| `rlm info --json`       | JSON output for session info |
| `rlm next --json`       | JSON output for chunk        |
| `rlm aggregate --json`  | JSON output for aggregate    |
| `rlm aggregate --final` | Add completion signal        |

## Quick Reference

### Strategy Selection

| Task                   | Strategy    |
|------------------------|-------------|
| Find specific info     | `filter`    |
| Summarize document     | `uniform`   |
| Analyze structure      | `semantic`  |
| Token-precise chunking | `token`     |
| Complex documents      | `recursive` |
| Unknown task           | `auto`      |

### Session Persistence

The session is stored at `~/.rlm-session.json`. Clear with `rlm clear` when starting a new task.

See [troubleshooting.md](troubleshooting.md) for tips and common errors.
