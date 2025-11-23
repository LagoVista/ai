# IDX-036 – Local Index File Format & Maintenance Rules

**Status:** Accepted

## 1. Description
Defines the storage format, location, atomic write rules, and lifecycle of the local index (`local-index.json`).

## 2. Decision
### Record Shape
Stored as an array of objects:
```json
{
  "FilePath": "string",
  "DocId": "string",
  "ContentHash": "string",
  "ActiveContentHash": "string or null",
  "SubKind": "string or null",
  "LastIndexedUtc": "ISO-8601 timestamp",
  "FlagForReview": "boolean or null",
  "Reindex": "null | 'chunk' | 'full'"
}
```

### Location
```
<repo>/.nuvos/index/local-index.json
```

### Startup
- If file exists → load
- If corrupt → rename to `.corrupt.json` and recreate
- If missing → start empty

### Maintenance Rules
- Compute `ActiveContentHash` for each file
- If mismatch between Active vs stored ContentHash → file is Active
- Manual SubKind overrides persist indefinitely
- After successful ingestion:
  - `ContentHash = ActiveContentHash`
  - Clear Reindex
  - Update LastIndexedUtc
- Missing files trigger removal (IDX-035)

### Atomic Writes
After each file:
1. Sort entries by FilePath
2. Write to `local-index.json.tmp`
3. Replace the real file atomically

## 3. Rationale
- Crash-safe incremental updates
- SubKind override stability
- Precise identification of files requiring LLM injection

## 4. Notes
Local index is always local-only; no synchronization.
