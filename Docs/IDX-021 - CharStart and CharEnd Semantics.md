# IDX-021 – CharStart / CharEnd Semantics

**Status:** Accepted

## 1. Description
`CharStart` and `CharEnd` describe 0-based character offsets in the **normalized** source text, enabling precise snippet extraction and editor navigation.

## 2. Decision
- Both are **optional**; may be null when chunker does not compute offsets.
- If present:
  - `CharStart ≥ 0`.
  - `CharEnd ≥ CharStart`.
  - Offsets are based on the **normalized text** (normalized line endings, trimmed, etc.).
  - Ranges are inclusive.
- Works for mid-line and mid-character (post-normalization) splits.
- When present, the substring `[CharStart … CharEnd]` must exactly match the chunk’s normalized text.
- Offsets are not required for all chunk types.

## 3. Rationale
- Character offsets provide more precise linking than lines alone.
- Optional implementation prevents forcing complexity on all chunkers.
- Inclusive range definition prevents ambiguity at boundary edges.

## 4. Resolved Questions
1. Required for all text chunks? → No.
2. Mid-character split? → Allowed; `CharEnd` remains inclusive.
3. Substring fidelity? → Yes, required.
4. Rounding/truncation rules? → No enforcement.
5. Threshold-based offset computation? → No.
