# AGN-026 — Agent Pipeline Refactor Checklist and Composition Trace

**ID:** AGN-026  
**Title:** Agent Pipeline Refactor Checklist and Composition Trace  
**Status:** Draft  
**Ddr Type:** 

---

## 1. Purpose

AGN-026 defines a **no-behavior-change** refactor plan to reorganize the Agent execution pipeline into composable steps with a standardized signature, improved testability, and a durable **Composition Trace**.

This DDR is intentionally implementation-oriented, but focuses on **contracts and structure**, not re-architecture.

---

## 2. Goals

- Standardize pipeline step signatures to reduce churn and stabilize unit tests.
- Improve organization and observability via a Composition Trace.
- Enable incremental migration with temporary seams while keeping tests green.
- Preserve current runtime behavior.

---

## 3. Non-Goals

- No concurrency or threading model changes.
- No behavioral changes to agent responses, tool execution, or persistence semantics.
- No performance optimizations beyond what falls out naturally from refactoring.

---

## 4. Reliability Rules

- The pipeline relies on `InvokeResult<T>` propagation (not exceptions) for expected failures.
- If `InvokeResult.Successful == false`, there will **never** be value in `Result`.
- If anything fails (and is not explicitly classified as a warning), the call must **fail loudly** and return the error message to the user.
- System/fatal errors should be rare and treated as likely programming bugs.

---

## 5. Standard Pipeline Contract

### 5.1 Canonical step signature

All pipeline steps MUST implement the same signature:

- `Task<InvokeResult<AgentPipelineContext>> ExecuteAsync(AgentPipelineContext ctx, CancellationToken ct = default)`

### 5.2 Context-in / Context-out

- Steps mutate `AgentPipelineContext` (in-memory) and return it wrapped in `InvokeResult`.
- Failures return `InvokeResult` with `Successful == false` and no usable Result.

### 5.3 Session flow rule

- The `AgentSession` is carried through the pipeline via `AgentPipelineContext`.
- Persistence may still occur at defined boundaries, but the pipeline’s primary mechanism is **in-memory state flow**.

---

## 6. Composition Trace

### 6.1 What it is

A durable trace of what happened while composing a single agent execution, suitable for:

- diagnostics
- auditing composition decisions
- debugging “why did the model see X?”

### 6.2 What it records

Each step should append an entry including (at minimum):

- Step name
- Start timestamp / stop timestamp
- Summary of actions performed
- High-level list of inputs appended to the eventual LLM request (not raw secrets)
- Warnings produced
- Persistence actions (what was written and where)

### 6.3 Storage

- The trace lives on the `AgentPipelineContext`.
- The trace may optionally be persisted to blob storage as a single artifact per request.

---

## 7. Migration Strategy

### 7.1 One-class-at-a-time

- Adopt one class to the new signature.
- Fix/adjust tests for that class.
- Only then proceed to the next class.

### 7.2 Temporary seams

During migration only:

- A predecessor may inject a temporary `IAgentPipelineStep` (or similarly shaped interface) to avoid breaking the entire chain.
- After the downstream step is migrated, the predecessor is updated to reference the final intended interface/type.
- Temporary seams are removed once their neighboring steps are migrated.

---

## 8. Pipeline Layers (Target)

This is the intended order (actual class names may vary slightly):

1. AgentRequestHandler
2. AgentOrchestrator
3. AgentTurnExecutor
4. AgentExecutionService
5. AgentReasoner
6. OpenAIResponsesClient (LLM Client)

---

## 9. Checklist (Execution Plan)

### A0 — Foundations

- [x] `AgentPipelineContext` exists.
- [x] `CompositionTrace` exists and is attached to `AgentPipelineContext`.
- [x] `ResponsesRequestBuilder` is an injected service (no behavior change).
- [ ] Confirm `AgentPipelineContext` contains all required flow objects:
  - Session
  - Request
  - Response
  - Org/User
  - CorrelationId
  - CompositionTrace
  - CancellationToken
  - Any per-turn identifiers needed

### A1 — AgentRequestHandler (Start here)

- [ ] Create step contract (temporary or final) using canonical signature.
- [ ] Refactor AgentRequestHandler to:
  - Create `AgentPipelineContext`
  - Populate base fields
  - Call next step
  - Return `InvokeResult<AgentPipelineContext>`
- [ ] Update unit tests:
  - Mock next step
  - Validate context creation + routing
  - Ensure failure paths fail loudly

### A2 — AgentOrchestrator

- [ ] Adopt canonical signature.
- [ ] Preserve new session vs follow-up behavior.
- [ ] Tests: mock downstream `AgentTurnExecutor` step.

### A3 — AgentTurnExecutor

- [ ] Adopt canonical signature.
- [ ] Preserve transcript persistence behavior.
- [ ] Tests: mock `AgentExecutionService` step.

### A4 — AgentExecutionService

- [ ] Adopt canonical signature.
- [ ] Preserve mode normalization + conversation context selection.
- [ ] Tests: mock downstream `AgentReasoner` step.

### A5 — AgentReasoner

- [ ] Adopt canonical signature.
- [ ] Preserve loop + mode-change behavior.
- [ ] Tests: mock LLM + tool executor.

### A6 — OpenAIResponsesClient

- [ ] Adopt canonical signature.
- [ ] Preserve request build + SSE/non-stream logic.
- [ ] Keep existing tests green.

### A7 — Remove temporary seams

- [ ] As each step is migrated, update predecessor to depend on the final intended interface and remove temporary adapters.

---

## 10. Notes

- This DDR is draft and expected to evolve while migrating the pipeline.
- The primary constraint is maintaining behavior while stabilizing signatures and improving organization.
