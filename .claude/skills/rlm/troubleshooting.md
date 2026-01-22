# RLM Troubleshooting Guide

This document covers tips, common errors, and solutions for working with the RLM CLI.

## Best Practices

### Before Processing

1. **Start with `info`**: Check document size and token estimate before choosing a strategy
   ```bash
   rlm load document.txt
   rlm info
   # Review: totalLength, tokenEstimate, lineCount
   ```

2. **Use `slice` to explore**: View different parts of the document to understand structure
   ```bash
   rlm slice 0:2000      # First 2000 chars
   rlm slice -1000:      # Last 1000 chars
   ```

3. **Filter first**: For search tasks, filter to reduce content before processing
   ```bash
   rlm filter "search_term"  # Much smaller than full document
   ```

### During Processing

4. **Store incrementally**: Don't batch - store results after each chunk
   ```bash
   rlm store chunk_0 "finding"
   rlm next
   rlm store chunk_1 "another finding"  # Store immediately
   ```

5. **Navigate efficiently**: Use `skip` and `jump` instead of repeated `next` calls
   ```bash
   rlm skip 10       # Skip 10 chunks forward
   rlm jump 50%      # Jump to midpoint
   rlm skip -5       # Go back 5 chunks
   ```

6. **Check progress**: Monitor processing state during long sessions
   ```bash
   rlm info --progress
   # Shows: progress bar, chunks remaining, estimated tokens
   ```

### Managing Chunks

7. **Merge small chunks**: If semantic creates too many chunks, use merging
   ```bash
   rlm chunk --strategy semantic --min-size 5000 --merge-small
   # Reduces 500+ chunks to ~60 chunks
   ```

8. **Use hybrid mode**: Combine semantic structure with filter patterns
   ```bash
   rlm chunk --strategy semantic --pattern "keyword"
   ```

### Session Management

9. **Clear between tasks**: Always reset when starting fresh
   ```bash
   rlm clear
   ```

10. **Use JSON for automation**: Machine-parsable output for scripts
    ```bash
    rlm next --json
    rlm aggregate --json --final
    ```

## Common Errors

| Error                      | Cause                     | Solution                                |
|----------------------------|---------------------------|-----------------------------------------|
| "No document loaded"       | `load` not called         | Run `rlm load <file>` first             |
| "No chunks available"      | `chunk` not called        | Run `rlm chunk` after loading           |
| "No more chunks"           | All chunks processed      | Use `rlm aggregate` to get results      |
| "Cannot read source"       | File not found            | Check file path exists                  |
| "Filter requires pattern"  | Missing `--pattern`       | Add pattern argument to filter          |
| "Invalid slice range"      | Malformed range syntax    | Use format `start:end` (e.g., `0:1000`) |
| "Recursion depth exceeded" | Too many nested RLM calls | Run `rlm clear` to reset depth          |

## Validation Errors

The CLI validates documents before processing. Common validation errors:

| Error                          | Cause                           | Solution                        |
|--------------------------------|---------------------------------|---------------------------------|
| "Binary content detected"      | File contains binary data       | Use text-based files only       |
| "File exceeds size limit"      | File larger than 5MB            | Split file or use smaller input |
| "Invalid UTF-8 encoding"       | File has encoding issues        | Convert to UTF-8 encoding       |
| "Unbalanced code blocks"       | Markdown code fences don't match| Fix markdown ``` syntax         |
| "Unsupported file format"      | Unknown file extension          | Use supported format or .txt    |

## Format-Specific Issues

### PDF Issues

| Problem                        | Cause                           | Solution                        |
|--------------------------------|---------------------------------|---------------------------------|
| Empty or minimal text          | Scanned PDF without OCR         | Use OCR tool first              |
| Garbled characters             | Non-standard font encoding      | Try different PDF or re-export  |
| Missing pages                  | Encrypted or protected PDF      | Remove protection first         |
| Slow loading                   | Very large PDF (100+ pages)     | Split PDF into smaller parts    |

```bash
# Check if PDF has extractable text
rlm load document.pdf
rlm info
# If TotalLength is very small, the PDF may be scanned images
```

### Word Document Issues

| Problem                        | Cause                           | Solution                        |
|--------------------------------|---------------------------------|---------------------------------|
| "Cannot open document"         | Corrupted .docx file            | Re-save from Word               |
| Missing content                | Complex formatting/tables       | Tables may not extract fully    |
| Wrong encoding                 | Non-UTF8 characters             | Re-save with UTF-8 encoding     |
| Old .doc format                | Pre-2007 Word format            | Convert to .docx first          |

```bash
# Note: Only .docx is supported, not .doc
# Convert older files using Word or LibreOffice
```

### HTML Issues

| Problem                        | Cause                           | Solution                        |
|--------------------------------|---------------------------------|---------------------------------|
| Missing content                | JavaScript-rendered content     | Use browser "Save as HTML"      |
| Broken structure               | Malformed HTML                  | Clean HTML before loading       |
| Excessive whitespace           | CSS-only formatting             | Content converts to Markdown    |

### JSON Issues

| Problem                        | Cause                           | Solution                        |
|--------------------------------|---------------------------------|---------------------------------|
| "Invalid JSON"                 | Malformed JSON syntax           | Validate JSON first             |
| Very slow loading              | Deeply nested structure         | Flatten or split the JSON       |
| Truncated output               | Very large arrays               | Filter before loading           |

## File Size Limits

The default maximum file size is **5MB**. For larger files:

```bash
# Option 1: Split the file
split -b 4M large-file.txt part-

# Option 2: Filter content first
grep "relevant" large-file.txt > filtered.txt
rlm load filtered.txt

# Option 3: Use stdin with head/tail
head -n 100000 large-file.txt | rlm load -
```

### Recommended Approaches by Size

| File Size | Approach                                  |
|-----------|-------------------------------------------|
| < 1MB     | Load directly                             |
| 1-5MB     | Load directly, use filter strategy        |
| 5-20MB    | Pre-filter or split before loading        |
| > 20MB    | Use external tools to extract relevant sections |

## Session Persistence

The RLM session is persisted to `~/.rlm-session.json` between commands. This enables multi-turn processing:

1. Run a command
2. Analyze the output
3. Run the next command
4. Repeat until complete

### Session Contents

The session file stores:
- `Content`: The loaded document text
- `Metadata`: Document metadata (source, length, token estimate)
- `ChunkBuffer`: List of chunks from last chunking operation
- `CurrentChunkIndex`: Current position in chunk buffer
- `Results`: Dictionary of stored partial results
- `RecursionDepth`: Current depth in nested RLM calls

### Managing the Session File

```bash
# View session file (if needed for debugging)
cat ~/.rlm-session.json | jq .

# Session file location
ls -la ~/.rlm-session.json

# Clear session and start fresh
rlm clear
```

### Session Recovery

If your session becomes corrupted:

```bash
# Remove session file manually
rm ~/.rlm-session.json

# Or use clear command
rlm clear
```

## Recursion Depth Limits

To prevent infinite decomposition loops:

- Maximum recursion depth: 5 levels
- Session tracks `RecursionDepth` property
- `rlm clear` resets depth to 0
- Exceeding depth returns error instead of processing

If you see "Recursion depth exceeded":
```bash
rlm clear  # Reset to 0
rlm load document.txt  # Start fresh
```

## Performance Considerations

### Large Documents

For documents over 1MB:
- Use `--min-size` with semantic chunking to reduce chunk count
- Consider `filter` strategy if you're searching for specific content
- Use `skip` and `jump` to navigate rather than sequential `next`

### Memory Usage

The CLI loads the entire document into memory. For very large files:
- Consider pre-filtering with external tools
- Split into smaller files before processing
- Use stdin piping with streaming tools

### Token Estimation

The default token estimate (`characters / 4`) is approximate. For accurate counting:
```bash
rlm chunk --strategy token --max-tokens 4096
# Uses Microsoft.ML.Tokenizers with GPT-4 tokenizer (cl100k_base)
```

## Regex Pattern Tips

The `filter` strategy and hybrid mode use .NET regex syntax:

### Common Patterns

| Pattern        | Matches                             |
|----------------|-------------------------------------|
| `word`         | Literal "word"                      |
| `word1\|word2` | Either word                         |
| `\bword\b`     | Whole word only                     |
| `\[ERROR\]`    | Literal "[ERROR]" (escape brackets) |
| `.*`           | Any characters                      |
| `\d+`          | One or more digits                  |

### Escaping Special Characters

Escape these characters with backslash: `[ ] ( ) { } . * + ? ^ $ \ |`

```bash
# Find [ERROR] literally
rlm filter "\\[ERROR\\]"

# Find email addresses
rlm filter "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}"
```

## JSON Output Issues

If JSON output is malformed:
- Ensure document content doesn't contain unescaped JSON characters
- Use `--json` consistently throughout the session
- The `--final` flag adds `"signal": "FINAL"` to aggregate output

## Related Documentation

- [SKILL.md](SKILL.md) - Overview and workflow
- [strategies.md](strategies.md) - All chunking strategies with options
- [examples.md](examples.md) - Real-world workflow scenarios
- [reference.md](reference.md) - Technical architecture
