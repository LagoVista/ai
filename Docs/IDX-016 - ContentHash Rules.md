# IDX-016 – ContentHash Rules

**Status:** Accepted

## 1. Description
`ContentHash` is a SHA-256 hash of the normalized text for each chunk. It is the authoritative mechanism for determining whether a chunk has changed.

## 2. Decision
- `ContentHash` is **required** for all chunks.
- Computed on **normalized text**, after chunking.
- Hash format: lowercase 64-hex string.
- Change detection compares newly computed hash to stored version.
- If normalization or chunking logic changes, treat entire document as changed.

## 3. Rationale
- Ensures deterministic detection of modifications.
- Using SHA-256 provides collision resistance.
- Eliminates need for separate document hash.

## 4. Resolved Questions
1. Always SHA-256? → Yes.
2. What happens if chunking rules change? → Reindex everything.
3. Include metadata values in hash? → No; text only.
4. Should timestamp of hash be stored? → No.
