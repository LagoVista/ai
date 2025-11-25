# IDX-015 – SourceSha256 Rules (Deprecated)

**Status:** Deprecated

## 1. Description
`SourceSha256` previously stored a document-wide hash for change detection. This is no longer used.

## 2. Decision
- Remove `SourceSha256` from the ingestion contract.
- Change detection relies solely on `ContentHash`.
- Existing historical values may remain but are ignored.
- Tooling should not expect or emit this field.

## 3. Rationale
- Document-level hashing is redundant under full-file reindex strategy.
- Simplifies the metadata surface.

## 4. Notes
Could be reintroduced if future incremental diffing strategies require it.
