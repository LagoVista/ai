# AGN-033 — AgentExecuteResponse Shape

**ID:** AGN-033  
**Title:** AgentExecuteResponse Shape  
**Status:** Approved  
**Ddr Type:** Generation

## Approval Metadata
- **Approved By:** Kevin D. Wolf  
- **Approval Timestamp:** 2025-12-24 09:11:00 ET (UTC-05:00)

---

## 1. Purpose and Scope

### 1.1 Purpose
AGN-033 defines the **external response contract** returned by Aptix agent execution APIs.

This contract represents **everything a client is allowed to see as the result of an agent turn**, independent of:
- LLM provider/model
- Internal pipeline structure
- Tooling/orchestration strategy

If a client can observe it, it must be represented here.

### 1.2 Scope
This DDR defines:
- The **top-level response envelope**
- Output semantics for **Final** vs **Client Tool Continuation**
- Tool-call request/response contract
- File reference contract (no embedded binaries)

### 1.3 Non-Goals
AGN-033 does not define:
- Prompting, reasoning strategy, pipeline implementation
- Vendor/raw response payload shapes
- Tool implementation behavior
- UI rendering directives
- Diagnostics/progress streams (served out-of-band)

Raw vendor payloads are explicitly **not** part of this public contract.

---

## 2. Payload Responsibilities

### 2.1 Relationship to InvokeResult
All agent execution APIs return results wrapped in `InvokeResult<T>`.

- Errors and system-level warnings are reported **only** via `InvokeResult`.
- If invocation is not successful, `AgentExecuteResponse` is **always null/empty**.
- `AgentExecuteResponse` must never signal transport/system failure.

### 2.2 Execution Outcome Types
Each successful response is exactly one of:
1) **Final Response**: completed, user-rendered.
2) **Client Tool Continuation**: client must execute tool calls.

### 2.3 User Visibility Rules
- **Final**: MUST include exactly one `PrimaryOutputText` rendered to the user.
- **Client Tool Continuation**: MUST NOT include `PrimaryOutputText`; MAY include informational `ToolContinuationMessage`.

---

## 3. Top-Level Response Envelope

### 3.1 Session Metadata (LOCKED)
Every successful `AgentExecuteResponse` MUST include:
- `SessionId`
- `TurnId`
- `ModeDisplayName` (display-only)

**Follow-on request rule (hard-stop):** Any follow-on request requires exactly `SessionId + TurnId`.

**TurnId scoping:** TurnId values may be globally unique, but are only valid/accessible within their SessionId “room”.

**ModeDisplayName rules (flag planted):**
- Returned always.
- Clients MUST NOT branch behavior based on ModeDisplayName.

**Explicit exclusions (server-only):**
- Model/provider continuation identifiers (e.g., `previous_response_id`)
- Internal pipeline state
- Diagnostics/progress/traces (served out-of-band)

### 3.2 Response Kinds
`Kind` is a required discriminator. AGN-033 supports exactly two kinds:
- `final`
- `client_tool_continuation`

**Contract Rule — Closed Enum:**
- The allowed set of `Kind` values is **closed** for AGN-033.
- Introducing a new kind requires a new DDR and a new contract/version.

### 3.3 Tool Continuation Validation Rules (LOCKED)
For client tool continuation:
- Server emits an **exact, finite, ordered** set of ToolCalls.
- Client MUST return ToolResults matching the ToolCall set **exactly**, including count, ids, and order.
- Any mismatch is a **hard-fault programming bug** and must abort processing.
- Tool failures are allowed but must still be returned as ToolResults for the same call id.

### 3.4 Bucket Presence Rules (LOCKED)

#### Final Response (`Kind = final`)
**REQUIRED**
- Session metadata
- `PrimaryOutputText` (exactly one)

**ALLOWED**
- `Files[]`
- `ToolResults[]` (server-executed visibility only)
- `UserWarnings[]`
- `Usage`

**FORBIDDEN**
- `ToolCalls[]`
- `ToolContinuationMessage`

#### Client Tool Continuation (`Kind = client_tool_continuation`)
**REQUIRED**
- Session metadata
- `ToolCalls[]` (one or more)

**ALLOWED**
- `ToolContinuationMessage` (informational only)

**FORBIDDEN**
- `PrimaryOutputText`
- `ToolResults[]`
- `Files[]`
- `UserWarnings[]`
- `Usage`

### 3.5 Envelope Invariants
- The response conforms to **exactly one** response kind.
- Forbidden buckets must not appear (even as empty values).
- Absence of a bucket is meaningful.

---

## 4. Output Model

### 4.1 Primary Output (LOCKED)
- `PrimaryOutputText` is always **text** (UTF-8 string).
- Canonical format is **Markdown**; readable as plain text.
- HTML must not be relied upon for correct rendering.

**Guarded Payload Rule:**
- Machine-consumable payloads (e.g., Aptix file bundles) MUST be inside a guarded fenced code block with a language tag (e.g., `aptix-json`, `json`).

### 4.2 ToolContinuationMessage (LOCKED)
- Optional informational message for `client_tool_continuation`.
- Never actionable; client may suppress.

### 4.3 UserWarnings (LOCKED)
- Final-only; non-fatal, user-relevant informational warnings.

---

## 5. Tool Interaction Contract (LOCKED)

### 5.1 ToolCall (Server → Client)
Present only when `Kind = client_tool_continuation`.

Required fields:
- `ToolCallId`
- `Name`
- `ArgumentsJson`

ToolCalls are emitted as an exact, ordered set.

### 5.2 ToolResult (Client → Server)
Required fields:
- `ToolCallId`
- `ExecutionMs`

Outcome fields:
- Exactly one of `ResultJson` OR `ErrorMessage` must be present.
- Tool failures are valid results.

### 5.3 ToolResults in AgentExecuteResponse
- Tool continuation responses MUST NOT include ToolResults.
- Final responses MAY include ToolResults for server-executed visibility.

---

## 6. Files & FileRef (LOCKED)

### 6.1 File Handling Rules
- Agent responses MUST NOT embed file contents (no base64, no inline code blobs), except Aptix bundles inside guarded code blocks.
- All other files are referenced via `FileRef`.

### 6.2 Files Bucket
- `Files[]` may appear only when `Kind = final`.
- `Files[]` must never appear when `Kind = client_tool_continuation`.

### 6.3 FileRef Contract
Required fields:
- `Name`
- `MimeType`
- `Url`
- `SizeBytes`
- `ContentHash` (computed via `IContentHashService`, IDX-016)

Optional fields:
- `Description`
- `ContentExpires` (ISO-8601). If omitted, clients may assume no expiration.

### 6.4 Tool File Output Guidance (Non-Normative)
Tools are encouraged (but not required) to surface produced files as `FileRef`.

---

## 7. Generated Artifacts
This DDR produces:
- This Markdown DDR
- C# contract types implementing the response shape
- JSONL index content for RAG
