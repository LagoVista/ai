# AGN-007 — File Identity, Source Sets & Path Semantics DDR

**Title:** File Identity, Source Sets & Path Semantics  
**Index:** AGN-007  
**Status:** Approved (v1)  
**Owner:** Aptix Orchestrator / Platform  
**Related Specs:**  
- TUL-000 — Aptix Tool Catalog DDR  
- AGN-003 — Agent Execute Request DDR  
- AGN-005 — Agent Tool Implementation Spec  
- TUL-001 — `workspace_read_file` DDR  
- TUL-002 — `workspace_write_patch` DDR (line-level patch tool)

---

## 1. Purpose

This DDR defines what a **file** is in the Aptix ecosystem, where file contents can come from, and how they are **uniquely identified** and **selected** by agents and tools.

It provides:

- A common model for **file source sets**:
  - **Active File List** (client-local, user-editing)
  - **Cloud File Set** (server/workspace-backed)
  - **RAG Content** (snippets / full files for context & discovery)
- A strict contract for **paths as unique identifiers** across all of these.
- Ground rules for how the **LLM must choose which copy is authoritative** when reading or editing.

All tools and agents that operate on files (e.g., `workspace_read_file`, `workspace_write_patch`, future `active_file_sha256`) MUST obey the rules in this DDR.

---

## 2. Core Concepts

### 2.1 docPath (File Identity)

The **docPath** is the **only primary key** for a file across the system. It is used consistently in:

- Active File List entries
- Cloud File Set lookup (workspace storage)
- RAG results (snippets and full-file content)
- All file tools (read, patch, SHA, etc.)

**Rules:**

1. **Uniqueness**  
   - docPath is **globally unique** within the Aptix workspace.
   - There is at most one *Active* file entry and one *Cloud* file for a given docPath.

2. **Case normalization**  
   - docPath MUST be **lowercase** by the time it reaches the LLM and tools.  
   - Any case normalization (e.g., on Windows paths) is handled before the value is provided to the LLM.

3. **Opaque semantics**  
   - The LLM and tools MUST treat docPath as an **opaque identifier** used only for equality, lookup, and logging.  
   - The apparent structure (`repositoryName/path/to/file.cs`) is **not a source of truth** or intelligence.  
   - A path like `ajhaafaeradaky234wf` MUST be treated as equally valid as `ai.machinelearning/src/lagovista.ai/parser.cs`.
   - The LLM MUST NOT infer behavior, ownership, or code semantics from docPath.

4. **Typical format (informative, not normative)**  
   - In practice, docPath will **generally** consist of:
     - Repository name
     - Full path on the local machine (normalized)
     - File name
   - This is described here to help humans, but MUST NOT affect tool logic.

> **Contract:** For all tools, *docPath is just a stable, lowercase key.*

---

## 3. File Source Sets

Aptix recognizes three primary **source sets** for file contents:

1. **Active File List** — files the user is actively working on in the client.
2. **Cloud File Set** — files stored in the Aptix cloud workspace.
3. **RAG Content** — chunks returned by the RAG/indexing system (snippets or full files).

### 3.1 Active File List

The Active File List is a collection of files that the **client has pushed** to Aptix to represent the user’s current working set.

Characteristics:

- These files may have **un-synced local edits** that are newer than the cloud workspace.
- For any given docPath, if it appears in the Active File List, that local version is always considered **newer and more authoritative** than the cloud copy.
- The Active File List is communicated to the LLM via:
  - System prompt context, and/or
  - A dedicated tool/API in the future.

A typical **Active File entry** (conceptual model):

```jsonc
{
  "docPath": "myrepo/src/services/agentorchestrator.cs",  // lowercase, unique
  "displayPath": "c:/dev/myrepo/src/services/AgentOrchestrator.cs", // client/editor path (informational)
  "sizeBytes": 12345,
  "isDirty": true,
  "isActive": true
}
```

> **Ground rule:** If a file’s docPath appears in the Active File List, the **local copy is the latest** and is the one that patches should target.

### 3.2 Cloud File Set

The Cloud File Set represents files stored in the Aptix cloud workspace backed by the server.

- Accessed via tools like `workspace_read_file` and (indirectly) `workspace_write_patch`.
- Each file is identified by the same docPath.

If a docPath **does not** appear in the Active File List, the **Cloud File Set** is treated as the authoritative version for reading and editing.

> **Ground rule:** Active File List has precedence. If docPath is not active, fall back to cloud.

### 3.3 RAG Content (Snippets vs Full Files)

RAG (Retrieval Augmented Generation) surfaces **content chunks** to help the LLM reason about the codebase.

Each RAG result MUST include metadata that lets the LLM distinguish between:

1. **Partial snippet** (`rag-snippet`)
2. **Full file content** (`rag-full`)

Example conceptual shape:

```jsonc
{
  "docPath": "myrepo/src/services/agentorchestrator.cs",
  "source": "rag-snippet",           // or "rag-full"
  "isFullFile": false,                // true if full file
  "startLine": 120,
  "endLine": 185,
  "content": "public class AgentOrchestrator { ... }"
}
```

**Rules:**

- `rag-snippet` / `isFullFile: false`
  - Indicates this is **not** the complete file.
  - The LLM MUST NOT generate line-level patches based solely on this snippet.
  - The LLM MUST treat this as **discovery context** and use docPath to fetch the full file via Active/Cloud.

- `rag-full` / `isFullFile: true`
  - Indicates that RAG has returned the **entire file** content.
  - Even in this case, if there is an Active File entry for docPath, the Active File copy is considered newer.

> **Ground rule:** RAG is for **finding and understanding**, not for editing bases. Edits MUST be based on a full file from Active/Cloud.

---

## 4. Source Precedence & Selection Rules

When an agent/LLM wants to **read or modify** a file, it MUST determine which copy is authoritative using this precedence:

1. **Check Active File List**

   - If there is an entry for `docPath` in the Active File List, that file is the **latest version**.
   - Edits and SHA generation MUST be scoped to the Active File version.

2. **Otherwise, use Cloud File Set**

   - If no Active File entry exists, the file is read from the Cloud File Set using `workspace_read_file` (or equivalent).

3. **RAG Content is never the base**

   - RAG content is only used as a **hint** to find the right docPath and lines to inspect.
   - Before editing, the LLM MUST fetch the authoritative full file (Active or Cloud) even if RAG returned a "full file" chunk.

> **Authoritative source algorithm (LLM):**  
> For any edit to docPath:  
> 1) If docPath is in Active File List → treat Active as truth.  
> 2) Else → call workspace_read_file(docPath) and treat Cloud as truth.  
> 3) RAG is for discovery only.

---

## 5. SHA-256 & Versioning Ground Rules

Future and existing tools (e.g., `workspace_write_patch`, `active_file_sha256`) rely on **SHA-256 hashes** of file contents for safe, optimistic concurrency.

### 5.1 SHA from the Correct Source

When constructing an edit/patch for docPath, the LLM MUST:

- Use a SHA-256 calculated from the **same source** it considers authoritative:
  - If docPath is Active → use a **client-side SHA** (via a tool like `active_file_sha256`).
  - If docPath is Cloud-only → use a **server-calculated SHA** (e.g., returned from `workspace_read_file`).

> **Rule:** Never invent or guess SHA values. The LLM MUST always copy the SHA provided by the relevant tool/response.

### 5.2 Division of Responsibility

- **LLM:**
  - Chooses the authoritative source (Active vs Cloud) using the rules above.
  - Ensures the SHA in any patch request matches the source it used.
  - Does **not** perform concurrency logic beyond that.

- **Client / Server:**
  - Verify the SHA against the actual file before applying a patch.
  - Reject or escalate if SHAs do not match.

---

## 6. LLM Behavior Rules for Working With Files

This section gives explicit behavioral rules that tools and system prompts can enforce.

### 6.1 Selecting a File to Work On

When the LLM wants to inspect or modify a file:

1. **Identify the docPath**
   - From the Active File List, a RAG hit, or user instructions.

2. **Resolve source precedence**
   - If docPath is present in Active File List → use Active.  
   - Else → use Cloud via `workspace_read_file`.

3. **Fetch a full file**
   - For Active: rely on the client providing content and/or a dedicated tool to read it.  
   - For Cloud: call `workspace_read_file(docPath)` to obtain the full file and its SHA.

### 6.2 Using RAG Content

- The LLM may use RAG results to:
  - Discover relevant docPaths.
  - Jump to interesting line ranges.
  - Understand context and existing patterns.

- The LLM MUST NOT:
  - Assume RAG snippets are complete files.
  - Generate patches or line edits directly from `rag-snippet` content alone.

**Required pattern for edits discovered via RAG:**

1. See an interesting RAG snippet for docPath.
2. Confirm whether that docPath is in the Active File List.
3. Fetch the **full file** from the authoritative source.
4. Use that full content to compute line-level edits.

### 6.3 Patch Construction (Hook for TUL-002)

When constructing line-level patches (per TUL-002):

- Base all line numbers, `expectedOriginalLines`, and new content on the **authoritative full file**.
- Carry the SHA from the **same source** (Active via local SHA tool, or Cloud via `workspace_read_file`).
- Use docPath exactly as identified in Active/RAG/Cloud (lowercase).

---

## 7. Interactions With Other DDRs

### 7.1 AGN-003 — Agent Execute Request DDR

- docPath values may appear in:
  - Active File descriptors passed with the request.
  - RAG chunk references.
  - Tool arguments.
- AGN-003 MUST treat docPath as defined here: lowercase, unique, opaque.

### 7.2 AGN-005 — Agent Tool Implementation Spec

- All tools that accept or emit docPath MUST:
  - Treat it as an opaque key.
  - Never attempt path-based inference about repositories or domains.
  - Log docPath in a safe, structured way.

### 7.3 TUL-001 — `workspace_read_file`

- Returns full file contents and a SHA-256 for a docPath in the Cloud File Set.
- LLM MUST only use this as the authoritative base when docPath is **not** in Active File List.

### 7.4 TUL-002 — `workspace_write_patch`

- Consumes docPath and `originalSha256` to describe line-level edits.
- LLM MUST:
  - Use a SHA that matches the authoritative source selected via AGN-007.
  - Use docPath consistent with Active/RAG/Cloud.

### 7.5 Future TUL-00X — `active_file_sha256`

- Will provide a SHA-256 for files in the Active File List.
- Intended to satisfy the AGN-007 rule that Active-based patches must use a **local** SHA.

---

## 8. Future Extensions

Possible future enhancements to this DDR:

- Multiple workspaces / repos with explicit workspace IDs in addition to docPath.
- Support for different file encodings (UTF-8 vs others) while preserving the same identity rules.
- Additional provenance metadata (e.g., `lastModifiedBy`, `lastModifiedSource: active|cloud`).

For v1, the rules above are **normative** and MUST be adopted by all tools that operate on files.
