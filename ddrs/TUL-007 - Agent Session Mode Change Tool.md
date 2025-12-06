# TUL-007 — Agent Session Mode Change Tool

**ID:** TUL-007  
**Title:** Agent Session Mode Change Tool  
**Status:** Approved  
**Owner:** Kevin Wolf & Aptix  

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-06 13:55:00 EST (UTC-05:00)

---

## 1. Purpose

TUL-007 defines a single server-side tool that changes the **mode** of an existing `AgentSession`. This tool is the only mechanism by which the LLM can request a persisted mode change for the current session. It does not define how modes are discovered, how mode catalogs work, or how prompts are constructed around modes; those are covered by other DDRs (for example, TUL-010 and AGN-013).

The scope of this DDR is strictly bounded by two edges:

1. The LLM has already decided that a mode change is appropriate for the user's request.  
2. The tool is successfully called (or not) to commit that change to the session.

Everything about *how* the LLM arrives at its decision, and what happens after branching, is out of scope.

---

## 2. Scope and Non-Goals

This DDR **does** define:

- The contract for a single tool (ModeChangeTool) that:
  - Accepts a target mode string, a branch flag, and a reason.  
  - Updates the current session's mode via `IAgentSessionManager`.  
  - Returns a small payload (`success`, `mode`, `branch`, `reason`).
- How the LLM is expected to call this tool after explicit user confirmation.
- The minimal unit tests that validate its behavior.

This DDR **does not** define:

- The catalog of valid modes or their semantics.  
- How the LLM detects that a different mode is needed.  
- How branching into a new `AgentSession` is implemented.  
- How system prompts or RAG scopes are constructed for each mode.

Those behaviors will be defined in separate AGN/TUL DDRs.

---

## 3. Tool Contract

### 3.1 Identity and Availability

The tool is implemented as `ModeChangeTool` and must:

- Implement `IAgentTool`.
- Be registered with the name:

  ```csharp
  public const string ToolName = "agent_change_mode";
  ```

- Always be included in the tools list sent to the LLM, **regardless of the current mode**. Mode filtering MUST NOT hide or remove this tool. This prevents the agent from becoming stuck in an inappropriate mode.

The tool is fully server-side:

```csharp
public bool IsToolFullyExecutedOnServer => true;
```

### 3.2 Dependency Injection

`ModeChangeTool` is constructed via DI and must accept:

- `IAgentSessionManager` — used to persist the new mode on the session.  
- `IAdminLogger` — used for error and exception logging.

```csharp
public ModeChangeTool(IAgentSessionManager sessionManager, IAdminLogger logger)
```

Both parameters are required and must be non-null.

### 3.3 Arguments JSON

The tool accepts a single JSON object with three required fields:

```json
{
  "mode": "<target-mode-string>",
  "branch": false,
  "reason": "Short explanation of why this mode fits the current request."
}
```

- `mode` (string, required)
  - Non-empty target mode name/key for the **current** session.
  - This DDR does not validate that the mode is part of a catalog; it only requires a non-empty string.

- `branch` (boolean, required)
  - `false` → "Change the current session's mode and keep working in this same session."  
  - `true` → "Change the current session's mode, and the caller intends to start new work as a *new* session in this mode."  
  - TUL-007 does **not** define how branching is implemented; it only surfaces this intent flag.

- `reason` (string, required)
  - Non-empty, short, natural-language explanation of *why* the mode change is appropriate for the user's request.  
  - Used for logging, audit, and potential future prompt construction.

The tool **must not** accept or require `sessionId`, `org`, or `user` in the arguments. These are always taken from `AgentToolExecutionContext`.

### 3.4 Validation Rules

When `ExecuteAsync` is called:

- If `argumentsJson` is null or whitespace → return `InvokeResult<string>.FromError("ModeChangeTool requires a non-empty arguments object.")`.
- If `context` is null → return `InvokeResult<string>.FromError("ModeChangeTool requires a valid execution context.")`.
- If `context.SessionId` is null/empty:
  - Log an error via `IAdminLogger.AddError` with a clear message.  
  - Return `InvokeResult<string>.FromError("ModeChangeTool cannot change mode because the session id is missing.")`.
- After deserializing `argumentsJson` into an internal `ModeChangeArgs` class:
  - If `mode` is null/empty → return a failed result with:  
    `"ModeChangeTool requires a non-empty 'mode' string."`
  - If `branch` is null (not supplied) → return a failed result with:  
    `"ModeChangeTool requires a 'branch' boolean flag."`
  - If `reason` is null/empty → return a failed result with:  
    `"ModeChangeTool requires a non-empty 'reason' string explaining why the mode change is needed."`

If any validation step fails, the tool MUST NOT call `IAgentSessionManager.SetSessionMode`.

### 3.5 Backend Call

On valid input, the tool must call:

```csharp
await _sessionManager.SetSessionMode(
    context.SessionId,
    args.Mode,
    args.Reason,
    context.Org,
    context.User);
```

This is the **only** side effect defined by this DDR. The method is responsible for persisting the new mode against the session.

### 3.6 Result JSON

On success, the tool must return a JSON payload of the form:

```json
{
  "success": true,
  "mode": "<mode-that-was-set>",
  "branch": true,
  "reason": "Short explanation of why the mode was changed."
}
```

- `success` (boolean) — always `true` on successful mode update.  
- `mode` (string) — the mode value that was written to the session.  
- `branch` (boolean) — echoes the input `branch` flag.  
- `reason` (string) — echoes the input `reason` value.

This payload is serialized as a JSON string and wrapped in `InvokeResult<string>.Create(json)`.

If an exception occurs while calling `SetSessionMode`:

- The tool must log the exception via `IAdminLogger.AddException` with a stable tag.  
- The tool must return `InvokeResult<string>.FromError("ModeChangeTool failed to change the session mode.")`.  
- No structured success payload is required on failure.

---

## 4. LLM Usage Guidance

### 4.1 When to Consider a Mode Change

This DDR assumes the LLM has already decided that the user's request likely belongs in a different mode (for example, from GeneralChat to DDRAuthoring). TUL-007 does **not** define how that decision is made.

Once the LLM suspects a new mode would be more appropriate, it must:

1. **Not** call the tool yet.  
2. Propose the candidate mode to the user.  
3. Ask which of three options the user prefers.

A recommended pattern is:

> "It seems like what you are asking for would be better served by **MODE_XYZ** mode.  
> Would you like to:  
> 1) Stay in the current mode,  
> 2) Switch this session to **MODE_XYZ**, or  
> 3) Switch to **MODE_XYZ** and start a new session for this work?"

### 4.2 Mapping User Choice to Tool Calls

- If the user chooses **stay in the current mode** (option 1 or equivalent):
  - The LLM must **not** call the tool.  
  - The session mode remains unchanged.

- If the user chooses **switch this session** (option 2):
  - The LLM may call the tool with:

    ```json
    {
      "mode": "MODE_XYZ",
      "branch": false,
      "reason": "Short explanation of why MODE_XYZ fits the request."
    }
    ```

- If the user chooses **switch and start a new session** (option 3):
  - The LLM may call the tool with:

    ```json
    {
      "mode": "MODE_XYZ",
      "branch": true,
      "reason": "Short explanation of why MODE_XYZ fits the request."
    }
    ```

TUL-007 does **not** define what happens after `branch = true`. The meaning of branching and the creation of new sessions is handled elsewhere.

### 4.3 Hard Guardrails

The LLM must observe the following rules:

- It must **never** call this tool without explicit user confirmation.  
- It must always provide:
  - A non-empty `mode` string.  
  - A `branch` boolean.  
  - A non-empty `reason` string.  
- It must **not** include `sessionId`, `org`, or `user` in the tool arguments; those come from the execution context.
- This tool is always present in the tools list, but should be called **sparingly** and only when a mode change is clearly indicated and confirmed.

### 4.4 Usage Metadata and Schema Description

The tool must define `ToolUsageMetadata` and a schema `description` that encode this behavior. A reference implementation is provided in Section 5.

---

## 5. Reference Implementation (Informative)

### 5.1 ModeChangeTool Class

A compliant implementation uses the following structure (summarized, not a strict copy requirement):

- Implements `IAgentTool` with:
  - `Name => "agent_change_mode"`.  
  - `IsToolFullyExecutedOnServer => true`.
- Constructor:
  - `ModeChangeTool(IAgentSessionManager sessionManager, IAdminLogger logger)`.
- Private argument and result classes:
  - `ModeChangeArgs` with `Mode`, `Branch`, `Reason`.  
  - `ModeChangeResult` with `Success`, `Mode`, `Branch`, `Reason`.
- `ExecuteAsync`:
  - Validates arguments and context.  
  - Calls `_sessionManager.SetSessionMode(context.SessionId, args.Mode, args.Reason, context.Org, context.User)`.  
  - Returns an `InvokeResult<string>` containing the serialized `ModeChangeResult` on success.  
  - Logs and returns a failed `InvokeResult<string>` on errors.
- `GetSchema()`:
  - Returns a function schema with `mode` (string), `branch` (boolean), and `reason` (string) as required parameters.

### 5.2 ToolUsageMetadata

A recommended `ToolUsageMetadata` value is:

```csharp
public const string ToolUsageMetadata =
    "Use this tool to change the current agent session mode, but only after the user " +
    "has explicitly agreed to switch. First, propose a specific target mode and ask " +
    "whether the user wants to (1) stay in the current mode, (2) switch this session " +
    "to that mode, or (3) switch and start a new session. " +
    "Call this tool only for options (2) or (3): use branch=false for switching the " +
    "current session, and branch=true when the user wants to switch and start a new session.";
```

### 5.3 Schema Description

A recommended schema `description` is:

```text
"Changes the mode for the current agent session to the specified mode string. Call only after the user confirms a mode change, and provide a short 'reason' describing why this mode fits the current request. Set branch=true when the user wants the new work to start as a separate session."
```

---

## 6. Testing Requirements

At minimum, the following unit tests are required for `ModeChangeTool`:

1. **Happy Path**
   - Given valid arguments (`mode`, `branch`, `reason`) and a context with a non-empty `SessionId`, the tool:
     - Calls `IAgentSessionManager.SetSessionMode` exactly once with the expected arguments.  
     - Returns `InvokeResult<string>` with `Successful == true`.  
     - The result JSON deserializes to an object where `success == true`, `mode` matches the input, `branch` matches the input, and `reason` matches the input.

2. **Missing Mode**
   - Given arguments where `mode` is missing or empty, the tool:
     - Returns a failed `InvokeResult<string>` with an error message mentioning the missing `mode`.  
     - Does **not** call `IAgentSessionManager.SetSessionMode`.

3. **Missing Reason**
   - Given arguments where `reason` is missing or empty, the tool:
     - Returns a failed `InvokeResult<string>` with an error message mentioning the missing `reason`.  
     - Does **not** call `IAgentSessionManager.SetSessionMode`.

4. **Exception from Session Manager**
   - If `IAgentSessionManager.SetSessionMode` throws, the tool:
     - Logs the exception via `IAdminLogger.AddException`.  
     - Returns a failed `InvokeResult<string>` whose error message indicates that the mode change failed.

These tests ensure that the contract defined in this DDR is enforced and that failures are safe and observable before deployment.

---

TUL-007 is now approved and serves as the authoritative specification for the agent session mode change tool.
