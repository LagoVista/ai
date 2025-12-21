# AGN-025 — KFR Lifecycle & Working Memory Management

**ID:** AGN-025  
**Title:** KFR Lifecycle & Working Memory Management  
**Status:** Approved  
**Type:** Generation

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-21

---

## 1. Purpose

AGN-025 defines the lifecycle, structure, and governance of Known Facts Registry (KFR) entries as ephemeral working memory within an agent session. This DDR establishes how KFR entries are created, updated, superseded, promoted, and discarded to preserve near-term correctness while avoiding long-term context pollution.

---

## 2. Scope

This DDR applies to:

- Session-scoped working memory only
- A single active body of KFR entries per session
- LLM-managed lifecycle with minimal human intervention

This DDR explicitly does **not** define branching or multi-scope behavior, though it does not preclude such extensions in the future.

---

## 3. Lifecycle Principles

- KFR entries exist solely to preserve **near-term correctness**.
- KFR entries are **atomic** and independently managed.
- KFR entries are **ephemeral** and expire by default.
- Only **active KFR entries** are injected into the system prompt.
- KFRs are authoritative but disposable.

---

## 4. KFR Kinds and Cardinality (Authoritative)

The following KFR kinds are fixed and exhaustive for this DDR.

### 4.1 Goal

- Defines what the agent is trying to accomplish right now.
- **Cardinality:** Single
- **Lifecycle Rule:** Replaced when the goal changes.

### 4.2 Plan

- Defines the current execution approach.
- **Cardinality:** Single
- **Lifecycle Rule:** Replaced when the plan changes.

### 4.3 ActiveContract

- Binding rules, schemas, identifiers, interfaces, or process constraints that must be followed exactly.
- **Cardinality:** Multiple
- **Lifecycle Rule:** Added and removed independently.

### 4.4 Constraint

- Hard limits or invariants that must not be violated.
- **Cardinality:** Multiple
- **Lifecycle Rule:** Added and removed independently.

### 4.5 OpenQuestion

- An unresolved unknown that blocks correctness or forward progress.
- **Cardinality:** Multiple
- **Lifecycle Rule:** Removed only when resolved or explicitly dismissed.
- **Special Requirement:** Must set `RequiresResolution = true`.

---

## 5. Creation

A KFR entry is created when information becomes **binding for near-term correctness**, including:

- Establishing or changing a goal or plan
- Identifying a binding contract or constraint
- Discovering an unresolved question
- Explicit user emphasis indicating critical importance

---

## 6. Update and Replacement

- KFR entries are **replaced, not amended**.
- For single-cardinality kinds (Goal, Plan):
  - Creating a new entry deactivates the prior entry of the same kind.
- For multi-cardinality kinds:
  - Entries are added or removed independently.

---

## 7. Resolution Gate

Any KFR entry may be marked with:

- `RequiresResolution = true`

Resolution-required entries:

- **Must not be evicted**
- **Must not be forgotten**
- **Must block context switch or working-memory abandonment**

This applies primarily to **OpenQuestion**, but may be used for other kinds when explicitly required.

---

## 8. Promotion

If a KFR entry remains relevant beyond the immediate task:

- It **must be promoted** to durable session memory (e.g., Memory Notes).
- Once promoted, it **must be removed** from the KFR.

---

## 9. Removal

A KFR entry is removed when:

- It is resolved
- It is superseded
- It no longer affects near-term correctness
- The current context completes or changes

Removal is expected and encouraged to keep the working set minimal.

---

## 10. Human Interaction

The human is not expected to manage individual KFR entries.

The system may expose the following high-level operations:

- **List KFRs** — Display all active KFR entries for transparency and diagnostics.
- **Clear KFRs** — Truncate the entire KFR set to reset working memory.

No piecemeal editing or manual lifecycle management is expected or required.

---

## 11. Non-Goals

KFRs must not contain:

- Todos or task tracking
- Reasoning history or rationale
- Narrative context
- Historical reference material

If an item does not affect **near-term correctness**, it does not belong in the KFR.

---

## 12. Summary

AGN-025 establishes a disciplined lifecycle for Known Facts Registry entries as ephemeral working memory, enabling high-fidelity reasoning over long-running sessions while preventing stale or irrelevant context from accumulating.

---

**End of AGN-025**
