# AGN-032 — Agent Pipeline Steps

**ID:** AGN-032  
**Title:** Agent Pipeline Steps  
**Status:** Approved

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-23 15:30:00 EST (UTC-05:00)

---

## 1. Purpose

Define the **finite, single-purpose pipeline steps** that process a single user turn, including **branching rules** and **rejoin guarantees**, as a referential map for future refactors and ad-hoc code generation.

---

## 2. Core Invariants

- **Single-purpose steps:** A step may contain conditional logic, but if it results in materially different pseudocode paths, the step must be split.
- **Branch then rejoin:** A request may branch, but must rejoin before progressing to the next shared step.
- **Context object contract:** Each step receives `AgentPipelineContext`, mutates it as needed, and either forwards it to the next step or returns an `InvokeResult` failure/abort.
- **Session terminology:** `SessionId` is the canonical term (legacy code may use `ConversationId`).
- **Client tool continuation model:** Client tool execution is part of the **same turn**; server resumes the **current turn** on return.
- **Authoritative commit point:** `AgentRequestHandler` commits `AgentSession` to durable storage once downstream returns, covering success, client-tool boundary, failure, or exception (best-effort).

---

## 3. Standard Step Map Template

Each step section should minimally state:

- **Expects:** What must already exist on `AgentPipelineContext` (high level only).
- **Updates:** What it populates or derives (high level only).
- **Next:** Possible next step(s) (finite set).

---

## 4. Step Flow Table

| Step | What it generally does | Next |
|---|---|---|
| **AgentRequestHandler** | Entry, build ctx, route request *(commits session on return)* | → **AgentSessionCreator** or **AgentSessionRestorer** or **ClientToolCallSessionRestorer** |
| **AgentSessionCreator** | Create a Session and initialize the First Turn | → **SessionContextResolver** |
| **AgentSessionRestorer** | Load existing Session and append a brand new Turn | → **SessionContextResolver** |
| **ClientToolCallSessionRestorer** | Load existing Session and load the current Turn being continued | → **SessionContextResolver** |
| **SessionContextResolver** | Load/resolve AgentContext + ConversationContext + Modes | → **ContextProviderInitializer** |
| **ContextProviderInitializer** | Initialize session/mode-based context providers | → **ClientToolContinuationResolver** or **AgentReasoner** |
| **ClientToolContinuationResolver** | Prepare continuation state from the current Turn | → **AgentReasoner** |
| **AgentReasoner** | LLM/tool loop orchestration | ↔ **LLM Client** / return |
| **LLM Client** | Model inference | → **AgentReasoner** |

---

## 5. Step Chapters

### 5.1 AgentRequestHandler

- **Expects:** Transport request and org/user identity.
- **Updates:** Constructs initial `AgentPipelineContext`; routes to one of the three session paths.
- **Next:** `AgentSessionCreator`, `AgentSessionRestorer`, or `ClientToolCallSessionRestorer`. Commits `AgentSession` to durable storage once downstream returns.

### 5.2 AgentSessionCreator

- **Expects:** New-session request.
- **Updates:** Creates Session and initializes the First Turn.
- **Next:** `SessionContextResolver`.

### 5.3 AgentSessionRestorer

- **Expects:** Existing-session request with new user input.
- **Updates:** Loads Session and appends a brand new Turn.
- **Next:** `SessionContextResolver`.

### 5.4 ClientToolCallSessionRestorer

- **Expects:** Client-tool continuation request.
- **Updates:** Loads Session and loads the current Turn being continued, including any client-tool-related state required later.
- **Next:** `SessionContextResolver`.

### 5.5 SessionContextResolver

- **Expects:** Session and Turn present.
- **Updates:** Resolves and loads AgentContext, ConversationContext, and effective mode catalog state for the turn.
- **Next:** `ContextProviderInitializer`.

### 5.6 ContextProviderInitializer

- **Expects:** Session and resolved contexts/mode.
- **Updates:** Initializes context providers (session and mode system content; KFR as applicable).
- **Next:** If client-tool continuation → `ClientToolContinuationResolver`; otherwise → `AgentReasoner`.

### 5.7 ClientToolContinuationResolver

- **Expects:** Current Turn represents a client-tool continuation boundary.
- **Updates:** Restores and normalizes tool manifest and pending tool state into the shape expected by the Reasoner and LLM flow.
- **Next:** `AgentReasoner`.

### 5.8 AgentReasoner

- **Expects:** Contexts resolved; turn-level state prepared, including tool continuation when applicable.
- **Updates:** Executes LLM/tool loop; sets response; may return client-tool boundary.
- **Next:** Iterates with `LLM Client` as needed, otherwise returns upstream.

### 5.9 LLM Client

- **Expects:** Fully prepared prompt and context, including any continuation or tool outputs.
- **Updates:** Produces model output into `AgentPipelineContext` for interpretation by the Reasoner.
- **Next:** Returns to `AgentReasoner`.

---

*End of AGN-032.*