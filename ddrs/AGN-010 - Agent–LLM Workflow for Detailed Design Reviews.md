# AGN-010 — Agent–LLM Workflow for Detailed Design Reviews

**ID:** AGN-010  
**Title:** Agent–LLM Workflow for Detailed Design Reviews  
**Status:** Approved  

## Approval Metadata
- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-05 17:05:00 EST (UTC-05:00)

---

# 1. Purpose and Scope
AGN-010 defines the end-to-end workflow model for how an Aptix Agent and an LLM collaborate to create, refine, validate, and finalize Detailed Design Reviews (DDRs) using deterministic tool operations defined in TUL-005. This DDR governs workflow phases, interaction rules, approvals, mini-sessions, token lifecycle, session restarts, and the required message schemas and envelopes.

AGN-010 does not cover RAG indexing design, non-DDR agent features, tool implementation details, or PDF rendering internals.

---

# 2. Primary Goals
The DDR workflow must be deterministic, auditable, phased, durable, recoverable, and token-safe. The LLM must rely on fresh DDR state provided by the Agent rather than its own long-term memory. Durable summaries, baseline response anchors, and per-chapter mini-sessions ensure no information is lost in long-running workflows.

---

# 3. Workflow Phases

## 3.1 Goal Refinement
User states intent to create a DDR. The LMM collaboratively refines the goal and ends by issuing a `request_user_approval` call.

## 3.2 Goal Approval
User approves goal using the client-side tool. Approved → proceed. Tabled → save and pause. NotApproved → refine.

## 3.3 Content / RAG Discovery
LLM performs RAG queries, surfaces artifacts, requests user confirmation, and finalizes discovery notes. No DDR is created yet.

## 3.4 DDR Creation & 50K-Foot Chapters
LLM calls `create_ddr`; proposes chapters; user refines; LLM commits with `add_chapters` after explicit approval.

## 3.5 Summary Refinement
For each chapter: refine summary only. Store changes via TUL-005 tools.

## 3.6 Detail Refinement
LLM expands detailed content, echoing all decisions, constraints, and edge cases. Agent may request durable summaries based on token thresholds.

## 3.7 Chapter Approval
LLM requests user approval before calling `approve_chapter`.

## 3.8 DDR Approval
LLM proposes final DDR approval → user approves → `approve_ddr`.

## 3.9 DDR Output Generation
Summary or Detail DDR is generated **only on explicit user request**; never automatically.

---

# 4. Chapter Mini-Sessions and Context Anchoring
Each chapter operates as a mini-session with its own `previous_response_id`.

## 4.1 Mode 1 — Fresh Boot
Agent sends full DDR state, chapter context, and overlays without a previous response ID.

## 4.2 Mode 2 — Warm Start
Agent resumes session using the baseline anchor `M` plus current DDR state.

## 4.3 DDR State Overlay
On every turn, the Agent transmits fresh DDR state; the LLM must never rely solely on model memory.

---

# 5. Token Lifecycle and Durable Summary Checkpoints

## 5.1 Monitoring
Agent monitors prompt and context token usage.

## 5.2 Threshold
Agent triggers a durable summary request when thresholds are exceeded.

## 5.3 LLM Summary
LLM emits a full implementation roll-up of all decisions to date.

## 5.4 Commit + Reset
Agent writes summary into chapter details, resets context, and restarts the mini-session using Mode 1 or Mode 2.

---

# 6. Approval Workflow and Safeguards
All protected operations require explicit approval using `request_user_approval`.

## 6.1 LLM Use of Approval Tool
LLM must request approval before performing any protected state mutation.

## 6.2 Pending Approval Blocking
Agent must suspend user messages until approval is resolved.

## 6.3 Final Outcomes
- Approved → perform tool call
- Tabled → save; do not proceed
- NotApproved → refine

---

# 7. User Approval Resolution (Subsection 7.3)

## 7.3.1 Required Outcomes
Every approval request must conclude with one: Approved, Tabled, NotApproved.

## 7.3.2 Implicit NotApproved
If user types instead of clicking Approve/Table, the Agent must return a NotApproved result.

## 7.3.3 Silent Period
No auto-action in V1; the next user message resolves as NotApproved.

## 7.3.4 Post-Decision Behavior
LLM proceeds, pauses, or revises accordingly.

---

# 8. DDR Output Forms

## 8.1 User-Requested Only
DDR outputs are never automatically generated.

## 8.2 Summary DDR
High-level, human-facing overview; no technical detail.

## 8.3 Detail DDR
Implementation-grade document including all decisions, constraints, and details.

## 8.4 PDF Support
Agent handles PDF transformations.

## 8.5 Outputs Never Mutate DDR
Outputs are projections of stored DDR, not mutations.

---

# 9. Agent ↔ LLM Interaction Protocol (Hybrid)

## 9.1 Behavioral Rules
- Agent always provides fresh DDR state.
- LLM performs exactly one state-changing action per turn.
- All mutations occur via TUL-005 tools.
- Agent blocks user input during pending approvals.
- Token resets are agent-managed.
- Session restarts use Mode 1 or Mode 2.

## 9.2 Formal Message Structure

### 9.2.1 Agent → LLM
Agent sends DDR state, phase, chapter ID, pending approval status, previous response ID, mode, token usage, and feature flags.

### 9.2.2 LLM → Agent
LLM may issue:
- Structured tool call
- Natural-language question
- User-requested DDR output

### 9.2.3 Tool Response Envelope
Follows TUL-005 success/failure schema, including approval decision results.

## 9.3 Protocol Guarantees
Each tool call receives exactly one response. No invalid operations proceed. Drift is impossible because state is freshly transmitted.

## 9.4 Error Recovery
Malformed or illegal tool calls must generate structured errors; LLM must correct and retry.

---

# End of DDR
