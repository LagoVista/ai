# AGN-027 — Agent Pipeline Composition & Execution Plan (Post-Foundation)

**ID:** AGN-027  
**Title:** Agent Pipeline Composition & Execution Plan (Post-Foundation)  
**Status:** Draft  
**Type:** Implementation / Refactor Follow-Through

---

## 1. Purpose

AGN-027 formally captures the **locked execution plan** for completing the Aptix agent pipeline after the successful introduction of the **common pipeline abstraction** (`IAgentPipelineStep`, `AgentPipelineContext`).

This DDR exists to:
- Re-anchor the work to the **original six-layer architecture**
- Clearly define **what each layer owns** and **what it must not do**
- Prevent further architectural drift while implementation proceeds

AGN-027 explicitly assumes that **Foundation work (AGN-026 Step A)** is complete.

---

## 2. Non-Goals

This DDR does **not**:
- Introduce new agent behavior
- Change LLM prompts, tool semantics, or response formats
- Optimize performance or concurrency
- Redesign session persistence or storage

The goal is **structural correctness and long-term maintainability**, not feature change.

---

## 3. Locked Architectural Invariants

The following invariants are binding and must not be violated during implementation.

### 3.1 Single Responsibility Invariants

1. **Composition is centralized**  
   Only the *Composition layer* may assemble LLM request content (prompts, tools, context).

2. **Network calls are isolated**  
   Only the *LLM Call layer* may perform HTTP calls to the OpenAI Responses API.

3. **Iteration is exclusive**  
   Only the *Reasoner layer* may:
   - Loop
   - Re-invoke the LLM
   - Apply mode changes

4. **Persistence is terminal**  
   Session and turn persistence occurs only in the *Persistence layer*.

---

## 4. The Six Pipeline Layers (Authoritative)

The agent pipeline is finalized as **six logical layers**, implemented as composable `IAgentPipelineStep`s.

### Layer 1 — Composition (B)

**Responsibility:**
Prepare *everything* needed to build a valid `/responses` request.

**Key Components:**
- `IResponsesRequestComposer : IAgentPipelineStep`
- `IResponsesRequestBuilder`
- (Future) `IModeCompositionManifestFactory`

**Inputs:**
- AgentContext
- ConversationContext
- AgentExecuteRequest
- RAG block
- Tool usage metadata

**Outputs (written to context):**
- Composed prompt artifacts
- `ResponsesApiRequest`

**Explicitly must NOT:**
- Call the LLM
- Execute tools
- Loop or retry

---

### Layer 2 — LLM Call (C)

**Responsibility:**
Execute exactly one call to the LLM and normalize the response.

**Key Components:**
- `LlmCallStep : IAgentPipelineStep`
- `OpenAIResponsesClient` (adapted)

**Inputs:**
- Pre-built `ResponsesApiRequest`

**Outputs:**
- Raw response JSON
- Parsed `AgentExecuteResponse`

**Explicitly must NOT:**
- Modify prompts or tools
- Execute tools
- Loop

---

### Layer 3 — Server Tool Execution (D)

**Responsibility:**
Execute **server-side tools only**, sequentially and deterministically.

**Key Components:**
- `ServerToolExecutionStep : IAgentPipelineStep`
- `IAgentToolExecutor`

**Behavior:**
- Unknown tools are treated as **client tools**
- Server tool failure fails the pipeline
- Tool outputs are normalized to **Responses API tool output shape**

**Outputs:**
- `ToolResultsJson`
- Executed server tool list
- Pending client tool list

---

### Layer 4 — Reasoner Loop & Mode Switching (E)

**Responsibility:**
Own the entire reasoning lifecycle.

**Key Component:**
- `ReasonerStep : IAgentPipelineStep`

**Behavior:**
- Controls iteration limits
- Invokes Composition → LLM → Tools
- Applies mode changes
- Prepends welcome messages
- Stops on client tools

**Critical Rule:**
Only this layer may re-enter earlier layers.

---

### Layer 5 — Persistence / Turn Lifecycle (F)

**Responsibility:**
Durably record request and response state.

**Key Steps:**
- Begin session / create turn
- Persist request transcript
- Persist response transcript
- Complete / fail / abort turn

**Error Policy:**
- Any pipeline failure fails the **turn**, not the session
- Failures are loud and visible

---

### Layer 6 — Pipeline Entry & Replacement (G)

**Responsibility:**
Route existing execution paths into the new pipeline.

**Key Work:**
- Replace legacy orchestration paths
- Preserve external APIs
- Remove obsolete code once stable

---

## 5. Error & Failure Model (Reaffirmed)

- All pipeline steps return `InvokeResult<T>`
- `InvokeResult.Successful == false` implies **no Result value**
- Failures immediately stop the pipeline
- Failures are returned directly to the user
- System/Fatal errors are expected to be rare and treated as bugs

---

## 6. Implementation Order (Locked)

With Foundation complete, work must proceed strictly in this order:

1. **B — Composition**
2. **C — LLM Call**
3. **D — Server Tools**
4. **E — Reasoner Loop**
5. **F — Persistence**
6. **G — Call Chain Replacement**
7. **H — Composition Trace (Optional)**

No later layer may be implemented before an earlier layer is complete and tested.

---

## 7. Composition Trace (Optional but Sanctioned)

A composition trace may be added to:
- Diagnose prompt assembly
- Audit multi-iteration reasoning

This is **explicitly optional** and must not block core implementation.

---

## 8. Exit Criteria

AGN-027 is considered complete when:
- All six layers are implemented as pipeline steps
- Legacy orchestration is removed or bypassed
- End-to-end execution matches pre-refactor behavior
- Pipeline contracts remain stable

---

## 9. Relationship to Other DDRs

- **AGN-026** — Foundation & pipeline abstractions
- **AGN-011 / AGN-013** — Mode semantics
- **AGN-003** — Responses API request construction

AGN-027 defines how those decisions are *executed*, not redefined.
