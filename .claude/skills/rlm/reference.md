# RLM CLI Command Reference

Complete reference for all RLM CLI commands, options, and output formats.

## Commands

### load

Load a document into the session.

```bash
rlm load <source> [options]
```

**Arguments:**
- `<source>` - File path, directory path, or `-` for stdin

**Options:**

| Option           | Description                 | Default                            |
|------------------|-----------------------------|------------------------------------|
| `--session <id>` | Named session for isolation | `default`                          |
| `-p              | --pattern <glob>`           | Glob pattern for directory loading |
| `--merge <bool>` | Merge multiple documents    | `true`                             |

**Examples:**

```bash
rlm load document.md                     # Single file
rlm load ./docs/                         # Directory (merged)
rlm load ./docs/ --pattern "**/*.md"     # Recursive glob
rlm load ./docs/ --merge false           # Keep separate
cat file.txt | rlm load -                # From stdin
rlm load report.pdf --session analysis   # Named session
```

---

### info

Display document metadata and session state.

```bash
rlm info [options]
```

**Options:**

| Option           | Description                  |
|------------------|------------------------------|
| `--session <id>` | Named session                |
| `--progress`     | Show processing progress bar |
| `--json`         | JSON output format           |

**Example Output:**

```
Source: document.md
Length: 1,048,576 chars (~262,144 tokens)
Lines: 15,234
Chunks: 63 (current: 32)
Results: 5 stored

Content-Type: text/markdown
Title: Document Title
Author: Author Name
Words: 45,000
Headers: 28
Code Blocks: 12 (C#, TypeScript, Bash)
Reading Time: ~61 minutes
```

**Progress Output:**

```
Progress: ███████████████░░░░░░░░░░░░░░░ 50.8%
Chunks: 32 / 63 (31 remaining)
Results: 5 stored
```

---

### slice

View a section of the loaded document.

```bash
rlm slice <range> [options]
```

**Arguments:**
- `<range>` - Character range in format `start:end`

**Options:**

| Option           | Description                     |
|------------------|---------------------------------|
| `--session <id>` | Named session                   |
| `--raw`          | Raw text output (no formatting) |

**Examples:**

```bash
rlm slice 0:1000         # First 1000 chars
rlm slice -500:          # Last 500 chars
rlm slice 1000:2000      # Middle section
rlm slice 0:5000 --raw   # Raw output for piping
```

---

### chunk

Apply a chunking strategy to the document.

```bash
rlm chunk [options]
```

**Options:**

| Option                 | Description                       | Default                         |
|------------------------|-----------------------------------|---------------------------------|
| `--session <id>`       | Named session                     | `default`                       |
| `-s                    | --strategy <name>`                | Chunking strategy               |
| `--size <chars>`       | Characters per chunk              | `50000`                         |
| `--overlap <chars>`    | Overlap between chunks            | `0`                             |
| `--max-tokens <n>`     | Tokens per chunk (token strategy) | `512`                           |
| `--overlap-tokens <n>` | Token overlap (token strategy)    | `50`                            |
| `--min-level <n>`      | Min header level (semantic)       | `1`                             |
| `--max-level <n>`      | Max header level (semantic)       | `3`                             |
| `--min-size <chars>`   | Min chunk size (semantic)         | `0`                             |
| `--max-size <chars>`   | Max chunk size (semantic)         | `0`                             |
| `--merge-small`        | Merge consecutive small chunks    | `false`                         |
| `-p                    | --pattern <regex>`                | Filter pattern (hybrid mode)    |
| `-c                    | --context <chars>`                | Context around matches (filter) |
| `-q                    | --query <text>`                   | Query for auto strategy         |

**Strategies:**

| Strategy    | Description                             |
|-------------|-----------------------------------------|
| `uniform`   | Fixed-size chunks with optional overlap |
| `filter`    | Regex pattern matching with context     |
| `semantic`  | Split on Markdown headers               |
| `token`     | Accurate token-based splitting          |
| `recursive` | Intelligent separator hierarchy         |
| `auto`      | Query-based automatic selection         |

See [strategies.md](strategies.md) for detailed strategy documentation.

---

### filter

Filter document by regex pattern (shorthand for `chunk --strategy filter`).

```bash
rlm filter <pattern> [options]
```

**Arguments:**
- `<pattern>` - Regex pattern to match

**Options:**

| Option           | Description        | Default                      |
|------------------|--------------------|------------------------------|
| `--session <id>` | Named session      | `default`                    |
| `-c              | --context <chars>` | Characters around each match |

**Examples:**

```bash
rlm filter "ERROR|CRITICAL"              # Match multiple patterns
rlm filter "email|@" --context 1000      # More context
rlm filter "\\[ERROR\\]"                 # Escape special chars
```

---

### next

Get the next chunk from the buffer.

```bash
rlm next [options]
```

**Options:**

| Option           | Description                     |
|------------------|---------------------------------|
| `--session <id>` | Named session                   |
| `--raw`          | Raw text output (no formatting) |
| `--json`         | JSON output format              |

---

### skip

Skip forward or backward in the chunk buffer.

```bash
rlm skip <count> [options]
```

**Arguments:**
- `<count>` - Number of chunks to skip (negative for backward)

**Options:**

| Option           | Description                                  |
|------------------|----------------------------------------------|
| `--session <id>` | Named session                                |
| `-j              | --json`                                      |
| `--skip-empty`   | Skip empty or very small chunks (<100 chars) |

**Examples:**

```bash
rlm skip 10              # Skip forward 10 chunks
rlm skip -5              # Skip backward 5 chunks
rlm skip 5 --skip-empty  # Skip forward, ignoring small chunks
rlm skip 3 --json        # JSON output with skip metadata
```

---

### jump

Jump to a specific chunk position.

```bash
rlm jump <position> [options]
```

**Arguments:**
- `<position>` - Chunk index (1-based) or percentage

**Options:**

| Option           | Description                     |
|------------------|---------------------------------|
| `--session <id>` | Named session                   |
| `-j              | --json`                         |
| `--raw`          | Raw text output (no formatting) |

**Examples:**

```bash
rlm jump 50         # Jump to chunk 50
rlm jump 50%        # Jump to 50% position
rlm jump 100%       # Jump to last chunk
rlm jump 25 --raw   # Raw output for piping
rlm jump 10 --json  # JSON output with jump metadata
```

---

### store

Store a partial result.

```bash
rlm store <key> <value> [options]
rlm store <key> - [options]  # Read value from stdin
```

**Arguments:**
- `<key>` - Result key (e.g., `chunk_0`, `summary`)
- `<value>` - Result value, or `-` for stdin

**Options:**

| Option           | Description   |
|------------------|---------------|
| `--session <id>` | Named session |

**Examples:**

```bash
rlm store chunk_0 "Found: alice@example.com"
echo "Long result..." | rlm store summary -
```

---

### results

List all stored results.

```bash
rlm results [options]
```

**Options:**

| Option           | Description   |
|------------------|---------------|
| `--session <id>` | Named session |

---

### import

Import results from child session files.

```bash
rlm import <glob> [options]
```

**Arguments:**
- `<glob>` - Glob pattern matching session files

**Options:**

| Option           | Description                         |
|------------------|-------------------------------------|
| `--session <id>` | Target session for imported results |

**Examples:**

```bash
rlm import "rlm-session-child_*.json" --session parent
```

---

### aggregate

Combine all stored results.

```bash
rlm aggregate [options]
```

**Options:**

| Option           | Description                     | Default               |
|------------------|---------------------------------|-----------------------|
| `--session <id>` | Named session                   | `default`             |
| `-s              | --separator <text>`             | Result separator      |
| `-j              | --json`                         | JSON output format    |
| `-f              | --final`                        | Add completion signal |
| `--raw`          | Raw text output (no formatting) | -                     |

**Examples:**

```bash
rlm aggregate                        # Default separator
rlm aggregate --separator "\n---\n"  # Custom separator
rlm aggregate --json --final         # JSON with FINAL signal
```

---

### clear

Reset session state.

```bash
rlm clear [options]
```

**Options:**

| Option           | Description            |
|------------------|------------------------|
| `--session <id>` | Clear specific session |
| `--all`          | Clear all sessions     |

**Examples:**

```bash
rlm clear                    # Clear default session
rlm clear --session child_1  # Clear specific session
rlm clear --all              # Clear all sessions
```

---

## Option Availability by Command

Not all options are available on every command. Here's the per-command availability:

| Command     | `--session` | `--raw` | `--json` |
|-------------|:-----------:|:-------:|:--------:|
| `load`      |      ✓      |         |          |
| `info`      |      ✓      |         |    ✓     |
| `slice`     |      ✓      |    ✓    |          |
| `chunk`     |      ✓      |         |          |
| `filter`    |      ✓      |         |          |
| `next`      |      ✓      |    ✓    |    ✓     |
| `skip`      |      ✓      |         |    ✓     |
| `jump`      |      ✓      |    ✓    |    ✓     |
| `store`     |      ✓      |         |          |
| `results`   |      ✓      |         |          |
| `import`    |      ✓      |         |          |
| `aggregate` |      ✓      |    ✓    |    ✓     |
| `clear`     |      ✓      |         |          |

**Notes:**
- `--session <id>` is available on all commands for session isolation
- `--raw` outputs plain text without formatting, useful for piping to other tools
- `--json` outputs structured JSON for machine parsing

---

## JSON Output Formats

### Session Info (`rlm info --json`)

```json
{
  "source": "document.md",
  "totalLength": 1048576,
  "tokenEstimate": 262144,
  "lineCount": 15234,
  "loadedAt": "2024-01-15T10:30:00.0000000Z",
  "chunkCount": 63,
  "currentChunkIndex": 32,
  "remainingChunks": 30,
  "resultCount": 5,
  "recursionDepth": 0,
  "maxRecursionDepth": 5,
  "progressPercent": 52.38,
  "processedChars": 524288,
  "totalChars": 1000000,
  "averageChunkSize": 15873,
  "remainingTokenEstimate": 125000
}
```

| Field                    | Description                             |
|--------------------------|-----------------------------------------|
| `source`                 | File path or "stdin"                    |
| `totalLength`            | Total document character count          |
| `tokenEstimate`          | Estimated token count (~chars/4)        |
| `lineCount`              | Number of lines in document             |
| `loadedAt`               | ISO 8601 timestamp when document loaded |
| `chunkCount`             | Total number of chunks                  |
| `currentChunkIndex`      | Current position (0-based)              |
| `remainingChunks`        | Chunks left to process                  |
| `resultCount`            | Number of stored results                |
| `recursionDepth`         | Current recursion depth (0-5)           |
| `maxRecursionDepth`      | Maximum allowed recursion depth (5)     |
| `progressPercent`        | Completion percentage                   |
| `processedChars`         | Characters processed so far             |
| `totalChars`             | Total characters across all chunks      |
| `averageChunkSize`       | Average characters per chunk            |
| `remainingTokenEstimate` | Estimated tokens remaining              |

### Chunk Output (`rlm next --json`)

```json
{
  "index": 0,
  "content": "## Introduction\n\nThis section covers...",
  "startPosition": 0,
  "endPosition": 4096,
  "metadata": {
    "headerPath": "Introduction",
    "tokenCount": 1024,
    "separatorUsed": "\n## "
  }
}
```

**Strategy-specific metadata:**

| Strategy    | Metadata Fields                                   |
|-------------|---------------------------------------------------|
| `semantic`  | `headerPath` - Section hierarchy                  |
| `token`     | `tokenCount` - Actual token count                 |
| `recursive` | `separatorUsed` - Separator that split this chunk |
| `filter`    | `matchCount` - Number of pattern matches          |

### Aggregate Output (`rlm aggregate --json`)

```json
{
  "content": "chunk_0: Found alice@example.com\nchunk_1: Found bob@example.com",
  "resultCount": 2,
  "signal": "PARTIAL"
}
```

With `--final` flag:

```json
{
  "content": "...",
  "resultCount": 2,
  "signal": "FINAL"
}
```

---

## Session File Format

Session files are stored in the home directory (`~/`):
- Default: `~/.rlm-session.json`
- Named: `~/rlm-session-{id}.json`

**Structure:**

```json
{
  "content": "# Document Title\n\nDocument content...",
  "metadata": {
    "source": "document.md",
    "totalLength": 1048576,
    "tokenEstimate": 262144,
    "lineCount": 15234,
    "loadedAt": "2024-01-15T10:30:00Z",
    "contentType": "text/markdown",
    "title": "Document Title",
    "wordCount": 45000,
    "headerCount": 28
  },
  "chunkBuffer": [
    {
      "index": 0,
      "content": "...",
      "startPosition": 0,
      "endPosition": 4096,
      "metadata": {}
    }
  ],
  "currentChunkIndex": 0,
  "results": {
    "chunk_0": "Finding from first chunk",
    "chunk_1": "Finding from second chunk"
  },
  "recursionDepth": 0
}
```

### Recursion Depth

The `recursionDepth` field tracks how many levels of nested decomposition have occurred:

- **Value 0**: Top-level processing (no recursion yet)
- **Values 1-5**: Nested decomposition levels
- **Maximum**: 5 (defined by `MaxRecursionDepth` constant)

When processing large chunks, workers can either:
1. **Delegate:** Spawn child `rlm-worker` agents (if depth < 4 and chunks > 3)
2. **Inline:** Process sub-chunks themselves (at depth limit or few chunks)

The recursion depth increments with each chunking operation. If depth exceeds 5,
the session rejects further decomposition attempts.

### Document Metadata Fields

| Field                  | Description              | Formats             |
|------------------------|--------------------------|---------------------|
| `source`               | File path or "stdin"     | All                 |
| `totalLength`          | Character count          | All                 |
| `tokenEstimate`        | Approximate tokens       | All                 |
| `lineCount`            | Line count               | All                 |
| `loadedAt`             | ISO 8601 timestamp       | All                 |
| `contentType`          | MIME type                | All                 |
| `originalFormat`       | Format before conversion | HTML                |
| `title`                | Document title           | PDF, Markdown, Word |
| `author`               | Document author          | PDF, Word           |
| `pageCount`            | Page count               | PDF                 |
| `elementCount`         | JSON element count       | JSON                |
| `wordCount`            | Word count               | Markdown, Word      |
| `headerCount`          | Header count             | Markdown            |
| `codeBlockCount`       | Code block count         | Markdown            |
| `codeLanguages`        | Languages in code blocks | Markdown            |
| `estimatedReadingTime` | Reading time in minutes  | Markdown, Word      |
| `extendedMetadata`     | YAML frontmatter pairs   | Markdown            |

---

## Related Documentation

| Topic           | File                                     | Description                  |
|-----------------|------------------------------------------|------------------------------|
| Overview        | [SKILL.md](SKILL.md)                     | Quick start and workflow     |
| Strategies      | [strategies.md](strategies.md)           | All chunking strategies      |
| Examples        | [examples.md](examples.md)               | Real-world scenarios         |
| Agent Guide     | [agent-guide.md](agent-guide.md)         | Parallel processing protocol |
| Troubleshooting | [troubleshooting.md](troubleshooting.md) | Errors and solutions         |