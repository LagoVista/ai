# IDX-034 – Deletion of Stale Chunks

**Status:** Accepted

## 1. Description
Defines deterministic rules for removing **stale** vector chunks for any DocId selected for re-indexing.

## 2. Decision
### Stale Chunk Definition
A chunk is stale if:
- It belongs to a DocId being re-indexed
- It does **not** appear in the newly generated chunk set

### Replace-All-Per-DocId Strategy
For any DocId selected for re-indexing:
1. Delete **all** existing chunks for that DocId
2. Insert all newly generated chunks
3. Never attempt diffing or granular updates

### Reindex Triggers
Deletion happens only when:
- `ContentHash` changed, or
- `Reindex = "chunk" or "full"`, or
- Heuristics triggered reclassification

### Dry-Run Behavior
- Report which DocIds and chunk counts *would* be deleted
- No actual DB changes

### Non-text Assets
Exactly the same rule applies.

### Stability Guarantees
- At end of run, only **one generation** of chunks exists per DocId
- `PointId`s are not stable
- DocId is the identity boundary

## 3. Rationale
- Completely avoids stale or orphaned chunks
- Avoids complexity of per-chunk diffs
- Guarantees deterministic state after each run

## 4. Resolved Questions
- Fine-grained diffing? → No
- Delete chunks for unprocessed DocIds? → No (IDX-035 handles that)
- PointId stability? → Not preserved
