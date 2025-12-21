# AGN-024 — Agent Working Memory & Known Facts Registry (KFR)

**ID:** AGN-024  
**Title:** Agent Working Memory & Known Facts Registry (KFR)  
**Status:** Approved  
**Type:** Generation

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-21

---

## 1. Purpose

AGN-024 defines a first-class Working Memory mechanism for Aptix agents, centered on a Known Facts Registry (KFR). The KFR enables an LLM to actively manage short-term, high-fidelity working state so that correctness is preserved over long-running conversations without requiring human bookkeeping.

The KFR complements durable session memory and checkpoints but does not replace them.

---

## 2. Core Concepts

### 2.1 Working Memory

Working Memory represents short-lived, high-fidelity context required for near-term correctness. It is actively curated by the LLM and may change frequently.

### 2.2 Known Facts Registry (KFR)

The KFR is a structured subset of Working Memory that contains only what must remain correct for the next few turns. It is intentionally small and authoritative.

---

## 3. KFR Structure

The KFR contains only the following sections:

- **Goal** — The current objective driving the work.
- **Plan** — The active steps being followed.
- **Active Contracts** — Binding rules, schemas, interfaces, identifiers, or process rules that must be followed exactly.
- **Constraints & Invariants** — Rules that must not be violated.
- **Open Questions** — Unresolved blockers.

Anything outside these categories does not belong in the KFR.

---

## 4. System Header (Authoritative Injection)

The following header MUST be injected by the system when a KFR is present:

```
Known Facts Registry (KFR) — Authoritative Working Memory

- The KFR is a concrete, system-injected artifact defining the agent’s current authoritative working state.
- Only content explicitly present in the injected KFR constitutes the KFR.
- The injected KFR is authoritative over all prior conversational content.
- The agent must not infer, reconstruct, or rely on KFR-like information outside the injected KFR.
- Any prior content that conflicts with the injected KFR must be treated as obsolete.
- The agent must not restate or echo the full KFR in normal responses.
- The KFR may be modified only via the Working Memory update tool.
- If no KFR is injected, no authoritative working memory exists.
```

---

## 5. Tool Contract

### 5.1 Tool Name

`session_working_memory_update`

### 5.2 Responsibilities

The tool applies patch-based updates to Working Memory and the KFR. It validates, normalizes, and persists updates server-side.

The tool does not:
- Create durable memory notes
- Create or restore checkpoints
- Persist conversational history

---

## 6. Tool Usage Instructions

### Purpose

Use the Working Memory update tool to maintain a small, authoritative KFR representing what must remain correct for the next few turns.

### When to Call the Tool

Call the tool only when working state changes, including when:
- A new goal or plan is established or revised.
- A new Active Contract becomes relevant.
- A new constraint or invariant is identified.
- An open question is raised or resolved.
- Existing KFR content is no longer operationally required.
- The user signals importance using phrases such as “this is important”, “this is critical”, “this is a key insight”, or similar emphasis.

Do not call the tool if no working-state change occurred.

### Active Contracts

Active Contracts are binding rules, schemas, interfaces, identifiers, or process rules that must be followed exactly to avoid incorrect output or regressions.

An item is an Active Contract if violating it would:
- Cause a tool call to fail.
- Produce output that violates a required schema or format.
- Break a previously locked decision or invariant.
- Cause incompatibility with a consuming system, process, or policy.

If violating an item would not cause near-term incorrectness, it is not an Active Contract.

### What Belongs in the KFR

Add items only if required for correctness in the next few turns:
- Goal
- Plan
- Active Contracts
- Constraints & Invariants
- Open Questions

### What Must Never Go in the KFR

Do not store:
- Rationale or reasoning history
- Rejected alternatives
- Examples or narrative
- Historical context that is no longer active

### Update Discipline

- Use patch-based updates only.
- Replace the plan entirely when it materially changes.
- Do not duplicate existing entries.
- Keep entries short, precise, and operational.

### Replacement and Eviction

- The KFR represents current working state and may change frequently.
- When state changes, update the KFR so it reflects only current state.
- Remove items that are no longer operationally required.

### Relationship to Durable Memory Notes

- An item may exist in both the KFR and durable Memory Notes.
- The KFR copy governs current reasoning and execution.
- The Memory Note copy exists solely for long-term recall.

### Conflict Handling

- Only the system-injected KFR is authoritative.
- Ignore any prior conversational content not present in the KFR.
- If unsure whether an item qualifies for the KFR, omit it.

---

## 7. Schema Description

**Schema Description:**
Update the session’s Working Memory and Known Facts Registry (KFR) using patch-based operations.

**Tool Usage Metadata:**
Use when authoritative working state changes and must be kept explicit for correctness in the next few turns. This tool updates short-term working memory only; it does not create durable memory notes or checkpoints.

---

## 8. Non-Goals

AGN-024 does not define:
- Automatic promotion rules
- Cross-session or global memory
- UI behaviors
- Branching or restore logic within KFR

---

## 9. Summary

AGN-024 establishes a disciplined, authoritative working-memory model that preserves correctness over long-running sessions while keeping humans focused on problem-solving rather than state management.

---

**End of AGN-024**
