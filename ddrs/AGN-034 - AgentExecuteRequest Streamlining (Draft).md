# AGN-034 — AgentExecuteRequest Streamlining (Draft)

**ID:** AGN-034  
**Title:** AgentExecuteRequest Streamlining  
**Status:** Draft

---

## 1. Purpose

AGN-034 defines the public **AgentExecuteRequest** contract used to invoke agent execution within the Aptix platform.

The request contract is intentionally:
- minimal
- deterministic
- model-agnostic
- easy to validate

The contract expresses **user intent**, not execution mechanics, provider state, or server-internal context.

---

## 2. Request Scenarios

Exactly two request scenarios are supported.

### 2.1 User Turn Request

Used when initiating a new agent turn.

**Required:**
- `SessionId`
- `TurnId`

**At least one input required:**
- `Instruction`
- File or image inputs

**Optional:**
- Context inputs (workspace, repo, language)
- RAG scope hints
- Streaming preference

**Forbidden:**
- Tool results
- Mode identifiers
- Provider or model continuation identifiers

---

### 2.2 Tool Continuation Submission

Used when returning tool execution results for an existing turn.

**Required:**
- `SessionId`
- `TurnId`
- `ToolResults[]`

**Forbidden:**
- Instruction or prompt inputs
- File or image inputs
- Context inputs
- Streaming flags

Tool continuation requests are mechanical only.

---

## 3. Top-Level Request Envelope

The request envelope is intentionally small.

**Required fields:**
- `SessionId`
- `TurnId`

Request type is determined by field presence, not an explicit discriminator.

Mode information, diagnostics, correlation identifiers, and provider-specific state are server-owned and must never appear in the request.

---

## 4. Context Inputs

### 4.1 Instruction

- UTF-8 text
- Markdown canonical
- Optional

A request may omit instruction entirely if intent is conveyed through files.

---

### 4.2 File and Image Inputs

- Inputs only
- May be references or inline attachments
- Allowed only on User Turn Requests

---

### 4.3 RAG Scope Inputs

The client may influence retrieval using a platform-agnostic scope definition.

Each condition includes:
- `Key`
- `Operator` (`==`, `!=`, `contains`, `does_not_contain`)
- `Values[]`

All conditions are ANDed. Semantics are advisory.

---

### 4.4 Workspace and Environment Hints

Optional hints such as:
- Workspace identifier
- Repository name
- Language hint

These are advisory only and must not be relied on for correctness.

---

### 4.5 Streaming Preference

- Optional boolean
- Defaults to `false`
- If `true`, responses are streamed
- A client that sets streaming must support streamed handling

Streaming affects delivery only, not request semantics.

---

## 5. Tool Results Submission

Tool results may be submitted only as part of a Tool Continuation Submission.

Each result includes:
- `ToolCallId`
- `ExecutionMs`
- Exactly one of:
  - `ResultJson`
  - `ErrorMessage`

Returned results must exactly match the requested tool calls in identity and order. Any mismatch is a hard fault.

---

## 6. Status

This DDR is a **draft**.

No generated code, compatibility guarantees, or migration behavior is implied at this stage.

---

*End of AGN-034 Draft*
