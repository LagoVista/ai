# AGN-034 — AgentExecuteRequest Streamlining

**ID:** AGN-034  
**Title:** AgentExecuteRequest Streamlining  
**Status:** Approved

## Approval Metadata
- **Approved By:** Kevin D. Wolf  
- **Approval Timestamp:** 2025-12-24 10:27:00 ET (UTC-05:00)

---

## 1. Purpose

AGN-034 defines the public **AgentExecuteRequest** contract used to invoke agent execution within the Aptix platform.

The request contract is intentionally:
- minimal
- deterministic
- model-agnostic
- easy to validate

The contract expresses **user intent**, not execution mechanics, provider-specific state, or server-internal context.

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
- `InputArtifacts[]`
- `ClipboardImages[]`

**Optional:**
- Context inputs
- RAG scope hints
- Streaming preference
- AgentContextId
- ConversationContextId

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
- Input artifacts or clipboard images
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

A request may omit instruction entirely if intent is conveyed through files or images.

---

### 4.2 Input Artifacts

`InputArtifacts[]` represents files or artifacts supplied by the client for agent reasoning.

Each artifact includes:
- `RelativePath` — relative to the VS Code opened root folder
- `FileName`
- `Contents`
- `Origin` — `ide` or `user`

Optional fields:
- `MimeType`
- `Language`
- `Encoding` — `utf8` (default) or `base64`

**Rules:**
- Allowed only for User Turn Requests
- Contents are treated as advisory inputs
- No absolute paths are permitted

---

### 4.3 Clipboard Images

`ClipboardImages[]` represents images pasted from the clipboard.

Each entry includes:
- `Id`
- `MimeType`
- `DataBase64`

Clipboard images are provenance-blind and distinct from file-based artifacts.

---

### 4.4 RAG Scope Inputs

The client may influence retrieval using a platform-agnostic scope definition.

Each condition includes:
- `Key`
- `Operator` — `==`, `!=`, `contains`, `does_not_contain`
- `Values[]`

All conditions are ANDed. Semantics are advisory.

---

### 4.5 Solution Context Hints

#### SolutionContextText

- Optional free-form string
- Describes observed solution or workspace context
- Examples: presence of `agent.aptix`, repo name, project type, directory conventions

**Session-latched semantics:**
- When present, replaces the session’s stored solution context
- When absent, the stored context remains unchanged

The server may inject this context into internal prompts on subsequent turns.

---

### 4.6 Workspace and Environment Hints

Optional contextual hints such as:
- Workspace identifier
- Repository name
- Language hint

These are advisory only and must not be relied on for correctness.

---

### 4.7 Streaming Preference

- Optional boolean
- Defaults to `false`
- If `true`, the response will be streamed
- Clients setting this flag must support streamed handling

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

This DDR is **approved**.

No code artifacts are generated as part of this DDR.

---

*End of AGN-034*
