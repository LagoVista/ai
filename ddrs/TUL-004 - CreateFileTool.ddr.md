# DDR TUL-004 — Create File Tool

- **DDR ID:** TUL-004
- **Title:** Create File Tool
- **Layer:** Tooling (TUL)
- **Type:** Tool Specification
- **Status:** Approved
- **Version:** 1.0.0
- **Owner:** Aptix Tooling
- **Created At:** 2025-11-29T09:45:00-05:00
- **Approved At:** 2025-11-29T09:45:00-05:00
- **Approved By:** Kevin Wolf

---

## 1. Overview

**TUL-004** defines the **Create File Tool** (`workspace_create_file`), a **client-executed** tool that allows the LLM to request creation (or explicit full overwrite) of files inside a user workspace.

Key properties:

- The **server never touches the filesystem** for this tool.
- The tool is **advertised in the server tool catalog**, but **executed exclusively on the client**.
- The tool is designed for **scaffolding new files** and **full replacements**, not for incremental edits.

---

## 2. Purpose and Scope

### 2.1 In Scope

- Creating **new text files** at workspace-relative paths.
- Optionally **overwriting existing files** when `overwrite` is explicitly requested.
- Returning metadata for successfully created or overwritten files, including:
  - `path`
  - `sizeBytes`
  - `hash`
  - `created`
  - `overwritten`
- Providing a **standard JSON contract** so any Aptix client (VS Code extension, CLI, etc.) can implement the tool consistently.

### 2.2 Out of Scope (v1)

- Deleting files.
- Renaming or moving files.
- Partial or in-place edits to existing files (handled by patch/edit tools).
- Binary-specific behavior (content is treated as text bytes; binary handling is not optimized here).
- Any direct filesystem I/O on the **server**.

---

## 3. Execution Model

### 3.1 Summary

- **Execution Location:** `client` (always).
- The server:
  - Advertises `workspace_create_file` in the tool catalog.
  - Routes tool calls from the LLM to the client.
  - May record tool requests and results for auditing and session history.
- The client:
  - Performs all **actual filesystem operations**.
  - Enforces path safety, workspace boundaries, and size limits.
  - Computes the canonical content hash and returns the result payload.

### 3.2 Server Responsibilities

The server **must**:

- Register `workspace_create_file` in the **tool catalog** with:
  - Name, description, request/response schema.
  - Execution location flagged as **client**.
- Include tool calls from the LLM in the **agent turn result envelope**.
- Log that a create-file tool call was generated and **forwarded** to the client.
- Optionally store tool requests and results as part of the agent session/turn history.
- **Never** perform filesystem I/O for this tool.
- **Not** implement a server-side `IAgentTool` for TUL-004.

### 3.3 Client Responsibilities

The client implementation **must**:

- Read `workspace_create_file` calls from the server's response envelope.
- Validate inputs and enforce **workspace-root path safety**.
- Ensure parent directories exist, creating them as needed.
- Write content as **UTF-8 without BOM**, normalizing line endings to `\n`.
- Compute the canonical Aptix content hash for the final bytes.
- Populate the TUL-004 response payload according to the contract.
- Log execution details using the client's logging/telemetry facilities.

---

## 4. Tool Identity and Contract

### 4.1 Tool Identity

- **Tool Name (catalog):** `workspace_create_file`
- **Category:** `workspace` (filesystem-related)
- **Execution Location:** `client`

### 4.2 Request Payload (Arguments)

**Shape (JSON object):**

```json
{
  "path": "src/Services/AgentOrchestrator.cs",
  "content": "using System;\n\npublic class AgentOrchestrator { }\n",
  "overwrite": false
}
```

**Fields:**

- `path` (**string**, required)
  - Workspace-relative file path.
  - Examples:
    - `"src/Services/AgentOrchestrator.cs"`
    - `"tests/LagoVista.AI.Tests/AgentOrchestratorTests.cs"`
    - `"README.md"`
  - Must not be absolute and must not escape the workspace root.

- `content` (**string**, required)
  - Full text contents of the file.
  - Client must treat this as text encoded **UTF-8 without BOM**, with **`\n` line endings**.
  - Caller (LLM) must provide the **entire final content** of the file.

- `overwrite` (**bool**, optional, default = `false`)
  - `false`:
    - If a file already exists at `path`, the tool **must fail** with `FileExists`.
  - `true`:
    - If a file exists, it is fully replaced with `content`.
    - Response must set `created = false`, `overwritten = true`.
    - If no file exists, behaves as a normal create (`created = true`, `overwritten = false`).

### 4.3 Response Payload (Result)

**Shape (JSON object):**

```json
{
  "success": true,
  "message": "File created.",
  "path": "src/Services/AgentOrchestrator.cs",
  "sizeBytes": 128,
  "hash": "<computed-hash>",
  "created": true,
  "overwritten": false,
  "errorCode": null
}
```

**Fields:**

- `success` (**bool**)
  - `true` if the operation completed as requested (create or overwrite).
  - `false` if any validation, I/O, or other error occurred.

- `message` (**string**)
  - Short human-readable description of the outcome.

- `path` (**string**)
  - Normalized path used by the client, relative to workspace root.
  - Must use forward slashes (`/`).

- `sizeBytes` (**integer**, 64-bit)
  - Number of bytes written to disk.

- `hash` (**string**)
  - Normalized content hash for the final file bytes.
  - Must use the canonical Aptix content hash algorithm (semantically equivalent to `IContentHashService`).

- `created` (**bool**)
  - `true` if the file did **not** exist before this call.
  - `false` if the file existed and a full overwrite occurred.

- `overwritten` (**bool**)
  - `true` if an existing file was replaced and the operation succeeded.
  - `false` otherwise.

- `errorCode` (**string or null**)
  - `null` when `success = true`.
  - One of the canonical error codes when `success = false` (see Section 5.3).

---

## 5. Behavior and Safety

This section describes how the **client** must behave when executing TUL-004.

### 5.1 Core Behavior Rules

1. **Validate input**
   - `path` must be non-empty and non-whitespace.
   - `content` may be empty but must be non-null.
   - `overwrite` defaults to `false` if omitted.

2. **Normalize and validate path**
   - Normalize path separators to `/`.
   - Resolve against the configured **workspace root**.
   - Reject:
     - Absolute paths (e.g., `C:\...`, `/usr/...`).
     - Paths that escape the root (e.g., via `..`).
   - The normalized `path` in the response reflects the final relative path.

3. **Check for existing file**
   - If a file exists and `overwrite == false`:
     - Do not write.
     - Return `success = false`, `errorCode = "FileExists"`.

4. **Ensure parent directories exist**
   - If parent directory does not exist, client attempts to create it.
   - On failure, return `success = false`, `errorCode = "DirectoryCreateFailed"`.

5. **Write file**
   - Write bytes as **UTF-8 without BOM**.
   - Normalize line endings in `content` to `\n` prior to hashing (and ideally prior to writing).
   - Operation should be **atomic**:
     - Implementation detail (e.g., temp file + rename) is client-specific.
     - Spec requires that no partially written files are left on failure.

6. **Compute hash**
   - Compute the hash on the final bytes as written.
   - Use the canonical Aptix content hash algorithm.

7. **Populate result**
   - On success:
     - `success = true`.
     - `created` / `overwritten` set based on prior existence and `overwrite` flag.
     - `sizeBytes` set to final byte length.
     - `hash` set to computed hash.
   - On failure:
     - `success = false`.
     - `errorCode` set to a canonical error code.
     - `message` gives a short explanation.

### 5.2 Path Safety

The client must enforce:

- No absolute paths.
- No drive roots or UNC roots.
- No resolution outside the workspace root.

Any violation must produce:

- `success = false`
- `errorCode = "InvalidPath"`

### 5.3 Size Limits and Error Codes

The client may enforce a maximum allowed file size. If the `content` exceeds the limit:

- Do not write.
- Return `success = false`, `errorCode = "TooLarge"`.

**Canonical error codes (when `success = false`):**

- `"FileExists"` — File exists and `overwrite == false`.
- `"InvalidPath"` — Path is invalid, empty, absolute, or escapes the workspace root.
- `"DirectoryCreateFailed"` — Parent directory creation failed.
- `"WriteFailed"` — File write failed (permissions, disk error, etc.).
- `"TooLarge"` — Content exceeds configured maximum size.
- `"HashFailed"` — Content written but hash computation failed.
- `"UnhandledException"` — Unexpected error.

In all error cases, `message` should be clear but may omit sensitive low-level details.

### 5.4 Atomicity and Partial Writes

- The client should implement an atomic write pattern (e.g., write to a temporary file, then rename).
- The spec requires that **partial writes must not be visible** if an error is returned.

---

## 6. Logging and Orchestrator Flow

### 6.1 Server-Side Logging and Flow

When the LLM calls `workspace_create_file`:

1. The tool call is included in the **agent turn result envelope**.
2. The server logs a forwarding event, e.g.:
   - Event name: `"Tool.CreateFile.Forwarded"`.
   - Fields: `SessionId`, `ConversationId`, `TurnId`, `ToolName`, `Path`, `Overwrite`.
3. The server does not perform filesystem I/O for this tool.
4. If the client returns a result payload, the server may:
   - Attach it to the same turn as `ToolResults`.
   - Log a result event, e.g. `"Tool.CreateFile.ResultReceived"` with `Success`, `ErrorCode`, `SizeBytes`, `Hash`.

### 6.2 Client-Side Logging and Flow

When the client executes `workspace_create_file`:

1. Client logs execution, e.g.:
   - Event name: `"Tool.CreateFile.Executed"`.
   - Fields: `SessionId`, `ConversationId`, `TurnId`, `Path`, `Overwrite`, `Success`, `ErrorCode`, `SizeBytes`, `Hash`.
2. Client returns a response payload matching the TUL-004 contract.
3. Optionally, the client sends this result back to the server for inclusion in the session history.

---

## 7. LLM Usage Guidance

This section describes how the **LLM** should use TUL-004. These instructions are intended to be included in the agent's boot/system prompt and tool catalog metadata.

### 7.1 Catalog Description (Short Form)

> **workspace_create_file**  
> Create a new file in the user’s workspace or fully replace an existing file when explicitly allowed.  
>  
> **Inputs**  
> - `path` (string, required): Relative file path inside the workspace.  
> - `content` (string, required): Full text content of the file (UTF-8, no BOM, `\n` line endings recommended).  
> - `overwrite` (bool, optional, default=false): Set to true if the file already exists and you intend to replace it.  
>  
> **Behavior**  
> - Fails if the file exists and `overwrite` is false.  
> - Creates parent directories if they do not exist.  
> - Always writes the complete file content in a single operation.  
>  
> **Output**  
> Returns whether the operation succeeded, the normalized path, file size in bytes, an integrity hash, and flags indicating whether the file was created or overwritten.

### 7.2 When to Use `workspace_create_file`

The LLM **should** call this tool when:

- Creating new source files (implementations, services, models, etc.).
- Creating new test files associated with new or existing components/classes.
- Creating config, schema, or documentation files (e.g., `.json`, `.yml`, `.md`).
- Fully replacing an existing file when the new content is known and the replacement should be atomic.

### 7.3 When *Not* to Use It

The LLM **must not** use this tool when:

- Modifying an existing file **in place** (use patch/edit tools instead).
- Appending content to an existing file.
- Attempting binary writes.
- Writing files outside the workspace root (these calls will fail).

### 7.4 Required LLM Behaviors

When using TUL-004, the LLM must:

1. **Provide the full intended file content**
   - The tool writes the entire file in one operation.
   - No incremental or append-style writes are supported.

2. **Specify a correct relative path**
   - Include the appropriate directory structure, e.g. `"src/Services/Foo.cs"`.

3. **Use `overwrite = true` only when intentional**
   - Do not assume silent overwrites.
   - If unsure about whether a file exists or should be replaced, ask the user for guidance.

4. **Avoid paths that escape the workspace**
   - No absolute paths, no `..` to move above the workspace root.

5. **Never attempt edits via this tool**
   - Use patch tools for edits.
   - This tool is for creation and full replacement only.

### 7.5 Workspace Awareness for Path Selection

The LLM is expected to receive **workspace structure context** via the boot/system prompt, including:

- A snapshot of the current workspace directory tree (truncated to a reasonable depth and excluding build artifacts such as `bin`, `obj`, `node_modules`, `.git`, `dist`).
- References to specific files/paths used during the conversation.

**Path selection rules:**

- Prefer **actual directories** from the workspace snapshot and known file paths over invented structure.
- Use existing patterns where present, e.g.:
  - .NET sources under `src/ProjectName/...`.
  - .NET tests under `tests/ProjectName.Tests/...`.
  - Angular app views under `apps/design-playground/src/app/views/...`.
  - Angular libraries under `libs/primitives/src/lib/...`.
- Place related files near each other:
  - Implementation and tests in corresponding `src`/`tests` projects.
  - New services alongside existing services in the same project.
- Only introduce new directories when there is no suitable existing directory that matches the conventions.

### 7.6 Ask vs. Act Rule (Ambiguity Handling)

- If the LLM can **clearly determine an appropriate path** using:
  - The workspace snapshot,
  - Known related files,
  - And/or explicit user instructions,
  
  then it should **issue the `workspace_create_file` tool call directly** using that path.

- If there are **multiple plausible locations** and the correct one is not clear (e.g., similar projects, ambiguous module ownership):
  - The LLM should **ask the user a brief, targeted question** to clarify where the file should live **before** calling the tool.

> Note: The client side will still gate tool execution with a human in the loop, but the LLM is expected to minimize ambiguity by asking when path placement is unclear and acting directly when it is clearly determined.

---

## 8. Cross-References

- **AGN-003 (Boot/System Prompt DDR)** should include:
  - The `workspace_create_file` catalog description and usage guidance from this DDR.
  - Rules for providing the workspace directory snapshot as part of the boot/system context.
- **Other Tooling DDRs (e.g., patch/edit tools)** should reference TUL-004 to clarify that TUL-004 is for creation/overwrite and not fine-grained edits.

---

## 9. Approval

- **DDR Status:** Approved
- **Approved By:** Kevin Wolf
- **Approved On:** 2025-11-29T09:45:00-05:00
