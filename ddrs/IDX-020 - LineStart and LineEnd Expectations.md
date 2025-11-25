# IDX-020 – LineStart / LineEnd Expectations

**Status:** Accepted

## 1. Description
`LineStart` and `LineEnd` indicate the inclusive 1-based line range covered by a chunk within its source document. These fields provide strong traceability for linking embeddings back to exact locations in original text.

## 2. Decision
- `LineStart` and `LineEnd` are **required** for all **text-based** chunks.
- `LineStart ≥ 1`.
- `LineEnd ≥ LineStart`.
- Range is **inclusive**: chunk covers lines `[LineStart … LineEnd]`.
- If the chunker splits mid-line due to token or overlap constraints, `LineEnd` may equal `LineStart`.
- `CharStart` and `CharEnd` remain **optional** and may be null if not tracked.
- No maximum line-span limit.
- Any single source line exceeding **500 characters** must be truncated at 500 characters before chunking.
- For non-text assets, `LineStart`/`LineEnd` may be null.

## 3. Rationale
- Enables tooling to reference exact file lines (“lines 101–128”).
- Required fields for text chunks ensure consistent coverage.
- Mid-line handling prevents pathological split conditions.
- 500-character truncation protects against massive one-line blobs.
- Keeping char offsets optional avoids forcing complexity on all chunkers.

## 4. Resolved Questions
1. Required for text-based chunks? → Yes.
2. Track `CharStart`/`CharEnd`? → Yes, optional.
3. Mid-line splits allowed? → Yes.
4. Maximum number of lines per chunk? → No.
5. Nullability for non-text? → Line fields may be null.
