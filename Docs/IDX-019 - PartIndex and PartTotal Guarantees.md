# IDX-019 – PartIndex / PartTotal Guarantees

**Status:** Accepted

## 1. Description
Defines ordering and completeness metadata for multi-part chunk sets within a single document.

## 2. Decision
- Both `PartIndex` and `PartTotal` are **required**.
- Indexing is 1-based.
- `PartTotal` is computed after chunking is finalized.
- Guarantee: `1 ≤ PartIndex ≤ PartTotal` for every chunk.
- All chunks from the same document share the same `PartTotal`.
- No maximum limit; no optional ordering modes.

## 3. Rationale
- Downstream consumers rely on deterministic ordering.
- Explicit completeness improves traceability, debugging, and display.

## 4. Resolved Questions
1. Limit total parts? → No.
2. Require a chunk-run ID? → No.
3. Allow 0-based indexing? → No.
