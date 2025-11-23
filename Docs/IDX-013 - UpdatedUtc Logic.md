# IDX-013 – UpdatedUtc Logic

**Status:** Accepted

## 1. Description
`UpdatedUtc` would represent when a document was last modified. Under current ingestion rules—where indexing only occurs when content has changed—it provides no functional value.

## 2. Decision
- Remove the `UpdatedUtc` field from the metadata contract.
- Ingestion detects changes via pipeline logic, not timestamps.
- Field remains documented as a placeholder if future auditing/versioning requires it.

## 3. Rationale
- Eliminates redundant metadata.
- Indexing only occurs when needed; no need to store modification timestamps.
- Simplifies payload shape.

## 4. Notes
`UpdatedUtc` may be revisited if incremental-update workflows are introduced.
