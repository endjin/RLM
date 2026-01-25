# RLM Example Workflows

This document provides real-world workflow examples for processing large documents with the RLM CLI.

## Finding Specific Information (Needle-in-Haystack)

Use the filter strategy when searching for specific content in large documents.

```bash
# 1. Load the large document
rlm load server-logs.txt

# 2. Filter for relevant content
rlm filter "ERROR|CRITICAL|exception"

# 3. Process each matching segment
# (analyze the content shown)
rlm store segment_0 "Database connection timeout at 14:32"
rlm next
rlm store segment_1 "Memory exhaustion at 15:45"
rlm next
# Continue until done

# 4. Aggregate findings
rlm aggregate
# Use this to synthesize final answer
```

**When to use:**
- Searching logs for specific errors
- Finding email addresses or names
- Locating specific configuration values
- Extracting mentions of a particular topic

## Summarizing a Large Document

Use uniform chunking for comprehensive document summarization.

```bash
# 1. Load document
rlm load annual-report.txt

# 2. Chunk uniformly
rlm chunk --strategy uniform --size 40000

# 3. Process each chunk
# (read and summarize the content)
rlm store chunk_0 "Q1: Revenue up 15%, new product launches"
rlm next
rlm store chunk_1 "Q2: Supply chain challenges, cost increases"
rlm next
# Continue

# 4. Aggregate for final synthesis
rlm aggregate
```

**When to use:**
- Executive summaries of long reports
- Extracting key points from documentation
- Creating overviews of large codebases

## Analyzing Document Structure

Use semantic chunking to preserve and analyze document hierarchy.

```bash
# 1. Load markdown document
rlm load specification.md

# 2. Use semantic chunking
rlm chunk --strategy semantic

# 3. Process each section
# (the section header is included in metadata)
rlm store intro "Purpose: Define API contracts"
rlm next
rlm store endpoints "Lists 15 REST endpoints"
rlm next
# Continue

# 4. Aggregate
rlm aggregate
```

**When to use:**
- Comparing different sections of a specification
- Building table of contents
- Understanding document organization

## Processing Large Documents Efficiently

Combine semantic chunking with merging and navigation for large documents.

```bash
# 1. Load large document
rlm load massive-guidelines.md

# 2. Check initial chunk count with basic semantic
rlm chunk --strategy semantic
# Output: Created 521 chunk(s) - too granular!

# 3. Re-chunk with merging to reduce count
rlm chunk --strategy semantic --min-size 5000 --merge-small
# Output: Created 63 chunk(s) - much better!

# 4. Navigate efficiently
rlm skip 10           # Skip ahead
rlm info --progress   # Check progress
rlm jump 50%          # Jump to middle
rlm skip -5           # Go back 5

# 5. Process and store results
rlm store section_32 "Found key configuration patterns"
rlm next
```

**When to use:**
- Very large documents with hundreds of sections
- Documents where not all sections are relevant
- Long processing sessions

## Finding Patterns in Structured Documents (Hybrid Mode)

Combine semantic structure with pattern filtering for targeted analysis.

```bash
# 1. Load technical documentation
rlm load api-docs.md

# 2. Use hybrid mode: semantic structure + pattern filter
rlm chunk --strategy semantic --pattern "authentication|OAuth" --min-size 3000 --merge-small
# Output: Created 12 chunk(s) matching pattern

# 3. Process only relevant sections
rlm store auth_1 "OAuth 2.0 flow described in section 3"
rlm next
rlm store auth_2 "Token refresh mechanism"
# Continue

# 4. Aggregate findings
rlm aggregate
```

**When to use:**
- Finding specific topics within structured documentation
- Extracting related information while preserving context
- Analyzing API documentation for specific features

## Processing Logs with Regex Patterns

Extract and analyze log entries matching complex patterns.

```bash
# 1. Load large log file
cat /var/log/application.log | rlm load -

# 2. Filter with complex regex
rlm filter "\\[ERROR\\].*timeout|connection refused"

# 3. Process matches
rlm store error_0 "Timeout connecting to database at 2024-01-15 14:32"
rlm next
# Continue

# 4. Get summary
rlm aggregate
```

## Multi-Turn Investigation

Use RLM for iterative investigation across multiple queries.

```bash
# Initial investigation
rlm load application.log
rlm filter "OutOfMemory"
# Found 3 matches, store findings
rlm store phase1 "OOM errors at 14:00, 14:15, 14:30"

# Dig deeper - look for related events
rlm filter "heap|GC|memory" --context 1000
# Store additional context
rlm store phase2 "GC pauses preceding each OOM"

# Aggregate all findings
rlm aggregate
```

## JSON Output for Automation

Use JSON mode for programmatic processing.

```bash
# Get session info as JSON
rlm info --json

# Process chunks as JSON
rlm next --json
# Returns: {"index": 0, "content": "...", "startPosition": 0, ...}

# Aggregate with FINAL signal for automation
rlm aggregate --json --final
# Returns: {"content": "...", "resultCount": 5, "signal": "FINAL"}
```

**JSON output models:**

Session info:
```json
{
  "source": "document.txt",
  "totalLength": 150000,
  "tokenEstimate": 37500,
  "lineCount": 2500,
  "chunkCount": 5,
  "currentChunkIndex": 2,
  "resultCount": 2
}
```

Chunk:
```json
{
  "index": 2,
  "content": "chunk content here...",
  "startPosition": 60000,
  "endPosition": 90000,
  "metadata": { "separatorUsed": "\n## " }
}
```

## Loading PDF Documents

Extract and process text from PDF files with automatic metadata extraction.

```bash
# 1. Load PDF document
rlm load annual-report.pdf
# Output: Loaded 485,230 chars, ~121,307 tokens (2.3s)
#         Content-Type: application/pdf
#         Title: Annual Financial Report 2024
#         Author: Finance Department
#         Pages: 156

# 2. Check extracted metadata
rlm info
# Shows page count, title, author from PDF properties

# 3. Chunk and process
rlm chunk --strategy uniform --size 40000

# 4. Process each section
rlm store summary_0 "Q1 revenue: $42M, up 15%"
rlm next
# Continue processing
```

**When to use:**
- Processing financial reports, contracts, research papers
- Extracting text from scanned documents (with embedded text)
- Analyzing multi-page business documents

## Converting HTML to Markdown

Load HTML pages with automatic conversion to Markdown format.

```bash
# 1. Load HTML document
rlm load documentation.html
# Output: Loaded 125,420 chars, ~31,355 tokens (450ms)
#         Content-Type: text/markdown
#         Original-Format: text/html

# 2. The content is now Markdown - use semantic chunking
rlm chunk --strategy semantic

# 3. Process sections (headers are preserved)
rlm store intro "API overview and authentication"
rlm next
```

**When to use:**
- Processing downloaded web pages
- Converting HTML documentation to analyzable format
- Preserving structure from web content

## Loading Directory of Documents

Process multiple documents from a directory with optional filtering.

```bash
# 1. Load all documents from directory (merged by default)
rlm load ./project-docs/
# Output: Loaded 3 documents (2.1s)
#         - README.md: 12,450 chars
#         - architecture.md: 45,230 chars
#         - api-reference.md: 89,120 chars
#         Total: 146,800 chars, ~36,700 tokens

# 2. Or load with pattern filter
rlm load ./docs/ --pattern "*.md"
# Only loads Markdown files

# 3. Load without merging (keeps documents separate)
rlm load ./docs/ --merge false

# 4. Process combined content
rlm chunk --strategy semantic
rlm store overview "Project has 3 main components..."
```

**When to use:**
- Analyzing entire project documentation
- Comparing multiple related documents
- Building comprehensive summaries across files

## Processing Word Documents

Extract and analyze Microsoft Word (.docx) documents.

```bash
# 1. Load Word document
rlm load proposal.docx
# Output: Loaded 89,340 chars, ~22,335 tokens (1.1s)
#         Content-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document
#         Title: Project Proposal
#         Author: John Smith
#         Words: 15,234

# 2. Check document info
rlm info
# Shows word count, author, title from document properties

# 3. Use semantic chunking (preserves paragraph structure)
rlm chunk --strategy semantic --min-size 3000

# 4. Process sections
rlm store exec_summary "Proposal for $2M infrastructure upgrade"
rlm next
```

**When to use:**
- Processing business proposals and reports
- Analyzing contracts and legal documents
- Extracting information from corporate documentation

## Processing JSON Data

Analyze structured JSON data with pretty-printing.

```bash
# 1. Load JSON file
rlm load api-responses.json
# Output: Loaded 234,560 chars, ~58,640 tokens (320ms)
#         Content-Type: application/json
#         Elements: 1,247

# 2. View structure
rlm slice 0:2000  # See formatted JSON

# 3. Filter for specific data
rlm filter '"error":|"status": "failed"'

# 4. Process matches
rlm store errors "Found 12 failed API calls"
```

**When to use:**
- Analyzing API response logs
- Processing configuration files
- Extracting data from JSON exports

## Recursive Directory Loading with Glob Patterns

Load markdown files from nested directories using recursive glob patterns.

```bash
# 1. Load all markdown files recursively from docs/
rlm load ./docs/ --pattern "**/*.md"
# Output: Loaded 15 documents (3.2s)
#         - docs/README.md: 5,230 chars
#         - docs/guides/getting-started.md: 12,450 chars
#         - docs/guides/advanced/configuration.md: 8,920 chars
#         - docs/api/endpoints.md: 45,230 chars
#         - ... (11 more files)
#         Total: 289,400 chars, ~72,350 tokens

# 2. Check what was loaded
rlm info
# Shows combined metadata from all files

# 3. Process with semantic chunking
rlm chunk --strategy semantic --min-size 5000 --merge-small

# 4. Search across all documentation
rlm filter "authentication|API key"
rlm store auth_findings "Found auth docs in 3 sections..."
```

**Common glob patterns:**

| Pattern         | Matches                              |
|-----------------|--------------------------------------|
| `*.md`          | Markdown files in directory only     |
| `**/*.md`       | Markdown files in all subdirectories |
| `**/*.{md,txt}` | Markdown and text files recursively  |
| `src/**/*.cs`   | C# files under src/ directory        |

**When to use:**
- Analyzing entire project documentation trees
- Building knowledge bases from scattered files
- Searching across multi-level directory structures

## Parallel Processing with Sub-Agents

For very large documents (10+ chunks), spawn worker agents to process in parallel.

```bash
# 1. Parent: Initialize and chunk
rlm load massive-codebase.md --session parent
rlm chunk --strategy uniform --size 30000 --session parent
# Output: Created 45 chunk(s)

# 2. Parent: Extract chunks for workers
rlm next --raw --session parent > chunk_0.txt
# SPAWN: rlm-worker agent with "Process chunk_0.txt, session=child_0"

rlm next --raw --session parent > chunk_1.txt
# SPAWN: rlm-worker agent with "Process chunk_1.txt, session=child_1"

# ... continue for all chunks ...

# 3. After all workers complete: Import and aggregate
rlm import "rlm-session-child_*.json" --session parent
rlm aggregate --session parent
```

**When to use:**
- Documents with 10+ independent chunks
- Tasks that don't require context from previous chunks
- Extraction, counting, or search tasks

See [agent-guide.md](agent-guide.md) for the complete parallel processing protocol, including decision criteria for when to use parallel vs sequential processing.

---

## Related Documentation

| Topic           | File                                     | Description                      |
|-----------------|------------------------------------------|----------------------------------|
| Overview        | [SKILL.md](SKILL.md)                     | Quick start and workflow         |
| Strategies      | [strategies.md](strategies.md)           | Chunking strategy selection      |
| Reference       | [reference.md](reference.md)             | Command options and JSON formats |
| Agent Guide     | [agent-guide.md](agent-guide.md)         | Parallel processing protocol     |
| Troubleshooting | [troubleshooting.md](troubleshooting.md) | Errors and solutions             |
