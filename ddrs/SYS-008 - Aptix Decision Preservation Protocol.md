# SYS-008 — Aptix Decision Preservation Protocol

**ID:** SYS-008  
**Title:** Aptix Decision Preservation Protocol  
**Status:** Draft  
**Owner:** Kevin Wolf & Aptix

## 1. Purpose
This DDR defines the Decision Preservation Protocol (DPP-01) — a foundational collaboration and system-interaction contract ensuring that no decision, clarification, requirement, or sub-answer is ever lost during multi-step workflows with Aptix. DPP-01 governs how Aptix captures, structures, records, validates, and retains decisions across architectural sessions, DDR authoring, mode development, tool specifications, and complex design threads involving many steps. The protocol ensures reliability, precision, and auditability in all iterative work between human SMEs and the Aptix system.

## 2. High-Level Summary
DPP-01 introduces a strict, formalized system that guarantees: (1) Every decision receives a stable ID; (2) All decisions appear in a Decision Table before any section is locked; (3) Final specifications must contain every decision; (4) Locked decisions become immutable until explicitly reopened; (5) Aptix must support traceability, regeneration, and diffing; (6) No part of a multi-step Q&A may be collapsed or omitted; (7) User overrides always supersede earlier decisions. DPP-01 becomes part of the Aptix operational foundation, affecting future DDR imports, tool behaviors, workflow definitions, and internal LLM prompt engineering.

## 3. System Responsibilities
### 3.1 Aptix Responsibilities
Aptix MUST: (1) Assign stable IDs to every decision (e.g., A4.1–Decision); (2) Capture all decisions verbatim — no collapsing or summarizing unless instructed; (3) Produce a full Decision Table before locking; (4) Generate a Narrative Spec only after user approves the Decision Table; (5) Ensure all decisions appear in the Narrative Spec; (6) Support Reprint, Diff, and Traceability operations on locked decisions; (7) Ask clarifying questions when ambiguities arise; (8) Freeze locked sections until explicitly reopened; (9) Maintain deterministic regeneration fidelity; (10) Store and reuse locked decisions across sessions.

### 3.2 User Responsibilities
The SME must: (1) Provide answers; (2) Approve or reject decision tables; (3) Reopen sections when changes are needed. Aptix must never require the SME to restate a finalized decision unless explicitly asked.

## 4. Workflow Rules
DPP-01 mandates these behaviors: (1) Every multi-part question sequence uses numbered decision IDs; (2) Before any spec is locked, Aptix generates a Decision Table showing all IDs and answers; (3) Only after explicit user approval may Aptix generate a Narrative Spec; (4) Locked decisions remain stable across sessions; (5) Regeneration must reproduce content verbatim unless reopened; (6) Summaries or collapses are prohibited unless asked for.

## 5. Failure Modes and Safeguards
If Aptix detects missing, ambiguous, or contradictory decisions, it must pause, highlight the issue, and request clarification — never guess. If a user request conflicts with a locked decision, Aptix must warn and request an explicit reopening.

## 6. Regeneration Guarantees
Aptix must support exact regeneration of any locked section on demand (e.g. “Reprint A4 exactly as locked”), preserving formatting, content, and ordering. Narrative specs must contain all decisions without omission.

## 7. Rationale
This protocol exists to eliminate lost detail, summary drift, and time-wasting re-explanations in complex multi-step design efforts. It embeds reliability and auditability into all Aptix-led workflows.

## 8. Status
Draft — pending user approval and tool-driven persistence.
