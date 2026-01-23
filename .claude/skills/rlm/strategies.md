# RLM Chunking Strategies

This document provides detailed information about all available chunking strategies in the RLM CLI.

## Overview

The RLM CLI supports six chunking strategies, each optimized for different use cases:

| Strategy    | Best For                    | Key Options                                |
|-------------|-----------------------------|--------------------------------------------|
| `uniform`   | Aggregation, summarization  | `--size`, `--overlap`                      |
| `filter`    | Needle-in-haystack search   | `--pattern`, `--context`                   |
| `semantic`  | Document structure analysis | `--min-level`, `--max-level`, `--min-size` |
| `token`     | Precise token counting      | `--max-tokens`, `--overlap-tokens`         |
| `recursive` | Complex mixed documents     | `--size`                                   |
| `auto`      | Unknown task type           | `--query`                                  |

## Uniform Strategy

Splits document into fixed-size chunks with optional overlap.

```bash
rlm chunk --strategy uniform --size 50000 --overlap 1000
```

**Options:**
- `--size`: Characters per chunk (default: 50000)
- `--overlap`: Overlap between chunks (default: 0)

**Best for:**
- Summarization tasks
- Counting entities across document
- Aggregating information

## Filter Strategy

Extracts segments matching a regex pattern with surrounding context.

```bash
rlm chunk --strategy filter --pattern "regex" --context 500
# Or shorthand:
rlm filter "regex" --context 500
```

**Options:**
- `--pattern`: Regex pattern to match (required)
- `--context`: Characters around each match (default: 500)

**Best for:**
- Finding specific information (needle-in-haystack)
- Extracting mentions of specific terms
- Locating error messages in logs

**Notes:**
- Automatically merges overlapping segments
- Reduces total content to process significantly

## Semantic Strategy

Splits on Markdown headers, preserving section hierarchy.

```bash
rlm chunk --strategy semantic --min-level 1 --max-level 3
```

**Options:**
- `--min-level`: Minimum header level to split on (default: 1)
- `--max-level`: Maximum header level to split on (default: 3)
- `--min-size`: Minimum chunk size in chars; smaller chunks are merged (default: 0)
- `--max-size`: Maximum chunk size in chars; larger chunks are split (default: 0)
- `--merge-small`: Enable merging of consecutive small sections (use with `--min-size`)

**Best for:**
- Document structure analysis
- Section comparison
- Multi-document analysis with clear headers

### Chunk Merging

Reduce excessive granularity by merging small chunks:

```bash
# Merge small sections to reduce chunk count
rlm chunk --strategy semantic --min-size 5000 --merge-small
# Example: Reduces 521 chunks to 63 chunks for a large document
```

### Hybrid Mode (Semantic + Filter)

Combine semantic structure with pattern filtering:

```bash
# Find pattern within document sections
rlm chunk --strategy semantic --pattern "JetStream"
# Returns only sections matching the pattern, preserving structure

# With chunk merging
rlm chunk --strategy semantic --pattern "JetStream" --min-size 3000 --merge-small
```

## Token Strategy

Splits document into chunks by accurate token count using the GPT-4 tokenizer.

```bash
rlm chunk --strategy token --max-tokens 512 --overlap-tokens 50
```

**Options:**
- `--max-tokens`: Tokens per chunk (default: 512)
- `--overlap-tokens`: Overlap between chunks (default: 50)

**Technical details:**
- Uses `Microsoft.ML.Tokenizers` with GPT-4 tokenizer (cl100k_base)
- Provides accurate token counting for LLM context windows
- Metadata includes `tokenCount` for each chunk

**Best for:**
- LLM context window compliance
- Precise token budgeting
- Model-specific optimization

## Recursive Strategy

Intelligently splits oversized chunks using a separator hierarchy.

```bash
rlm chunk --strategy recursive --size 50000
```

**Options:**
- `--size`: Target characters per chunk (default: 50000)

**Separator hierarchy (7 levels):**
1. `\n## ` - H2 headers
2. `\n### ` - H3 headers
3. `\n#### ` - H4 headers
4. `\n\n` - Paragraph breaks
5. `\n` - Line breaks
6. `. ` - Sentence endings
7. ` ` - Word boundaries

**Behavior:**
- Recursively subdivides oversized chunks
- Force-splits when no separators work (preserves word boundaries)
- Sets `separatorUsed` metadata on each chunk

**Best for:**
- Complex documents with mixed structure
- Documents that don't fit other strategies
- Preserving logical document boundaries

## Auto Strategy

Analyzes your query to automatically select the best strategy.

```bash
rlm chunk --strategy auto --query "find the API key"
```

**Options:**
- `--query`: Query string to analyze (required)
- `--pattern`: Optional regex pattern (triggers hybrid mode selection)

**Keyword scoring:**

| Keywords                                          | Selected Strategy           |
|---------------------------------------------------|-----------------------------|
| find, locate, search, extract, identify           | `filter`                    |
| compare, contrast, sections, structure, hierarchy | `semantic`                  |
| summarize, all, count, list, comprehensive        | `token`                     |
| implement, pattern, example, code, api            | `semantic` (with structure) |

**Additional factors:**
- Document structure (markdown headers boost semantic score)
- If `--pattern` provided with structured content, selects `semantic` for hybrid mode

## Strategy Selection Guide

| Task Type               | Strategy            | When to Use                                             |
|-------------------------|---------------------|---------------------------------------------------------|
| Find specific info      | `filter`            | "What is Alice's email?", "Find the error message"      |
| Summarize all           | `uniform`           | "Summarize the main themes", "What are the key points?" |
| Count/list entities     | `uniform`           | "List all person names", "How many errors?"             |
| Compare sections        | `semantic`          | "Compare conclusions", "Contrast the approaches"        |
| Multi-document analysis | `semantic`          | Document has clear header structure                     |
| Reduce chunk count      | `semantic` + merge  | Too many small chunks; use `--min-size --merge-small`   |
| Find in structure       | `semantic` + filter | Find pattern within document sections (hybrid mode)     |
| Model context limits    | `token`             | Precise token counting for LLM context windows          |
| Complex documents       | `recursive`         | Mixed content, preserve structure with smart splitting  |
| Unknown task type       | `auto`              | Let CLI analyze query and pick best strategy            |

## Related Documentation

- [SKILL.md](SKILL.md) - Overview and workflow
- [examples.md](examples.md) - Real-world workflow scenarios
- [reference.md](reference.md) - JSON output formats and session file
- [troubleshooting.md](troubleshooting.md) - Tips and error handling
