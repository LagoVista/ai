# Aptix Orchestrator — Design Decision Record (DDR)

This document captures all finalized architectural decisions from our orchestrator design sessions.

---

## ORCH‑001 — Decoupled, Testable Orchestrator Architecture
**Status:** Accepted  
**Title:** Orchestrator delegates all domain work to injected collaborators  
**Decision:**  
The Orchestrator *must* act as a coordinator only—no business logic, no RAG, no LLM calls. All real work is delegated to interfaces such as:
- `IAgentSessionManager`
- `IAgentSessionFactory`
- `IAgentTurnExecutor`
- `INotificationPublisher`
- `IAgentContextManager`
- `IAgentNamingService`

The orchestrator must remain extremely thin to support mocking and testability.

**Rationale:**  
Keeps orchestration deterministic, mockable, composable, and free of business logic.

---

## ORCH‑002 — Notification Model & Event Stream Semantics
**Status:** Accepted  
**Title:** Only orchestrator-owned lifecycle events are published  
**Decision:**  
We define `AptixOrchestratorEvent` with fields `{SessionId, TurnId, Stage, Status, Message, ElapsedMs, Timestamp}`.  
Orchestrator publishes:
- `SessionStarted`
- `TurnCreated`
- `TurnExecutionStarted`
- `TurnCompleted`
- `TurnFailed`

RAG-phase and LLM-phase events are *not* emitted by the orchestrator—they belong to the RAG and LLM subsystems.

**Rationale:**  
Maintains layered responsibility boundaries.

---

## ORCH‑003 — Session Creation & Turn Initialization
**Status:** Accepted  
**Title:** Session and turn creation delegated to SessionFactory  
**Decision:**  
`IAgentSessionFactory` generates:
- New session from `NewAgentExecutionSession`
- First turn for a new session
- Turns for follow-up requests

**Rationale:**  
Prevents the orchestrator from knowing session/turn construction rules.

---

## ORCH‑004 — Validation Strategy
**Status:** Accepted  
**Title:** Orchestrator validates envelope, collaborators validate domain  
**Decision:**  
Orchestrator validates request *shape*; domain validators enforce property-level correctness.

**Rationale:**  
Separates structural vs. semantic validation.

---

## ORCH‑005 — Error Handling & Turn Failure Semantics
**Status:** Accepted  
**Title:** All failure paths must result in `FailAgentSessionTurnAsync`  
**Decision:**  
When any component errors:
1. Turn is marked failed via `FailAgentSessionTurnAsync`
2. Orchestrator emits `TurnFailed`
3. Caller receives an error-wrapped `InvokeResult`

**Rationale:**  
Ensures event-stream consistency and traceability.

---

## ORCH‑006 — RAG & LLM Streaming Events
**Status:** Accepted  
**Title:** RAG and LLM publish their own streaming events  
**Decision:**  
The orchestrator **does not** emit RAG/LLM progress events.  
Instead:
- RAGAnswerService may emit e.g. `RagStarted`, `RagChunkSelected`, `RagCompleted`
- LLMClient may emit `LlmDelta`, `LlmCompleted`

**Rationale:**  
Prevents the orchestrator from owning subsystem semantics.  
LLM streaming is forwarded through `INotificationPublisher`.

---

## ORCH‑007 — Transcript Storage (Requests + Responses)
**Status:** Accepted  
**Title:** Turn transcript storage handled by dedicated service  
**Decision:**  
Introduce `IAgentTurnTranscriptStore` storing:
- Full request JSON
- Full response JSON

Orchestrator stores only summary fields and blob URLs.

**Rationale:**  
Enables auditability without polluting session/turn records.

---

## ORCH‑008 — LLM Client Architecture (Responses API + Streaming)
**Status:** Accepted  
**Title:** Use OpenAI Responses API + rich streaming  
**Decision:**  
`ILLMClient` supports both:
- `GetAnswerAsync(...)` for blocking full-answer calls
- `GetStreamingAnswerAsync(...)` for real-time deltas

Streaming mode forwards:
- `LlmStarted`
- `LlmDelta`
- `LlmCompleted`

**Rationale:**  
Provides real-time feedback (narration) and parity with ChatGPT-style UX.

---

## ORCH‑009 — Naming Service for Auto‑Generated Session Names
**Status:** Accepted  
**Title:** Summarize requests into human-readable names  
**Decision:**  
`IAgentNamingService.GenerateSessionNameAsync()`  
Uses OpenAI to turn instructions into short session names.

Injected into orchestrator so all session creation flows use it.

**Rationale:**  
Fixes `EntityBase` validation errors and produces friendly session history entries.

---

## ORCH‑010 — Argument/Parameter Formatting Rule
**Status:** Accepted  
**Title:** Mandatory formatting convention for Aptix code generation  
**Decision:**  
Strict rules:
1. **Never break parameters/arguments across lines**  
2. **Always break class initializers across lines**  
3. **Never exceed 120 chars unless unavoidable**  
4. **If >120 chars, wrap *before parameters begin*** (Option B rule)

**Rationale:**  
Improves readability and matches existing NuvOS codebase style.

---

## ORCH‑011 — Agent Turn Executor Refactor
**Status:** Accepted  
**Title:** Orchestrator loads AgentContext and passes into TurnExecutor  
**Decision:**  
`AgentTurnExecutor` no longer loads context.  
`AgentOrchestrator` retrieves context once per turn and injects it into:
- `ExecuteNewSessionTurnAsync(...)`
- `ExecuteFollowupTurnAsync(...)`

**Rationale:**  
Ensures consistent context usage and reduces dependency scattering.

---

## ORCH‑012 — Request Normalization Layer (AgentRequestHandler)
**Status:** Accepted  
**Title:** Request handler normalizes incoming payloads for all client types  
**Decision:**  
`IAgentRequestHandler` produces either:
- `NewAgentExecutionSession`
- `AgentExecutionRequest`

And later will format outbound responses depending on client type (Web, CLI, Thick client).

**Rationale:**  
Gives us a stable integration boundary and future‑proofs the platform.

---

## Current System Architecture Diagram (Textual)

```
Controller
   ↓
AgentRequestHandler
   ↓
AgentOrchestrator
   ├─ AgentSessionFactory
   ├─ AgentSessionManager
   ├─ AgentNamingService
   ├─ AgentTurnExecutor
   │     ├─ AgentExecutionService
   │     ├─ RagAnswerService
   │     │     └─ QdrantClient + Embedder
   │     └─ LLMClient (Responses API + Streaming)
   └─ NotificationPublisher
```

---

If you'd like this converted to PDF, included in the repo, versioned, or extended with diagrams, I can generate those immediately.  
