# IDX-014 – Token Field Definitions

**Status:** Accepted

## 1. Description
Defines fields for describing token-related metrics associated with a chunk:
- `EstimatedTokens`: required estimate.
- `ChunkSizeTokens`: optional measured size.
- `OverlapTokens`: optional sliding-window overlap.

## 2. Decision
- `EstimatedTokens` is **required** and must be a positive integer.
- `ChunkSizeTokens` and `OverlapTokens` are **optional**.
- Use PascalCase.
- Estimated tokens may be heuristic; actual token count goes into `ChunkSizeTokens`.
- Overlap only populated when sliding-window logic is applied.

## 3. Rationale
- Estimates provide cost-awareness for embedding.
- Optional fields allow richer analysis without forcing all chunkers to support them.
- Consistent naming across all metadata.

## 4. Resolved Questions
1. Enforce max size? → Deferred.
2. Validate overlap < size? → No enforcement.
3. If chunker is line-based, omit estimate? → No; still compute estimate.
4. Require rounding rules? → No.
5. Track input/output token usage? → Not at this stage.
