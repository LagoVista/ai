# IDX-030 – Local Indexing & Persisted SubKind Override

**Status:** Accepted

## 1. Description
Defines how the ingestion engine maintains a **local index** (`local-index.json`) of previously processed files, enabling:
- Fast change detection
- Stable SubKind classification
- Manual overrides
- Controlled reindexing via a `Reindex` directive

This ensures deterministic re-ingestion and avoids re-running SubKind heuristics unless required.

## 2. Decision
Each local index record includes:
- `FilePath` (canonical)
- `DocId`
- `ContentHash` (last indexed hash)
- `SubKind` (persisted; may be manually overridden)
- `LastIndexedUtc`
- `FlagForReview` (boolean)
- `Reindex` (null | "chunk" | "full")

### Ingestion Logic
1. Compute `ContentHash` for the file.
2. Retrieve matching record by `FilePath`.
3. Behavior:
   - **Record exists + ContentHash matches + SubKind matches + no Reindex flag** → Skip indexing; update `LastIndexedUtc`.
   - **Record exists + (ContentHash differs OR SubKind differs OR Reindex="chunk")** → Reindex file; use stored SubKind; clear `Reindex`.
   - **Record missing OR Reindex="full"** → Run SubKind heuristics; index file; clear `Reindex`.

### Manual Override
Manually setting a new `SubKind` should set:
```
Reindex = "chunk"
```
forcing re-chunk & re-embed *without* running heuristics.

### Stability Rules
- Local index is authoritative for SubKind.
- Heuristics only run when required.
- Files flagged for review bubble up in logs.

## 3. Rationale
- Prevents unstable SubKind drift.
- Avoids unnecessary re-indexing.
- Allows safe manual corrections.
- Provides deterministic control via Reindex flags.

## 4. Notes
Future expansions may propagate entity changes to dependent files.
