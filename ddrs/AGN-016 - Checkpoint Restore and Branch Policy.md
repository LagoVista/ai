# AGN-016 — Checkpoint Restore and Branch Policy

**ID:** AGN-016  
**Title:** Checkpoint Restore and Branch Policy  
**Status:** Approved  
**Owner:** Kevin Wolf & Aptix  

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-16 17:00:00 EST (UTC-05:00)

---

## Guiding Principle

> **Aptix favors free-form creativity by default, and introduces structure only when the user signals readiness or when durability is at risk.**

---

## 1. Background & Problem Statement

Aptix agent sessions support extended, high-value workflows (software design, debugging, architecture exploration). Over time, sessions accumulate transcripts, tool results, file context, and historical decisions.

When propagated continuously to the LLM, growing context leads to increased latency, higher token usage/cost, reduced responsiveness, and higher risk of summarization or loss of early critical details.

Naively continuing a conversation relies on the LLM to retain all constraints indefinitely. In software workflows, tiny invariants (e.g., “never divide by zero”) can have outsized impact many turns later.

Aptix therefore introduces two complementary mechanisms:
- **Memory Notes** preserve *what was learned* (facts, decisions, invariants).
- **Checkpoints** preserve *where the session was* (a restore anchor to a specific turn).

The system must allow users to reset LLM context for performance while preserving correctness, trust, and recoverability.

---

## 2. Definitions & Terminology

### 2.1 Session
A durable, server-side record of user↔agent interaction. Includes ordered turns, metadata (mode/context/workspace/repo), memory notes, checkpoints, and artifact references.

### 2.2 Turn
A single user↔agent exchange, including tool invocations and metadata. Turns are ordered within a session.

### 2.3 Checkpoint
A durable marker associated with a session and a specific restore anchor (`TurnSourceId`). Stores label and UTC timestamp. Does not store transcript text or LLM context.

### 2.4 Restore
The act of resuming work from a checkpoint. In Aptix, restore produces a branched session rather than mutating the original session.

### 2.5 Branch
A new session derived from an existing session at a checkpoint. Preserves lineage metadata and continues independently.

#### Turn Identity Constraint
A **TurnId is never globally unique on its own**. A turn must always be referenced as the tuple:

```
(SessionId, TurnId)
```

Branching may create an exact copy of a session up to a checkpoint and preserve identical `TurnId` values across sessions to maintain internal references and traceability. Therefore, any operation that resolves a turn must include the associated `SessionId`.

### 2.6 Active LLM Chain
The sequence of LLM responses linked via mechanisms such as `previous_response_id`. The chain is ephemeral, performance-sensitive, and must never be treated as authoritative session state.

### 2.7 Durable Session State
All persisted, authoritative data stored on the server (session metadata, turns, memory notes, checkpoints, file refs/hashes, mode history, artifacts).

---

## 3. Design Goals & Non-Goals

### 3.1 Goals
- **Performance Recovery:** enable lowering context load to regain responsiveness.
- **Correctness and Safety:** preserve critical decisions/invariants via durable state.
- **Explicitness and Transparency:** restore/branch must be visible and inspectable.
- **History Preservation:** never mutate or destroy historical sessions.
- **Minimal Cognitive Load:** summaries by default; details on demand.
- **Deterministic Lineage:** branches record source session/checkpoint/turn and timestamps.

### 3.2 Non-Goals
- Conversation “rewind” inside the same session.
- Guaranteed semantic equivalence of future reasoning.
- Automatic/implicit restore.
- Cross-branch merging.
- Full transcript replay.
- Any guarantee that the LLM retains constraints indefinitely without durable capture.

### 3.3 Guiding Principle
**Performance recovery must never come at the cost of correctness or trust.**

---

## 4. High-Level Restore Model

### 4.1 Restore Is a Branch, Not a Rewind
Restore creates a new session derived from a prior session at a checkpoint. The original session remains intact.

### 4.2 Source and Target Sessions
- **Source Session:** existing session containing the checkpoint.
- **Target Session (Branch):** new session that references source session and checkpoint and continues forward.

### 4.3 Checkpoint as the Restore Anchor
A checkpoint identifies where to branch via a turn reference. It does not snapshot conversation text or LLM context.

### 4.4 Lineage
Every restore-created branch records:
- Source SessionId
- Source CheckpointId
- Source turn reference tuple
- Restore timestamp (UTC)

### 4.5 Restore Modes
To support both creativity and performance recovery, restore supports two modes:
- **Soft Restore:** continuity-first; may preserve conversational continuity aids; does not guarantee token reset.
- **Hard Restore:** performance-first; starts a new LLM chain and uses a bounded state pack.

---

## 5. State Transfer Policy

### 5.2 State Categories (Refined and Non-RAG-Dependent)
Restore policy operates on durable session state and system-observed artifacts. Optimization strategies (including RAG) are implementation details.

#### 5.2.1 Identity & Lineage
Session/branch and checkpoint lineage identifiers and restore timestamps.

#### 5.2.2 Durable Knowledge
Memory notes, mode, mode history.

#### 5.2.3 Working Set (System-Observed Artifacts)
- **Active File References:** path, hash, size, touched flags, sent-to-LLM flags.
- **Chunk References:** chunk ids + ranges + hashes.
- **System-Generated Artifacts:** blob URLs, applied patches, persisted tool outputs.
- **Pinned Artifacts (Optional):** explicitly promoted artifacts (approved outlines, ground rules, key excerpts, contracts).

#### 5.2.4 Explicit Exclusions
Not part of restore state:
- raw transcripts
- assistant explanations or reasoning traces
- casual/unreferenced links
- external content not acted upon by tools
- full file contents unless explicitly pinned

#### 5.2.5 Rehydration Provider (Non-Normative)
Context rehydration may use direct reads, chunk reads, cached excerpts, or RAG-derived summaries/snippets. Restore semantics do not depend on a specific provider.

### 5.3 Transfer Rules

#### 5.3.1 Restore Modes
- **Soft Restore (Continuity-First):** preserves flow; may keep conversational continuity aids; no guarantee of token reset.
- **Hard Restore (Performance-First):** always starts a new LLM chain; bounded state pack; deterministic reset behavior.

#### 5.3.2 Rules by Category
- **Identity & Lineage:** always transferred.
- **Durable Knowledge:** always transferred; hard restore may inject only a filtered subset into the state pack while retaining all notes durably.
- **Active File References:** always transferred; **file contents are never auto-injected**.
- **Chunk References:** always transferred for audit; never auto-injected.
- **System-Generated Artifacts:** identifiers/links transferred; payloads not auto-injected.
- **Pinned Artifacts:** transferred; eligible for state pack inclusion (bounded).
- **Ephemeral Chain State:** optional in soft restore; never carried in hard restore.

#### 5.3.3 Hard Restore State Pack
Hard restore injects a bounded state pack that may include:
- restore notice + lineage
- mode + reason
- memory notes (filtered/ordered)
- active file list (paths + hashes)
- pinned artifacts
- optional small continuity window

Hard requirements:
- token budget enforced
- no implicit replay of full files/transcripts
- anything not included is not guaranteed to be present

#### 5.3.4 Soft Restore Expectations
Soft restore favors continuity and nuance, accepts performance drift, and must keep hard restore available as an explicit option.

---

## 6. Token Reset Strategy

Token reset is a necessary, intentional operation for performance recovery.

- **Hard Restore** is the mechanism for token reset: new LLM chain, no `previous_response_id` reuse, bounded state pack, tool-driven rehydration.
- Survivability is defined by policy: memory notes/checkpoints/lineage/mode/file refs/pinned artifacts survive; raw transcripts and implicit conversational memory do not.

User experience:
- summary by default (“restored, new branch created, context reset”)
- details on demand (what was included vs deferred; warnings)

---

## 7. Logging, Audit, and User Transparency

### 7.1 Technical / System Logging
Restore operations emit structured events with required correlation fields:
- sessionId, sourceSessionId, checkpointId, source turn tuple, restoreMode, operationId, user/org ids, UTC timestamps.

Minimum event types:
- RestoreRequested
- RestoreValidated
- BranchCreated
- StateTransferred
- StatePackPrepared (Hard)
- LLMChainReset (Hard)
- RestoreCompleted
- RestoreFailed

Also log drift (hash mismatch), truncation (budget), missing artifacts, and fallbacks.

### 7.2 User-Facing Restore Summary (Default)
Concise confirmation (non-intrusive): checkpoint label/id, new session created, whether context reset occurred, high-level carry-forward statement, link to details.

### 7.3 User-Facing Restore Details (On Demand)
Full lineage, timestamps (UTC stored; local rendered), counts/lists transferred, what was reset, warnings, links to source session and branch.

### 7.4 Transparency Without Distraction
Default: short summary. Optional: full detail. Never: dumping logs into the main chat flow.

---

## 8. User Experience & Mental Model

Users should internalize:
- sessions are workspaces
- checkpoints are save points
- restore creates a new branch (does not erase history)
- hard restore resets performance, not intent

Suggested framing:
- “Continue with context” (Soft)
- “Reset context for performance” (Hard)

Users should be able to navigate source vs branch timelines and view restore summaries/details on demand.

---

## 9. Implementation Notes (Non-Normative)

- Always address turns as `(SessionId, TurnId)`.
- Restore creates a new session; never mutate the source session.
- Hard restore starts a new chain and enforces token budgets; do not fail restore solely due to token limits.
- Prefer tool-driven, batched rehydration of needed file regions.
- Store timestamps in UTC ISO 8601; render locally in UI.
- Use structured logs; avoid logging raw prompts.
- Optimizations (caching, summaries, RAG) must not change restore semantics.

---

## 10. Future Considerations

- **Artifact Relevance Feedback (Placeholder):** allow the agent to record which injected artifacts were authoritative/helpful/noise, including “suppress on hard restore” and “pin for restore” guidance.
- Artifact ranking/scoring as advisory inputs for future state pack construction.
- Selective restore profiles (performance-first vs continuity-first) without altering semantics.
- Merge semantics explicitly out of scope until a dedicated DDR exists.
- RAG-enhanced rehydration providers as optional optimizations.
- Cross-session knowledge promotion (explicit and auditable).
- Telemetry-driven suggestions (advisory only).

---

## 11. Approval Metadata & Finalization

**Status:** Approved

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-16 17:00:00 EST (UTC-05:00)

Change control:
- Revisions require explicit reopening of AGN-016.
- Additive enhancements are permitted if they do not alter restore semantics.
