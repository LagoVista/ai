# IDX-035 – Deletion of Chunks When File No Longer Exists

**Status:** Accepted

## 1. Description
Defines how the indexer deletes chunks when a file from the previous run **no longer exists** in the filesystem or has been excluded.

## 2. Decision
### Orphan Detection
A file is considered missing when:
- A prior record exists in the local index
- The physical file is not present at that path

### Deletion Procedure
When missing:
1. Delete all chunks where `payload.DocId == DocId`
2. Remove record from local index
3. Do not create new chunks

### Rename/Move Cases
- Renames produce new canonical paths → new DocIds
- Old DocId is deleted under this rule

### Exclusion Cases
If a file is newly ignored or filtered out:
- It is treated as missing
- DocId is deleted

### Dry-Run Behavior
- Report DocIds that would be deleted
- No DB/index changes

## 3. Rationale
- Maintains perfect sync between filesystem, local index, and vector DB
- Prevents ghost search results from deleted or excluded files

## 4. Resolved Questions
- Recover renamed files? → No
- Depend on ContentHash or Reindex flags? → No
- Keep tombstones? → No; remove record entirely
