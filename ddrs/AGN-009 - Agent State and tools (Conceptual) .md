# AGN-009 — Agent Reasoning, State, and Context Reconstruction DDR (Research Draft)

**ID:** AGN-009  
**Title:** Agent Reasoning, State, and Context Reconstruction Strategy  
**Status:** Research Draft (Non-Production)  
**Owner:** Aptix + Kevin Wolf  
**Purpose:** Explore, evaluate, and document patterns for deterministic use of LLM reasoning in conjunction with the Responses API, DDR workflows, thread branching, and agent-managed state. This DDR captures research insights, not hardened implementation requirements.

---

## 1. Scope & Intent
This DDR investigates *how* an LLM-driven agent can safely and deterministically manage:
- Conversation state across the Responses API
- Use of `previous_response_id` as a performance and continuity mechanism
- Controlled rehydration of authoritative state (Goal, SystemUnderstanding, DDR status)
- Long-running multi-thread workflows (chapters, phases)
- Deterministic state transitions using an agent-owned state machine
- Token-budget behavior, truncation, drift, and how to counteract them

This document **does not** mandate implementation but establishes conceptual guardrails that inform future production DDRs.

---

## 2. Key Research Conclusions (High-Level)
1. **LLMs are stateless unless state is explicitly passed each call.**  
   `previous_response_id` does *not* replace agent-managed state.

2. **`previous_response_id` provides a cached branch history**, but:
   - It is bounded by model context window.
   - Older content can be truncated or compressed.
   - It cannot be relied on for long-term correctness.

3. **Authoritative truth must live in agent/DDR storage**, not conversation history.

4. **Agent rehydration is required** before any deterministic reasoning step.

5. **Branches (chapters) form a conversation tree**, each starting from an anchor node M (Goal + SystemUnderstanding), but this tree is logical only—the model still has one context window.

6. **Token windows decay in fidelity over time.**  
   Empirical safe ranges:
   - <20K tokens: high fidelity
   - 20–40K: caution, begin summarizing
   - 40–80K: drift likely
   - >80K: expect truncation

7. **Summaries are the backbone of deterministic reasoning.**  
   The agent must distill raw conversation/code into persistent records before context window pressure causes loss.

8. **System prompts must be minimal and stable.**  
   All evolving logic is agent-driven.

---

## 3. MasterContext (M) — The Research Model
M represents a stable anchor state:

**M = { Goal, SystemUnderstanding }

Where:**
- **Goal** comes from DDRGoal phase and is user-approved.
- **SystemUnderstanding** comes from LLM-led context discovery and is user-approved.

Once approved:
- M is **snapshotted** into agent storage.
- M's `previous_response_id` becomes an *optional* fast-start anchor.
- M must be **rehydrated explicitly** for deterministic behavior.

---

## 4. Branching & Chapter Threads (Research Model)
A chapter thread is created by:
- Starting from the M anchor (via DDR snapshot, not relying on memory)
- Injecting current DDR state
- Allowing exploration until the user approves/pends

Each chapter thread:
- Has its own `previous_response_id` chain
- May grow large and drift
- Is periodically summarized and rolled forward into a new, smaller thread
- Produces formal DDR updates upon user approval

This forms a *deterministic multi-branch workflow* on top of a nondeterministic LLM substrate.

---

## 5. Rehydration Strategy (Research Model)
Before any serious reasoning request, the agent provides:
- Goal (from DDR)
- SystemUnderstanding (from DDR)
- Current DDR section(s)
- Chapter summary (if applicable)
- Any RAG summaries relevant to the chapter

The LLM **must not** depend on prior conversation for authoritative facts.

This resolves drift, token overflow, and loss of early reasoning.

---

## 6. Interaction With `previous_response_id` (Research Model)
### Observed behaviors:
- Using a fresh `previous_response_id` provides a fast, cached local context.
- It does *not* guarantee access to all earlier reasoning.
- If the chain exceeds the context window, older content drops silently.
- Branch explosions do not pollute anchor nodes (response storage is immutable).

### Recommended safe rule:
- Use `previous_response_id` **only** for short-term continuity.  
- Use DDR snapshot rehydration for correctness.
- Use summaries to reset token budgets when branches grow.

---

## 7. Agent State Machine (Research Model)
Phases studied:
1. **DDRGoal** — intent extraction, user alignment
2. **SystemUnderstanding** — LLM-led querying + RAG-backed context
3. **Design** — 50K-foot chapters + chapter-by-chapter refinement
4. **CodePlanning** — shapes of code, interfaces, files touched

Each phase:
- Has predictable tools available
- Has approval gates enforced by the agent
- May use multiple thread branches
- Writes determinations into DDR storage

Future work: CodeImplementation, review workflows.

---

## 8. Tool Activation Strategy (Research Model)
Tools must be **phase-aware**. Key insights:
- Limiting tools reduces latency significantly.
- LLM performance increases when tool choice space is small.
- Agent can pre-bundle deterministic tool outputs to eliminate unnecessary tool calls.

Example: during Goal phase, expose *only* TLA catalog + DDR allocation tools.

---

## 9. Token Management & Summarization
Observations:
- Threads naturally grow beyond safe token windows.
- Summaries are required to lift durable facts out of volatile history.
- Chapter rollovers (new threads) should occur when estimated token count approaches ~40K.
- Agent estimation of tokens per branch is feasible and recommended.

---

## 10. Conclusions & Recommendations
This DDR is **research-only**, but firmly establishes:
- Deterministic reasoning requires *agent-owned state, not LLM memory*.
- `previous_response_id` is a *performance cache*, not a source of truth.
- Summaries + rehydration form the backbone of correctness.
- Phased tool visibility dramatically improves determinism and speed.
- All long-lived truth must reside in DDR storage.

Future DDRs will convert these research insights into **production-grade protocols**.

---

## 11. Glossary (Research)
- **M (MasterContext)** — Anchor state (Goal + SystemUnderstanding)
- **Branch** — A conversation path via `previous_response_id`
- **Chapter Thread** — Branch representing a DDR chapter
- **Rehydration** — Injecting authoritative state for deterministic reasoning
- **Summarization** — Extracting durable facts from volatile threads
- **Context Window** — Max tokens model can consider in one call

---

## 12. Status
This DDR is exploratory and informs future implementation DDRs. Marked as:
- **Research Draft**
- **Non-binding**
- **Not yet enforced in agent runtime**
