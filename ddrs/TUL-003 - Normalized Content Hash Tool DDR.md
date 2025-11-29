# TUL-003 — Normalized Content Hash Tool DDR
**ID:** TUL-003  
**Title:** Normalized Content Hash Tool  
**Status:** Approved  
**Owner:** Aptix Orchestrator / Reasoner  

---

## Approval Metadata
- **Approved By:** Kevin Wolf
- **Approval Timestamp:** 2025-11-29 06:05:00 EST (UTC-05:00)

---

# 1. Purpose
`code_hash_normalized` computes a **normalized SHA-256 hash** of text content by delegating all hashing and normalization behavior to the canonical:

```csharp
public interface IContentHashService
{
    Task<string> ComputeFileHashAsync(string fullPath);
    string ComputeTextHash(string content);
}
```

The tool does **not** implement normalization logic. It is a thin, safe, deterministic wrapper that exposes normalized hashing to the LLM and agent workflows.

The purpose includes:
- Consistent hashing across OS/editor differences
- Deterministic signatures for patch operations
- Drift detection between client/local/server/indexed copies
- Stable hashing for DDRs and RAG chunks
- Semantic-change detection (whitespace-insensitive)

Normalization semantics are defined elsewhere (future DDR for `IContentHashService`).

---

# 2. Normalization Delegation & Guarantees

### 2.1 Delegation Rules
`code_hash_normalized` must:
- Delegate **all** normalized hashing to `IContentHashService.ComputeTextHash`
- Never re-implement normalization
- Never modify normalization results
- Never introduce alternative hashing paths

### 2.2 Guarantees
The tool guarantees:
- Deterministic hash for identical logical content
- Normalization always precedes hashing
- Whitespace/line-ending/indentation agnostic hashing
- Text-agnostic behavior (no language-specific parsing)
- LagoVista-wide consistency (all systems using `IContentHashService` produce same hash)

### 2.3 Non-Guarantees
The tool does **not**:
- Perform semantic normalization
- Parse source code
- Return normalized content
- Expose underlying normalization rules
- Guarantee semantic equivalence

---

# 3. Input

### 3.1 v1 Input Schema
```json
{
  "content": "string (required)",
  "docPath": "string (optional)",
  "label": "string (optional)"
}
```

### 3.2 Behavior
- `content` is required and passed directly to `ComputeTextHash`.
- `docPath` and `label` are optional metadata for logging/tracing only.
- The tool does **not** read from disk.
- Any file content must be obtained via `workspace_read_file` or Active Files.

### 3.3 Size Handling
- The tool may reject extremely large content.
- On rejection, it returns a clear error (`CONTENT_TOO_LARGE`).

---

# 4. Output

### 4.1 v1 Output Schema
```json
{
  "success": true,
  "hash": "string (sha256)",
  "contentLength": 1234,
  "docPath": "optional",
  "label": "optional",
  "errorCode": "string or null",
  "errorMessage": "string or null"
}
```

### 4.2 Field Definitions
- **success** – standard AGN-005 flag
- **hash** – normalized SHA-256 (lowercase hex)
- **contentLength** – UTF-8 byte length of original content
- **docPath/label** – echoed metadata
- **errorCode/errorMessage** – AGN-005 compliant error structure

### 4.3 Exclusions
- No normalized content is returned
- No normalization version is returned

---

# 5. Use Cases

### 5.1 Patch Tool Stability (CRITICAL)
Used by `workspace_write_patch` to:
- Compare pre & post patch states
- Avoid false mismatches due to formatting
- Ensure optimistic concurrency safety

### 5.2 Client ↔ Server ↔ Index Drift Detection
Detects true content drift vs harmless whitespace differences.

### 5.3 DDR Indexing & RAG Consistency
- Each chunk may store a normalized hash
- Supports drift detection between repo and index
- Enables duplicate-chunk elimination

### 5.4 Semantic No-Change Detection
If normalized hash is unchanged → only formatting changed.

### 5.5 Multi-Environment Consistency
Ensures stable content identity between:
- Cloud workspace
- Local Active Files
- Repo copy
- RAG chunk text

### 5.6 Logging & Telemetry
Hash fingerprints can be used for correlation and debugging.

---

# 6. Constraints

### 6.1 Behavioral
- Deterministic
- No side effects
- No semantic rewriting
- No filesystem access
- No large-stream or binary hashing via this tool

### 6.2 Integration
- Must rely exclusively on `IContentHashService`
- Must follow AGN-005 error semantics
- Must handle cancellation cleanly

### 6.3 LLM Usage Constraints
- LLM must treat the hash as opaque
- LLM must not infer normalization rules
- LLM should call sparingly (when needed for comparison)

---

# 7. Priority
`code_hash_normalized` is a **CRITICAL** tool.

It supports:
- patch integrity,
- DDR & RAG consistency,
- drift detection,
- multi-environment safety.

---

# 8. Tool Name (Canonical)
The canonical tool name for this specification is:

```
code_hash_normalized
```

This name is snake_case and stable across all models and sessions.

---

# 9. Persistence & Future DDRs
- Normalization rules will be defined in a future DDR for `IContentHashService`.
- This tool DDR remains stable even if normalization evolves.
- All hashing in the LagoVista universe must flow through `IContentHashService`.
