# RLM Chunking Strategies

Complete guide to chunking strategies with decision tree for selection.

## Quick Decision Tree

```
What do you need to do?
│
├── Find specific information? ──────────────────────► filter
│   "Find Alice's email", "Locate the error"
│
├── Summarize or count everything? ──────────────────► uniform
│   "Summarize the report", "List all names"
│
├── Analyze document structure? ─────────────────────► semantic
│   "Compare sections", "Analyze the hierarchy"
│
├── Need precise token control? ─────────────────────► token
│   "Fit within 4096 tokens", "Token budget"
│
├── Complex mixed content? ──────────────────────────► recursive
│   Code + prose, tables + text
│
└── Not sure? ───────────────────────────────────────► auto
    Let the CLI analyze your query
```

## Strategy Overview

| Strategy    | Best For                    | Key Options                                |
|-------------|-----------------------------|--------------------------------------------|
| `filter`    | Needle-in-haystack search   | `--pattern`, `--context`                   |
| `uniform`   | Aggregation, summarization  | `--size`, `--overlap`                      |
| `semantic`  | Document structure analysis | `--min-level`, `--max-level`, `--min-size` |
| `token`     | Precise token counting      | `--max-tokens`, `--overlap-tokens`         |
| `recursive` | Complex mixed documents     | `--size`                                   |
| `auto`      | Unknown task type           | `--query`                                  |

---

## Filter Strategy

Extracts segments matching a regex pattern with surrounding context.

**Best for:** Finding specific information (needle-in-haystack)

```bash
rlm filter "pattern" --context 500
# Or: rlm chunk --strategy filter --pattern "pattern" --context 500
```

**Options:**

| Option      | Description                  | Default    |
|-------------|------------------------------|------------|
| `--pattern` | Regex pattern to match       | (required) |
| `--context` | Characters around each match | `500`      |

**Use cases:**
- Finding email addresses or names
- Locating error messages in logs
- Extracting configuration values
- Searching for specific terms

**Behavior:**
- Automatically merges overlapping segments
- Significantly reduces content to process
- Preserves context around matches

**Example:**

```bash
rlm load server-logs.txt
rlm filter "ERROR|CRITICAL|exception"
# Returns only matching segments with context
```

---

## Uniform Strategy

Splits document into fixed-size chunks with optional overlap.

**Best for:** Summarization, counting, aggregation

```bash
rlm chunk --strategy uniform --size 50000 --overlap 1000
```

**Options:**

| Option      | Description            | Default |
|-------------|------------------------|---------|
| `--size`    | Characters per chunk   | `50000` |
| `--overlap` | Overlap between chunks | `0`     |

**Use cases:**
- Summarizing long reports
- Counting entities across documents
- Aggregating information
- Processing without structure awareness

**Example:**

```bash
rlm load annual-report.txt
rlm chunk --strategy uniform --size 40000
# Process each chunk for key points
```

---

## Semantic Strategy

Splits on Markdown headers, preserving section hierarchy.

**Best for:** Structured document analysis

```bash
rlm chunk --strategy semantic --min-level 1 --max-level 3
```

**Options:**

| Option          | Description                                | Default |
|-----------------|--------------------------------------------|---------|
| `--min-level`   | Minimum header level to split on           | `1`     |
| `--max-level`   | Maximum header level to split on           | `3`     |
| `--min-size`    | Minimum chunk size (smaller chunks merged) | `0`     |
| `--max-size`    | Maximum chunk size (larger chunks split)   | `0`     |
| `--merge-small` | Merge consecutive small sections           | `false` |
| `--pattern`     | Filter pattern for hybrid mode             | -       |

**Use cases:**
- Comparing document sections
- Building table of contents
- Understanding document organization
- Multi-document analysis with clear headers

### Chunk Merging

Reduce excessive granularity with merging:

```bash
rlm chunk --strategy semantic --min-size 5000 --merge-small
# Reduces 521 chunks to ~63 chunks
```

### Hybrid Mode (Semantic + Filter)

Combine structure preservation with pattern filtering:

```bash
rlm chunk --strategy semantic --pattern "authentication|OAuth"
# Returns only sections matching pattern, preserving structure

rlm chunk --strategy semantic --pattern "API" --min-size 3000 --merge-small
# Filter + merge for targeted analysis
```

---

## Token Strategy

Splits by accurate token count using GPT-4 tokenizer (cl100k_base).

**Best for:** LLM context window compliance

```bash
rlm chunk --strategy token --max-tokens 512 --overlap-tokens 50
```

**Options:**

| Option             | Description            | Default |
|--------------------|------------------------|---------|
| `--max-tokens`     | Tokens per chunk       | `512`   |
| `--overlap-tokens` | Overlap between chunks | `50`    |

**Use cases:**
- Precise token budgeting
- Model context window limits
- Embedding generation
- Token-sensitive processing

**Technical details:**
- Uses `Microsoft.ML.Tokenizers` with GPT-4 tokenizer
- Chunk metadata includes `tokenCount` field
- More accurate than character-based estimation

---

## Recursive Strategy

Intelligently splits oversized chunks using a separator hierarchy.

**Best for:** Complex documents with mixed content

```bash
rlm chunk --strategy recursive --size 50000
```

**Options:**

| Option   | Description                 | Default |
|----------|-----------------------------|---------|
| `--size` | Target characters per chunk | `50000` |

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
- Force-splits when no separators work
- Preserves word boundaries
- Sets `separatorUsed` in chunk metadata

**Use cases:**
- Documents mixing code and prose
- Content that doesn't fit other strategies
- Preserving logical boundaries automatically

---

## Auto Strategy

Analyzes your query to automatically select the best strategy.

**Best for:** When you're unsure which strategy to use

```bash
rlm chunk --strategy auto --query "find the API key"
```

**Options:**

| Option      | Description             | Default    |
|-------------|-------------------------|------------|
| `--query`   | Query string to analyze | (required) |
| `--pattern` | Optional regex pattern  | -          |

**Keyword scoring:**

| Keywords                                          | Selected Strategy |
|---------------------------------------------------|-------------------|
| find, locate, search, extract, identify           | `filter`          |
| compare, contrast, sections, structure, hierarchy | `semantic`        |
| summarize, all, count, list, comprehensive        | `token`           |
| implement, pattern, example, code, api            | `semantic`        |

**Additional factors:**
- Document structure (markdown headers boost semantic score)
- If `--pattern` provided with structured content, selects `semantic` for hybrid mode

---

## Strategy Selection Guide

| Task                | Strategy            | Example Query                 |
|---------------------|---------------------|-------------------------------|
| Find specific info  | `filter`            | "What is Alice's email?"      |
| Find error messages | `filter`            | "Show me all errors"          |
| Summarize all       | `uniform`           | "Summarize the main themes"   |
| Count/list entities | `uniform`           | "List all person names"       |
| Compare sections    | `semantic`          | "Compare the conclusions"     |
| Analyze structure   | `semantic`          | "What are the main sections?" |
| Reduce chunk count  | `semantic` + merge  | "Too many small chunks"       |
| Find in structure   | `semantic` + filter | "Find OAuth in each section"  |
| Token precision     | `token`             | "Fit within context window"   |
| Complex documents   | `recursive`         | "Mixed code and prose"        |
| Unknown             | `auto`              | "Let the CLI decide"          |

---

## Related Documentation

| Topic           | File                                     | Description                  |
|-----------------|------------------------------------------|------------------------------|
| Overview        | [SKILL.md](SKILL.md)                     | Quick start and workflow     |
| Examples        | [examples.md](examples.md)               | Real-world scenarios         |
| Reference       | [reference.md](reference.md)             | Complete command reference   |
| Agent Guide     | [agent-guide.md](agent-guide.md)         | Parallel processing protocol |
| Troubleshooting | [troubleshooting.md](troubleshooting.md) | Errors and solutions         |