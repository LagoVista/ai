# TUL-002 — workspace_write_patch Tool DDR

**Title:** Workspace Line Patch Tool  
**Index:** TUL-002  
**Status:** Draft (v1)  
**Owner:** Aptix Orchestrator / Platform  
**Tool Name:** `workspace_write_patch`  
**Category:** Workspace Tools  
**Priority:** CRITICAL (per TUL-000)  

**Related Specs:**  
- TUL-000 — Aptix Tool Catalog DDR  
- AGN-003 — Agent Execute Request DDR  
- AGN-005 — Agent Tool Implementation Spec  
- AGN-007 — File Identity, Source Sets & Path Semantics DDR  
- TUL-001 — `workspace_read_file` DDR  

---

## 1. Purpose

`workspace_write_patch` defines a **line-based patch protocol** for modifying one or more files in an Aptix workspace.

This tool allows the LLM to:

1. Describe changes as a **batch of per-file patches**.  
2. For each file, specify a **SHA256** of the exact file content it used when planning the patch.  
3. Provide a **minimal list of line-level operations** (`insert`, `replace`, `delete`).  
4. Optionally annotate each change with a **short description** and **human-friendly keys**.  

The tool output is consumed by the Aptix client, which:

- Verifies SHA256 against the local file.  
- Applies each line-level change deterministically.  
- Uploads the resulting file(s) back to the server as the new Cloud truth.  

This DDR captures the **wire-level contract** between the LLM, the orchestrator, and the client.

---

## 2. Scope

### 2.1 In Scope

- Describing **one or more file changes** in a single patch batch.  
- Per-file **optimistic concurrency** via SHA256 of the full authoritative file.  
- **Line-based** patch operations only (whole lines; no substring edits).  
- Optional human-readable **keys** and **descriptions** for batches, files, and changes.  
- Support for later introspection by ID (e.g., Q&A about a specific change).

### 2.2 Out of Scope (v1)

- Applying patches on the client (this will be covered by a separate client DDR).  
- Three-way merges or conflict resolution beyond SHA + exact-line checks.  
- Binary file editing.  
- Full-file replacement mode (this tool is specifically line-based).  
- Server-side unified diff generation (may be added as a derived artifact, but not part of the LLM-facing schema).

---

## 3. File Identity & Source Selection (Summary)

This tool relies on **AGN-007** for file identity and source precedence.

- **docPath** is the **only primary key** for a file.  
  - Lowercase, globally unique, opaque identifier.  
- When the LLM wants to edit `docPath`, it MUST:
  1. Use the **Active File** version if `docPath` is in the Active File List.  
  2. Otherwise, use the **Cloud File** version via `workspace_read_file(docPath)`.  
- RAG content (snippets or full-file) is for discovery/context only and MUST NOT be used as the authoritative base for patches.

> **All line numbers and expected original lines referenced in this tool MUST be derived from the authoritative full-file content as defined in AGN-007.**

---

## 4. Request Schema — Multi-File Patch Batch

The LLM calls `workspace_write_patch` with a **batch** of one or more file patches.

### 4.1 Top-Level Request Object

```jsonc
{
  "batchLabel": "Add SystemPrompt support across orchestrator and tests",
  "batchKey": "add-systemprompt",             // optional, LLM-assigned
  "files": [
    {
      "fileKey": "orchestrator",            // optional, LLM-assigned
      "docPath": "myrepo/src/services/agentorchestrator.cs",
      "originalSha256": "...",
      "fileLabel": "Orchestrator: wire SystemPrompt through",
      "changes": [
        { /* ChangeObject #1 */ },
        { /* ChangeObject #2 */ }
      ]
    },
    {
      "fileKey": "tests-orchestrator",
      "docPath": "myrepo/tests/services/agentorchestrator.tests.cs",
      "originalSha256": "...",
      "fileLabel": "Tests: cover SystemPrompt",
      "changes": [
        { /* ChangeObject for tests */ }
      ]
    }
  ]
}
```

#### 4.1.1 Fields

- `batchLabel` (string, optional)  
  - Human-readable description for the entire batch (for UI/logging).

- `batchKey` (string, optional)  
  - Short, LLM-assigned semantic key for this batch (e.g., `add-systemprompt`).  
  - Unique only within the current context; used for narration, not storage.

- `files` (array, required)  
  - One or more per-file patch objects.  
  - **Each `docPath` MUST appear at most once** in this array.

### 4.2 Per-File Patch Object

Each entry in `files[]` describes a patch for a single file.

```jsonc
{
  "fileKey": "orchestrator",                      // optional
  "docPath": "myrepo/src/services/agentorchestrator.cs",
  "originalSha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "fileLabel": "Orchestrator: wire SystemPrompt through",
  "changes": [
    { /* ChangeObject */ }
  ]
}
```

#### 4.2.1 Fields

- `fileKey` (string, optional)  
  - LLM-assigned semantic key for this per-file patch (e.g., `orchestrator`, `tests-orchestrator`).  
  - Unique only within the batch.  
  - Used for narration and UI, not for storage.

- `docPath` (string, required)  
  - Lowercase, globally unique file identifier as defined in **AGN-007**.  
  - MUST match the docPath used in Active File List, Cloud File Set, and RAG.

- `originalSha256` (string, required)  
  - SHA256 of the **full authoritative file content** the LLM used when planning this patch.  
  - If the file is Active: computed from the **active file content**.  
  - If the file is Cloud-only: computed from the **Cloud content** (e.g., from `workspace_read_file`).  
  - MUST be a hex-encoded SHA256; LLM MUST NOT invent; it must hash the actual text or copy a server-provided SHA.

- `fileLabel` (string, optional)  
  - Human-readable description of the per-file change.

- `changes` (array, required)  
  - Ordered list of **ChangeObject** entries describing line-level operations.  
  - MUST be non-empty for files that appear in the batch.

---

## 5. ChangeObject Schema — Line-Level Operations

Each change describes a set of **line-level edits**. The only allowed operations are:

- `"insert"`
- `"replace"`
- `"delete"`

### 5.1 Common Fields

All change objects share these fields:

```jsonc
{
  "changeKey": "add-systemprompt-property",   // optional, LLM-assigned
  "changeId": "chg-0007",                    // server-assigned, response only
  "operation": "replace",                    // required
  "description": "Extend AgentExecuteRequest with SystemPrompt property.",
  // operation-specific fields...
}
```

#### 5.1.1 Fields

- `changeKey` (string, optional)  
  - LLM-assigned semantic key for this change (e.g., `add-systemprompt-property`).  
  - Unique only within its file patch.  
  - Used for narration and UI, not storage.

- `changeId` (string, response-only)  
  - Server-assigned canonical ID for this change (GUID or equivalent).  
  - Not required in requests; MUST be present in tool results.  
  - Used for later introspection tools (e.g., “tell me about change `chg-0007`”).

- `operation` (string, required)  
  - One of `"insert"`, `"replace"`, `"delete"`.

- `description` (string, optional)  
  - Short, human-readable explanation of **why** this change exists.  
  - Typically one short sentence or fragment.  
  - Clients MUST treat it as informational only (no correctness semantics).

> **Full-line rule:** All operations MUST work on complete lines only. No substring edits. No “insert into the middle of this line.”

### 5.2 Insert Operation

```jsonc
{
  "changeKey": "add-validation-using",
  "operation": "insert",
  "description": "Add using for validation utilities required by new code.",
  "afterLine": 5,
  "newLines": [
    "using LagoVista.Core.Validation;"
  ]
}
```

- `afterLine` (int, required)  
  - Line number **after which** `newLines` are inserted.  
  - `0` means insert at the top of the file before the current line 1.

- `newLines` (string[], required)  
  - One or more complete lines to insert.

### 5.3 Replace Operation

```jsonc
{
  "changeKey": "extend-agentexecuterequest",
  "operation": "replace",
  "description": "Extend AgentExecuteRequest with SystemPrompt property.",
  "startLine": 42,
  "endLine": 42,
  "expectedOriginalLines": [
    "    public string ConversationId { get; set; }"
  ],
  "newLines": [
    "    public string ConversationId { get; set; }",
    "    public string SystemPrompt { get; set; }"
  ]
}
```

- `startLine` (int, required)  
  - First line (1-based) to replace.

- `endLine` (int, required)  
  - Last line (inclusive) to replace.  
  - If replacing a single line, `startLine == endLine`.

- `expectedOriginalLines` (string[], required)  
  - Verbatim text of the lines currently at `[startLine..endLine]` in the authoritative file.  
  - Used by the client for conflict detection.  
  - Must match exactly (including whitespace) when the patch is applied.

- `newLines` (string[], required)  
  - Full replacement lines.

### 5.4 Delete Operation

```jsonc
{
  "changeKey": "remove-obsolete-logging",
  "operation": "delete",
  "description": "Remove obsolete console-based debug logging.",
  "startLine": 100,
  "endLine": 104,
  "expectedOriginalLines": [
    "    // old experimental logging",
    "    Console.WriteLine(\"Starting...\");",
    "    Console.WriteLine(\"Working...\");",
    "    Console.WriteLine(\"Done.\");",
    ""
  ]
}
```

- `startLine` (int, required)  
  - First line to delete.

- `endLine` (int, required)  
  - Last line to delete (inclusive).

- `expectedOriginalLines` (string[], required)  
  - Verbatim text currently at `[startLine..endLine]`.  
  - Used to detect conflicts.

- `newLines` MUST NOT be present for `delete` operations.

---

## 6. Ordering, Non-Overlap & Line Semantics

- All `changes[]` for a file MUST be ordered **top-to-bottom** by their line positions:
  - For `insert`: by `afterLine`.  
  - For `replace`/`delete`: by `startLine`.
- Change ranges MUST NOT overlap within a file.  
  - E.g., a delete on `10–12` cannot be followed by a replace on `11`.
- A **line** is defined as a sequence of characters ending with a newline in the authoritative file text (after any server-side normalization).  
- All line numbers and original lines MUST be computed based on this canonical view.

---

## 7. IDs vs Keys — Hybrid Identity Model

This tool supports both **server-assigned IDs** and **LLM-assigned keys**.

### 7.1 IDs (Server-Assigned, Canonical)

In the tool **response**, the server MUST provide:

- `batchId` (string)  
  - Canonical identifier for the entire batch (e.g., GUID).

- For each file patch:
  - `filePatchId` (string)  
    - Canonical identifier for this per-file patch.

- For each change:
  - `changeId` (string)  
    - Canonical identifier for this specific change.

These IDs are used for:

- Storage and retrieval.  
- Future introspection tools (e.g., `get_patch_change`).  
- Cross-session references.

### 7.2 Keys (LLM-Assigned, Semantic)

In the **request**, the LLM MAY supply:

- `batchKey`, `fileKey`, `changeKey` — short semantic labels.  
- These are echoed back in the tool response alongside canonical IDs.

Use cases:

- Richer narration in the same response (e.g., “In `changeKey: add-systemprompt-property` we…”).  
- Friendlier UI labels.  
- Human debugging, logs, and oversight agents.

> **Contract:** IDs are authoritative; keys are best-effort, semantic sugar. Clients MUST NOT rely on keys for correctness.

---

## 8. LLM Usage Rules (Bullets 1–6 Captured)

### 8.1 Bullet 1 — Latest File Source

For each file patch (`docPath`):

1. **Determine authoritative source** per AGN-007:  
   - If `docPath` is in Active File List → Active file is authoritative.  
   - Else → Cloud file via `workspace_read_file(docPath)` is authoritative.
2. **Never treat RAG content as authoritative**. RAG is for discovery only.

### 8.2 Bullet 2 — SHA256

For each file patch:

1. Obtain the full authoritative file text.  
2. Compute or copy SHA256 for that exact text.  
3. Provide this as `originalSha256` in the per-file patch.  
4. Do not hash partial snippets or modified versions.

### 8.3 Bullet 3 — Decide What Changes Are Needed

- Reason only over the authoritative full file and task instructions.  
- Do not base edits on partial RAG snippets or earlier stale versions.  
- Determine a **minimal, precise set of line-level edits** needed to satisfy the requested change.

### 8.4 Bullet 4 — Full-Line Only

- The smallest unit of change is a **whole line**.  
- Any mid-line modification must be expressed as a `replace` with full original and new lines.  
- No substring-level operations.

### 8.5 Bullet 5 — Presenting the Change List

- For each file, build a `changes[]` array containing `insert`/`replace`/`delete` operations.  
- Ensure changes are ordered, non-overlapping, and fully specified.  
- Include `description` where helpful, to explain intent.

### 8.6 Bullet 6 — Sending the Patch to the Client

- The LLM calls `workspace_write_patch` once with a batch of per-file patches.  
- The server validates and persists the batch, assigns IDs, and forwards it to the client.  
- The client verifies SHA per file, applies changes deterministically, and uploads the new full file(s) to the server, which become the new Cloud truth.

---

## 9. Error Handling & Logging (AGN-005 Alignment)

- The tool MUST return an `InvokeResult<T>`-style envelope, not throw for expected errors.  
- Validation errors (e.g., invalid line ranges, overlapping changes, bad SHA format) MUST result in a failed InvokeResult with clear messages.  
- Unexpected exceptions MUST be logged per AGN-005 and return a generic error message.

Logging examples (conceptual):

- Start event: batch received.  
- Success event: batch stored with `batchId`, number of files, number of changes.  
- Error event: reason, docPath (if applicable), validation failures.

---

## 10. Testing Expectations

Unit tests for the tool implementation SHOULD cover:

1. **Single-file happy path**  
   - Well-formed changes; IDs assigned; result structure correct.

2. **Multi-file batch**  
   - Multiple docPaths in a single batch; file-level metadata correct.

3. **Line validation**  
   - Overlapping changes rejected.  
   - Invalid ranges rejected.

4. **SHA validation**  
   - Reject obviously invalid SHA formats.  
   - (Client-side SHA mismatch behavior covered in client DDR/tests.)

5. **ID assignment**  
   - `batchId`, `filePatchId`, `changeId` always present in responses.  
   - Request may omit them.

6. **Descriptive metadata**  
   - `batchKey`, `fileKey`, `changeKey`, and `description` are round-tripped when present.

---

## 11. Future Extensions

Potential extensions to this DDR include:

- Derived unified-diff artifacts for display-only purposes.  
- An introspection tool (e.g., `get_patch_change`) that fetches a stored change by `batchId`/`changeId` for Q&A and iterative refinement.  
- Full-file replacement mode as a sibling tool or an extended operation type.  
- Richer metadata: severity, categories, tags.

For now, this v1 DDR defines the **normative contract** for how the LLM describes line-based patches and how the patch tool represents them for the client.
