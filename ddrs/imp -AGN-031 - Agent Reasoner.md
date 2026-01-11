# AGN-031 — Agent Reasoner

**ID:** AGN-031  
**Title:** Agent Reasoner  
**Status:** Approved

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-23

---

# 1. Purpose

AGN-031 defines the behavioral contract, lifecycle rules, and refactoring strategy for the Aptix **Agent Reasoner**.

The goal of this DDR is to:
- Replace a complex, monolithic reasoner implementation with a simple, explicit turn orchestrator
- Preserve all existing runtime behavior
- Clearly assign responsibilities across the pipeline
- Enable deterministic multi-step tool execution and client continuation

This DDR is **generation-focused** and exists to produce concrete work products.

---

# 2. Scope and Non-Goals

## In Scope
- Reasoner behavior and lifecycle
- Tool loop orchestration
- Mode switching semantics
- Turn vs session context handling
- Client tool handoff and re-entry
- Observability requirements
- Refactoring and cutover strategy

## Out of Scope
- Prompt composition details
- Storage implementation choices
- UI or client behavior
- Tool internal semantics

---

# 3. Terminology

- **Turn**: A single user request handled by the reasoner
- **Session**: A durable conversational context spanning turns
- **LLM Call**: A single outbound request to the LLM API
- **Exit Mode 1**: Turn completed successfully
- **Exit Mode 2**: Turn requires client-side tool execution
- **ContextProvider**: Component responsible for supplying prompt context

---

# 4. Tool Loop Contract

- Tool calls are issued by the LLM and executed sequentially
- Server tools execute synchronously via the ToolExecutor
- Unregistered tools are a hard error
- First tool failure terminates the turn immediately
- Client tools always have a server-side partner step

---

# 5. Continuation via ContextProviders

- Continuation is expressed entirely through ContextProviders
- The reasoner does not mutate request payloads for continuation
- Tool results are written into providers by the executor or pipeline
- Turn-scoped context is consumed once and then cleared by the reasoner
- Session-scoped context persists across turns

---

# 6. Client Tool Handoff and Re-Entry

- Exit Mode 2 returns pending client tool calls
- Provider state is persisted by the pipeline, not the reasoner
- On re-entry, provider state must already be restored
- The reasoner verifies all expected tool calls have completed successfully
- Missing or mismatched tool results cause a hard failure

---

# 7. Provider State Persistence and Rehydration

- Provider state persistence must be cluster-safe
- Persistence is owned by the pipeline
- A repository interface is required for save/load operations
- The reasoner assumes restored state exists on re-entry

The ToolResults ContextProvider also serves as the **tool ledger**, tracking:
- expected tool calls
- completion status
- execution results

---

# 8. Mode Switching and Bootstrap

- Mode changes are performed exclusively by the ModeStateChange tool
- Mode changes may occur mid-turn and apply to the next LLM call
- Tool availability may change between LLM calls

## Bootstrap Rules

- Welcome messages and initialization instructions are **turn-scoped**
- Bootstrap content is injected once via turn context
- Bootstrap is armed by ModeStateChange or session initialization
- The reasoner clears turn-scoped context immediately after emission

---

# 9. Observability Contract

- The reasoner emits a **CompositionTrace** for each execution
- Trace steps are ordered, stable, and lightweight
- Trace records:
  - LLM calls
  - tool execution
  - mode changes
  - exit modes
  - continuation and re-entry verification

Correctness must never depend on logs or streaming events.

---

# 10. Simplified Reasoner Design

The reasoner is a deterministic turn loop that:
- validates state up front
- calls the LLM
- executes tools
- verifies correctness
- clears turn context
- exits via one of two modes

The reasoner:
- does **not** populate providers
- does **not** persist state
- does **not** compose prompts

Its only provider mutation is clearing turn-scoped context after emission.

---

# 11. Refactoring Strategy

## Strategy

- Explicitly shift responsibilities out of the reasoner
- Replace the existing implementation wholesale
- Add aggressive validation before execution
- Keep methods small (≈20 LOC)
- Introduce helper services when they simplify logic

## Testing

Required tests include:
- normal turn completion
- server tool continuation
- mid-turn mode change
- Exit Mode 2 client continuation
- re-entry verification failures
- turn context clearing

---

# 12. External Pipeline Responsibilities

## Upstream Pipeline

- Pre-populate all ContextProviders before reasoner execution
- Restore provider state before client re-entry
- Initialize mode state for sessions

## Tool Execution

- ToolExecutor populates ToolResults / ToolLedger provider for server tools
- Pipeline re-entry handler populates ToolResults provider for client tools

## Persistence

- Provider state persistence is cluster-safe and repository-backed
- Reasoner does not manage storage

## Routing

- Pipeline distinguishes between:
  - new turn requests
  - client tool re-entry requests
- Server-side continuation never leaks outside the reasoner

## Prompt Construction

- Request builder materializes provider output into LLM requests
- Session context is emitted every call
- Turn context is emitted once

## Non-Responsibilities of the Reasoner

The reasoner does **not**:
- populate providers
- persist or rehydrate state
- compose prompts
- mutate session-scoped context

---

# End of AGN-031
