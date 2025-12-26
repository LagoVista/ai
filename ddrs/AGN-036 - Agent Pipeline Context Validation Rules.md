# AGN-036 — Agent Pipeline Context Validation Rules

**ID:** AGN-036  
**Title:** Agent Pipeline Context Validation Rules  
**Status:** Approved  
**DDR Type:** Policy / Rules / Governance

## Approval Metadata
- **Approved By:** _TBD_  
- **Approval Timestamp:** _TBD_

---

## 1. Purpose

AGN-036 defines the **authoritative validation rules** for `IAgentPipelineContext` across the Aptix agent execution pipeline.

The goals of this DDR are to:
- Establish a **trustable and invariant pipeline context**
- Centralize validation logic in a **single, stateless validator**
- Prevent validation rules from being scattered across pipeline steps
- Enable deterministic reasoning, testing, and debugging

This DDR is strictly concerned with **validation rules**. It does **not** define execution behavior, persistence, or UI semantics.

---

## 2. Validation Model

All validation is performed by a single service:

- `IAgentPipelineContextValidator`

The validator is:
- **Stateless**
- **Non-mutating** (must never modify `IAgentPipelineContext`)
- Purely evaluative, returning `InvokeResult`

### 2.1 Validator Entry Points (LOCKED)

- `ValidateCore(ctx)`
- `ValidatePreStep(ctx, step)`
- `ValidatePostStep(ctx, step)`
- `ValidateToolCallManifest(manifest)`

---

## 3. Core Context Invariants (ValidateCore)

The following invariants MUST always be true after context creation:

- `ctx.Type` is a valid `AgentPipelineContextTypes` value
- `ctx.TimeStamp` is non-empty
- `ctx.CorrelationId` is non-empty
- `ctx.Envelope.Org` is present
- `ctx.Envelope.User` is present

### 3.1 Type-Based Envelope Rules

#### Initial / FollowOn
- At least one of the following must be provided:
  - `Envelope.Instructions`
  - `Envelope.InputArtifacts`
  - `Envelope.ClipBoardImages`

#### Initial
- `Envelope.SessionId` MUST be empty
- `Envelope.TurnId` MUST be empty
- `Envelope.ToolResults` MUST be empty

#### FollowOn
- `Envelope.SessionId` MUST be present
- `Envelope.TurnId` MUST be present
- `Envelope.ToolResults` MUST be empty

#### ClientToolCallContinuation
- `Envelope.SessionId` MUST be present
- `Envelope.TurnId` MUST be present
- `Envelope.ToolResults` MUST contain one or more entries

---

## 4. Pipeline Step Validation Rules

Each pipeline step defines explicit **PRE** and **POST** validation rules.

### 4.1 RequestHandler
- **PRE:** N/A
- **POST:** `ValidateCore(ctx)` must succeed

---

### 4.2 SessionRestorer
- **PRE:**
  - `Envelope.SessionId` present
  - `Envelope.TurnId` present
  - `ctx.Session == null`
  - `ctx.Turn == null`
- **POST:**
  - `ctx.Session` populated
  - `ctx.Turn` populated
  - `ctx.Turn.Id != Envelope.TurnId`
  - `Session.Mode` MUST have a value

---

### 4.3 AgentContextResolver
- **PRE:**
  - No `Session` or `Turn` populated
- **POST:**
  - `AgentContext` populated
  - `ConversationContext` populated

---

### 4.4 ClientToolContinuationResolver
- **PRE:**
  - `Session` and `Turn` populated
  - `Envelope.ToolResults` present
- **POST:**
  - `ToolCallManifest` populated and valid
  - `ctx.Turn.Id == Envelope.TurnId`

---

### 4.5 AgentSessionCreator
- **PRE:**
  - `AgentContext` and `ConversationContext` populated
  - `Session` and `Turn` are null
- **POST:**
  - `Session` populated
  - `Turn` populated

---

### 4.6 AgentContextLoader
- **PRE:**
  - `Session` and `Turn` populated
- **POST:**
  - `AgentContext` and `ConversationContext` populated

---

### 4.7 PromptContentProviderInitializer
- **PRE:**
  - `Session`, `Turn`, `AgentContext`, `ConversationContext` populated
  - If tool continuation, `ToolCallManifest` must be non-null
- **POST:**
  - Prompt knowledge provider considered **Ready**

---

### 4.8 Reasoner
- **PRE:**
  - Core context valid
  - Prompt knowledge provider Ready
- **POST:** N/A

---

### 4.9 LLMClient
- **PRE:**
  - `ResponsePayload == null`
  - If `ctx.Type == ClientToolCallContinuation`, validate ToolCallManifest
- **POST:** Exactly one outcome:
  - **Final:** `ResponsePayload` populated, no client tool calls
  - **ToolContinuation:** client tool calls present, no ResponsePayload

---

### 4.10 ResponseBuilder
- **PRE:**
  - `ResponseType ∈ { Final, ToolContinuation }`
- **POST:**
  - Context invariants unchanged (PRE == POST)
  - Returned response conforms to AGN-033

---

## 5. ToolCallManifest — Validity Definition (LOCKED)

A ToolCallManifest is valid if and only if:

- ToolCalls and ToolCallResults:
  - Have the same count
  - Are in the same order
  - Have matching `ToolCallId`
  - Have matching `Name`
- **All ToolCallResults MUST contain `ResultJson`**
  - Any failure is a hard gate
  - Failure is bubbled with tool id, name, and reason

---

## 6. Non-Goals

This DDR does not define:
- Execution behavior
- Error handling UX
- Turn status transitions
- Persistence strategy

---

## 7. Status

This DDR is a **draft** pending final human approval per SYS-001.
