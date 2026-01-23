# RLM CLI Technical Reference

This reference covers JSON output formats and session file structure for advanced CLI usage.

## JSON Output Formats

Use `--json` flag with commands for machine-parsable output.

### Session Info (`rlm info --json`)

```json
{
  "source": "document.md",
  "totalLength": 1048576,
  "tokenEstimate": 262144,
  "lineCount": 15234,
  "chunkCount": 63,
  "currentChunkIndex": 32,
  "resultCount": 5
}
```

| Field               | Description                                    |
|---------------------|------------------------------------------------|
| `source`            | Document file path or "stdin"                  |
| `totalLength`       | Total characters in document                   |
| `tokenEstimate`     | Approximate token count (chars / 4)            |
| `lineCount`         | Total lines in document                        |
| `chunkCount`        | Number of chunks after chunking                |
| `currentChunkIndex` | Current position (0-based) in chunk buffer     |
| `resultCount`       | Number of stored partial results               |

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

| Field           | Description                                         |
|-----------------|-----------------------------------------------------|
| `index`         | Chunk index (0-based)                               |
| `content`       | Chunk text content                                  |
| `startPosition` | Starting character position in original document    |
| `endPosition`   | Ending character position in original document      |
| `metadata`      | Strategy-specific metadata (varies by strategy)     |

**Strategy-specific metadata:**

| Strategy    | Metadata Fields                                          |
|-------------|----------------------------------------------------------|
| `semantic`  | `headerPath` - Section hierarchy (e.g., "Overview > Setup") |
| `token`     | `tokenCount` - Actual token count for the chunk          |
| `recursive` | `separatorUsed` - Which separator split this chunk       |
| `filter`    | `matchCount` - Number of pattern matches in chunk        |

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
  "content": "chunk_0: Found alice@example.com\nchunk_1: Found bob@example.com",
  "resultCount": 2,
  "signal": "FINAL"
}
```

| Field         | Description                                      |
|---------------|--------------------------------------------------|
| `content`     | Combined results with separator                  |
| `resultCount` | Number of partial results combined               |
| `signal`      | `"PARTIAL"` or `"FINAL"` (with `--final` flag)   |

In text mode, `--final` wraps output with `FINAL(...)`.

## Session File Format

The session is stored at `~/.rlm-session.json`. Example structure:

```json
{
  "content": "# Document Title\n\nDocument content here...",
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

### Session Fields

| Field               | Description                                        |
|---------------------|----------------------------------------------------|
| `content`           | Full document text                                 |
| `metadata`          | Document metadata (see below)                      |
| `chunkBuffer`       | Array of chunks from last chunking operation       |
| `currentChunkIndex` | Position in chunk buffer (0-based)                 |
| `results`           | Dictionary of stored partial results               |
| `recursionDepth`    | Current depth in nested RLM calls (max: 5)         |

### Document Metadata Fields

Metadata varies by document format. Common fields:

| Field                  | Description                              | Formats           |
|------------------------|------------------------------------------|-------------------|
| `source`               | File path or "stdin"                     | All               |
| `totalLength`          | Character count                          | All               |
| `tokenEstimate`        | Approximate tokens (chars / 4)           | All               |
| `lineCount`            | Line count                               | All               |
| `loadedAt`             | ISO 8601 timestamp                       | All               |
| `contentType`          | MIME type (e.g., "text/markdown")        | All               |
| `originalFormat`       | Format before conversion                 | HTML              |
| `title`                | Document title                           | PDF, Markdown, Word |
| `author`               | Document author                          | PDF, Word         |
| `pageCount`            | Page count                               | PDF               |
| `elementCount`         | JSON element count                       | JSON              |
| `wordCount`            | Word count                               | Markdown          |
| `headerCount`          | Markdown header count                    | Markdown          |
| `codeBlockCount`       | Code block count                         | Markdown          |
| `codeLanguages`        | Detected languages in code blocks        | Markdown          |
| `estimatedReadingTime` | Reading time in minutes                  | Markdown          |
| `extendedMetadata`     | YAML frontmatter key-value pairs         | Markdown          |

## Supported Formats

| Format     | Extension(s)       | Features                                        |
|------------|--------------------|-------------------------------------------------|
| Markdown   | `.md`, `.markdown` | YAML frontmatter, code blocks, headers          |
| PDF        | `.pdf`             | Text extraction, page count, title, author      |
| HTML       | `.html`, `.htm`    | Converts to Markdown, preserves structure       |
| JSON       | `.json`            | Pretty-prints, element count                    |
| Word       | `.docx`            | Paragraph extraction, document properties       |
| Plain text | `.txt`, etc.       | Basic text loading                              |

## Related Documentation

| Topic           | File                                     | Description                          |
|-----------------|------------------------------------------|--------------------------------------|
| Overview        | [SKILL.md](SKILL.md)                     | Quick start and workflow             |
| Strategies      | [strategies.md](strategies.md)           | All chunking strategies with options |
| Examples        | [examples.md](examples.md)               | Real-world workflow scenarios        |
| Troubleshooting | [troubleshooting.md](troubleshooting.md) | Tips, errors, and edge cases         |
