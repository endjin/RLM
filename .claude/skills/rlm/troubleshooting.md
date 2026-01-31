# RLM Troubleshooting Guide

Quick reference for common issues and solutions when using the RLM CLI.

## Quick Reference Table

| Issue                  | Cause                     | Solution                                     |
|------------------------|---------------------------|----------------------------------------------|
| "rlm: command not found" | RLM not installed       | `dotnet tool install -g rlm`                 |
| "Session locked"       | Concurrent access         | Use unique `--session` IDs                   |
| "No chunks available"  | Missing `load` or `chunk` | Run `rlm load` then `rlm chunk`              |
| "No more chunks"       | Reached end of buffer     | Use `rlm aggregate` to combine results       |
| Output has formatting  | Default output mode       | Use `--raw` for clean text                   |
| Too many small chunks  | Granular semantic split   | Add `--min-size 5000 --merge-small`          |
| Session file not found | Wrong session ID          | Check `ls rlm-session-*.json`                |
| Pattern not matching   | Regex escaping            | Escape special chars: `\\[`, `\\]`           |
| Import finds no files  | Glob pattern wrong        | Test glob with `ls rlm-session-child_*.json` |

---

## Common Errors

### RLM Not Installed

**Symptom:** `rlm: command not found` or similar error.

**Cause:** RLM CLI tool not installed or not in PATH.

**Solution:**
```bash
# Install RLM (requires .NET 10+)
dotnet tool install -g rlm

# Verify installation
rlm --version

# If dotnet tools not in PATH, add to shell profile:
export PATH="$PATH:$HOME/.dotnet/tools"
```

---

### Session Locked

**Symptom:** Error message about session being locked or in use.

**Cause:** Multiple commands accessing the same session simultaneously.

**Solution:**
```bash
# Always use unique session IDs for parallel processing
rlm load doc.pdf --session parent
rlm next --raw --session parent > chunk.txt

# Each worker uses unique ID
rlm load chunk.txt --session child_0
rlm load chunk.txt --session child_1
```

---

### No Chunks Available

**Symptom:** `rlm next` returns "No chunks available".

**Cause:** Document loaded but not chunked.

**Solution:**
```bash
# Must run chunk after load
rlm load document.md
rlm chunk --strategy uniform --size 50000
rlm next  # Now works
```

---

### No More Chunks

**Symptom:** `rlm next` returns "No more chunks".

**Cause:** Reached the end of the chunk buffer.

**Solution:**
```bash
# Check progress
rlm info --progress

# If done processing, aggregate results
rlm aggregate

# Or navigate back
rlm jump 1  # Go back to first chunk
```

---

### Too Many Small Chunks

**Symptom:** Semantic chunking creates hundreds of tiny chunks.

**Cause:** Document has many small headers/sections.

**Solution:**
```bash
# Use merging to consolidate small chunks
rlm chunk --strategy semantic --min-size 5000 --merge-small

# Or use recursive strategy instead
rlm chunk --strategy recursive --size 50000
```

---

### Pattern Not Matching

**Symptom:** Filter returns no results but you know matches exist.

**Cause:** Regex special characters need escaping.

**Solution:**
```bash
# Wrong - brackets are regex operators
rlm filter "[ERROR]"

# Correct - escape special characters
rlm filter "\\[ERROR\\]"

# Or use simpler pattern
rlm filter "ERROR"
```

---

### Import Finds No Files

**Symptom:** `rlm import` returns "No session files found".

**Cause:** Glob pattern doesn't match file names.

**Solution:**
```bash
# Verify files exist
ls rlm-session-*.json

# Match the exact naming pattern used by workers
rlm import "rlm-session-child_*.json" --session parent

# Check worker session naming
# Workers should use: --session child_0, --session child_1, etc.
```

---

### Max Recursion Depth Exceeded

**Symptom:** Error "Maximum recursion depth (5) exceeded" when chunking.

**Cause:** Document decomposition has reached 5 levels deep.

**Solution:**
```bash
# Check current depth
rlm info --json --session child_0_0_0_0
# If recursionDepth: 5, must process inline (no more chunking)

# Process content directly without further chunking
# Or use larger chunk sizes at earlier levels
```

---

### Deep Nesting Session Conflicts

**Symptom:** Import misses some child sessions in deeply nested workflows.

**Cause:** Glob pattern too broad or too narrow.

**Solution:**
```bash
# Import ONLY direct children (one underscore level)
rlm import "rlm-session-child_0_*.json" --session child_0
# Matches: child_0_0, child_0_1 (NOT child_0_0_0)

# Verify pattern before importing
ls rlm-session-child_0_*.json
```

---

## Best Practices

### Session Management

1. **Always use named sessions for parallel work**
   ```bash
   rlm load doc.pdf --session main  # Not default session
   ```

2. **Use consistent naming conventions**
   - Parent: `parent`, `main`, `controller`
   - Workers: `child_0`, `child_1`, or `worker_0`, `worker_1`

3. **Clean up after processing**
   ```bash
   rlm clear --all
   ```

### Performance Tips

1. **Filter before chunking** when searching
   ```bash
   rlm load logs.txt
   rlm filter "ERROR"  # Reduces content first
   ```

2. **Use appropriate chunk sizes**
   - Too small: Many chunks, slow iteration
   - Too large: May exceed context limits
   - Recommended: 30,000-50,000 characters

3. **Navigate efficiently**
   ```bash
   rlm skip 10      # Better than 10x rlm next
   rlm jump 50%     # Jump to middle directly
   ```

### Result Storage

1. **Store incrementally** after each chunk
   ```bash
   rlm store chunk_0 "finding..."
   rlm next
   rlm store chunk_1 "more findings..."
   ```

2. **Use descriptive keys**
   ```bash
   rlm store summary_q1 "Q1 revenue analysis"
   rlm store summary_q2 "Q2 revenue analysis"
   ```

3. **Check stored results before aggregating**
   ```bash
   rlm results  # List all stored keys
   rlm aggregate
   ```

---

## Debugging Commands

```bash
# Check session state
rlm info

# View processing progress
rlm info --progress

# List stored results
rlm results

# View session file directly
cat rlm-session-{id}.json | jq .

# List all session files
ls -la rlm-session-*.json
```

---

## Related Documentation

| Topic       | File                             | Description                   |
|-------------|----------------------------------|-------------------------------|
| Overview    | [SKILL.md](SKILL.md)             | Quick start and workflow      |
| Strategies  | [strategies.md](strategies.md)   | Chunking strategy selection   |
| Examples    | [examples.md](examples.md)       | Real-world workflow scenarios |
| Reference   | [reference.md](reference.md)     | Complete command reference    |
| Agent Guide | [agent-guide.md](agent-guide.md) | Parallel processing protocol  |