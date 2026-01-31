# RLM Agent Guide: Recursive Decomposition

This guide instructs AI Agents on how to use the RLM CLI to perform Recursive Language Modeling (RLM).
The RLM pattern allows you to process documents larger than your context window by breaking them down and optionally spawning sub-agents to process chunks.

## When to Use Parallel vs Sequential Processing

### Decision Criteria

| Factor        | Use Sequential                      | Use Parallel                 |
|---------------|-------------------------------------|------------------------------|
| Chunk count   | < 10 chunks                         | 10+ chunks                   |
| Dependencies  | Chunks depend on each other         | Chunks are independent       |
| Task type     | Narrative summary, ordered analysis | Search, extraction, counting |
| Context needs | Need to see previous findings       | Each chunk standalone        |
| Complexity    | Simple extraction                   | Complex analysis per chunk   |

### Quick Decision Tree

```
How many chunks?
│
├── < 10 chunks ─────────────────────────► Sequential (process yourself)
│   Simple, no agent overhead
│
└── 10+ chunks
    │
    ├── Chunks depend on each other? ────► Sequential (process yourself)
    │   Need previous context
    │
    └── Chunks are independent? ─────────► Parallel (spawn workers)
        Faster, scalable
```

---

## Sequential Processing (Single Agent)

Process all chunks yourself without spawning sub-agents. Simpler and maintains context.

```bash
# 1. Load and chunk
rlm load document.pdf --session main
rlm chunk --strategy uniform --size 40000 --session main

# 2. Process each chunk yourself
rlm next --session main
# (analyze content, extract findings)
rlm store chunk_0 "Found: Key point A, reference to section 3"

rlm next --session main
# (analyze, referencing previous findings if needed)
rlm store chunk_1 "Found: Key point B, expands on point A"

# Continue until "No more chunks"

# 3. Aggregate
rlm aggregate --session main
```

**When to use:**
- Fewer than 10 chunks
- Summary requires understanding flow/narrative
- Each chunk may reference previous content
- Simple extraction tasks

---

## Parallel Processing (Multiple Agents)

Spawn sub-agents to process chunks concurrently. Faster for large documents with independent chunks.

### 1. Initialize Parent Session

Always use a named session for the parent to avoid conflicts.

```bash
rlm load large_document.pdf --session parent
rlm chunk --strategy uniform --size 20000 --session parent
```

### 2. Extract and Delegate Chunks

For each chunk, extract content and spawn a worker agent.

```bash
# Get chunk content
rlm next --raw --session parent > chunk_0.txt

# SPAWN WORKER AGENT with instruction:
# "Process chunk_0.txt. Extract all email addresses. Store result with key 'result'. Use session child_0."

rlm next --raw --session parent > chunk_1.txt
# SPAWN WORKER AGENT with instruction:
# "Process chunk_1.txt. Extract all email addresses. Store result with key 'result'. Use session child_1."

# Continue for all chunks...
```

### 3. Worker Agent Workflow

Each worker processes its assigned chunk independently.

```bash
# Load the assigned chunk
rlm load chunk_0.txt --session child_0

# Process (may chunk further if still too big)
# ... perform extraction/analysis ...

# Store result with standard key "result"
rlm store result "alice@example.com, bob@company.org" --session child_0
```

**Worker Rules:**
- Use the assigned unique session ID (`child_N`)
- Store final result with key `result`
- Clean up temporary files if created

### 4. Import and Aggregate

After all workers complete, parent imports and combines results.

```bash
# Import all child session results
rlm import "rlm-session-child_*.json" --session parent

# Combine all results
rlm aggregate --session parent
```

### 5. Cleanup

```bash
rlm clear --all
# Or specific: rlm clear --session child_1
```

---

## Session File Naming Conventions

### File Locations

| Session Type | File Name               | Location              |
|--------------|-------------------------|-----------------------|
| Default      | `.rlm-session.json`     | Home directory (`~/`) |
| Named        | `rlm-session-{id}.json` | Home directory (`~/`) |

### Recommended Naming Patterns

| Role              | Session ID               | File Created                 |
|-------------------|--------------------------|------------------------------|
| Parent/Controller | `parent`                 | `rlm-session-parent.json`    |
| Worker (by index) | `child_0`, `child_1`     | `rlm-session-child_0.json`   |
| Worker (by task)  | `search_0`, `summary_1`  | `rlm-session-search_0.json`  |
| Nested recursion  | `child_0_0`, `child_0_1` | `rlm-session-child_0_0.json` |

### Import Glob Patterns

```bash
# Import all child sessions
rlm import "rlm-session-child_*.json" --session parent

# Import specific task type
rlm import "rlm-session-search_*.json" --session parent

# Verify glob matches expected files
ls ~/rlm-session-child_*.json
```

---

## Advanced Features for Agents

### Pipe Support

Avoid temporary files by piping content directly.

**Reading Input:**
```bash
cat content.txt | rlm load - --session temp_1
```

**Writing Output:**
```bash
# Get raw content to pass to a tool or API
rlm next --raw --session parent
```

**Storing Results from Stdin:**
```bash
echo "$SUMMARY" | rlm store summary - --session child_1
```

### Exporting Session Content to Files

Use `slice` when you need to export session content to files (e.g., for parallel worker input). Use `next` for iterative chunk-by-chunk processing within a single agent.

**For entire content (parallel worker input):**
```bash
# Export first 200k chars to a file for a worker
rlm slice 0:200000 --session parent --raw > chunk_0.txt

# Export specific ranges
rlm slice 200000:400000 --session parent --raw > chunk_1.txt
```

**For chunk-by-chunk iteration (single agent):**
```bash
# First chunk the document
rlm chunk --strategy uniform --size 50000 --session parent

# Then iterate through chunks
rlm next --raw --session parent > chunk_0.txt
rlm next --raw --session parent > chunk_1.txt
```

**Key Difference:**
- `slice` works on the raw document content by character position
- `next` requires chunking first and iterates through the chunk buffer

### Nested Recursion (Worker-Spawns-Worker)

Workers can spawn child workers for true recursive parallel decomposition. This enables processing of arbitrarily large documents through hierarchical delegation.

**⚠️ Maximum recursion depth: 5 levels**

The session tracks recursion depth and will reject decomposition beyond 5 levels. Check depth with `rlm info --json` (see `recursionDepth` and `maxRecursionDepth` fields).

#### Multi-Level Delegation Diagram

```
Parent Agent (depth 0)
├── child_0 (depth 1) ─────────────────────────┐
│   ├── child_0_0 (depth 2)                    │
│   │   ├── child_0_0_0 (depth 3)              │ True parallel
│   │   └── child_0_0_1 (depth 3)              │ decomposition
│   └── child_0_1 (depth 2)                    │
│       └── child_0_1_0 (depth 3)              │
├── child_1 (depth 1) ─────────────────────────┤
│   ├── child_1_0 (depth 2)                    │
│   └── child_1_1 (depth 2)                    │
└── child_2 (depth 1) ─────────────────────────┘
    └── (processes inline - small chunk)
```

#### Session Naming Convention (Hierarchical)

```
Level 0 (parent):       parent
Level 1 (workers):      child_0, child_1, child_2
Level 2 (sub-workers):  child_0_0, child_0_1, child_1_0
Level 3 (sub-sub):      child_0_0_0, child_0_0_1
Level 4:                child_0_0_0_0
Level 5 (max):          child_0_0_0_0_0 (must process inline)
```

#### Worker Decision Criteria

Workers decide between delegation and inline processing:

| Condition                                 | Action                                          |
|-------------------------------------------|-------------------------------------------------|
| `recursionDepth >= 4`                     | Process INLINE (approaching limit)              |
| `chunkCount <= 3`                         | Process INLINE (not enough to justify spawning) |
| `recursionDepth < 4` AND `chunkCount > 3` | DELEGATE to child workers                       |

#### Delegation Workflow (Worker Spawning Children)

```bash
# Worker receives large chunk
rlm load chunk_0.txt --session child_0
rlm info --json --session child_0  # Check size and recursion depth

# Still too big - chunk again (increments recursion depth)
rlm chunk --strategy uniform --size 15000 --session child_0
rlm info --json --session child_0
# Check: recursionDepth < 4 AND chunkCount > 3

# DELEGATE: Extract chunks and spawn child workers
rlm next --raw --session child_0 > subchunk_0.txt
# SPAWN: rlm-worker with session=child_0_0, file=subchunk_0.txt

rlm next --raw --session child_0 > subchunk_1.txt
# SPAWN: rlm-worker with session=child_0_1, file=subchunk_1.txt

# ... spawn workers for all chunks ...

# Wait for all children to complete

# Import child results and aggregate
rlm import "rlm-session-child_0_*.json" --session child_0
rlm aggregate --session child_0
rlm store result "<aggregated finding>" --session child_0

# Cleanup
rm -f subchunk_*.txt
```

#### Inline Processing Workflow (Fallback)

When at depth limit or with few chunks, process inline:

```bash
# Worker at depth 4 receives chunk
rlm load chunk_0_0_0.txt --session child_0_0_0
rlm info --json --session child_0_0_0
# Shows: recursionDepth: 4 → must process inline

# Chunk and process yourself
rlm chunk --strategy uniform --size 15000 --session child_0_0_0

rlm next --session child_0_0_0
# ... analyze and extract ...
rlm store sub_0 "finding 1" --session child_0_0_0

rlm next --session child_0_0_0
rlm store sub_1 "finding 2" --session child_0_0_0

# Aggregate before storing final result
rlm aggregate --session child_0_0_0
rlm store result "<aggregated content>" --session child_0_0_0
```

#### Import Patterns for Deep Nesting

```bash
# Parent imports direct children
rlm import "rlm-session-child_*.json" --session parent

# Worker imports its children (note the underscore pattern)
rlm import "rlm-session-child_0_*.json" --session child_0

# Sub-worker imports its children
rlm import "rlm-session-child_0_0_*.json" --session child_0_0
```

---

## Troubleshooting

| Issue                 | Cause                             | Solution                            |
|-----------------------|-----------------------------------|-------------------------------------|
| "Session Locked"      | Concurrent access to same session | Use unique `--session` IDs          |
| "No chunks available" | Missing `load` or `chunk`         | Run `rlm load` then `rlm chunk`     |
| Output has formatting | Default output mode               | Use `--raw` for clean text          |
| Import finds no files | Glob doesn't match                | Verify with `ls rlm-session-*.json` |

See [troubleshooting.md](troubleshooting.md) for detailed solutions.

---

## Related Documentation

| Topic           | File                                     | Description                   |
|-----------------|------------------------------------------|-------------------------------|
| Overview        | [SKILL.md](SKILL.md)                     | Quick start and workflow      |
| Strategies      | [strategies.md](strategies.md)           | Chunking strategy selection   |
| Examples        | [examples.md](examples.md)               | Real-world workflow scenarios |
| Reference       | [reference.md](reference.md)             | Complete command reference    |
| Troubleshooting | [troubleshooting.md](troubleshooting.md) | Errors and solutions          |