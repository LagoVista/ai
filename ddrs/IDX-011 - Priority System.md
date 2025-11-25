# IDX-011 – Priority System

**Status:** Accepted

## 1. Description
`Priority` provides a numeric signal for ranking importance or relevance of a chunk in retrieval scenarios. Lower numbers indicate higher importance.

## 2. Decision
- Valid range: **1 to 10** (integer only).
- `Priority = 1` is highest priority.
- Different asset groups may use different heuristics within this range.
- Priority values are adjustable over time.
- No decimals or fractional values.
- Meaning of levels is not fully defined yet, but will be documented later.

## 3. Rationale
- A simple 1–10 integer scale is expressive without complexity.
- Allows asset-specific heuristics.
- Easy to sort and compare.
- Allows dynamic recalibration as content evolves.

## 4. Resolved Questions
1. Range? → 1–10.
2. Define all levels now? → Deferred.
3. Separate scales per asset kind? → Allowed.
4. Fractions? → No.
5. Immutable? → No; may be recalculated.
