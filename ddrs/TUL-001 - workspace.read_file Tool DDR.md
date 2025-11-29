# TUL-001 — workspace.read_file Tool DDR

**Title:** workspace.read_file Tool  
**Status:** Draft  
**Index:** TUL-001  
**Owner:** Aptix Orchestrator / Reasoner  
**Namespace:** LagoVista.AI.Services.Tools

---

## 1. Purpose

The `workspace.read_file` tool provides Aptix agents with a read-only mechanism to retrieve the full contents of a source document from the Aptix cloud source store.

It is the primary way for the LLM to hydrate full files when it has seen only a RAG snippet. The tool:

- never touches the user’s local filesystem  
- never writes or mutates any content  
- always uses the `DocPath` from RAG snippet headers as the lookup key  
- respects `ActiveFiles` precedence so the LLM does not accidentally read stale versions

The implementation must conform to the generic Aptix Agent Tool contract defined in **AGN-005 – Agent Tool Implementation Specification**.

---

## 2. RAG snippet pathing

Each RAG snippet that represents a source document MUST include two path identifiers:

- **DocPath** – canonical path for the full source document, e.g. `src/Billing/BillingService.cs`. This is passed verbatim as `path` to `workspace.read_file`.  
- **ChunkPath** – identifier for the snippet’s position within the document, e.g. `src/Billing/BillingService.cs#method:CalculateTaxes`. This is informational only and is never passed to the tool.

When the LLM needs the full file, it must call `workspace.read_file` with the DocPath exactly as provided by RAG.

---

## 3. ActiveFiles precedence

Each Aptix session maintains an `ActiveFiles` collection keyed by DocPath.

Before consulting cloud storage, `workspace.read_file` MUST:

1. Check if `path` exists in `ActiveFiles`.  
2. If present, do not fetch from cloud; instead return an `ALREADY_IN_CONTEXT` error payload.  
3. If not present, query the Aptix cloud source store using `path` as-is.  
4. If cloud storage does not have the file, return a `NOT_FOUND` error payload.

Example error payloads (shape, not strict):

- `ALREADY_IN_CONTEXT`:
  - success: false  
  - errorCode: ALREADY_IN_CONTEXT  
  - errors: ["File 'src/Billing/BillingService.cs' is already present in activeFiles for this session; reuse that content."]

- `NOT_FOUND`:
  - success: false  
  - errorCode: NOT_FOUND  
  - errors: ["File 'src/Billing/ExperimentalService.cs' is not available in activeFiles or cloud source storage. Ask the user to upload it if needed."]

---

## 4. Tool schema (LLM-facing)

Tool name: `workspace.read_file`

Parameters:

- `path` (string, required): canonical DocPath for the document  
- `maxBytes` (integer, optional): maximum number of bytes to return; if omitted, the entire file is returned

Result (success):

- success: true  
- file:
  - path: DocPath
  - sizeBytes: long
  - content: string (UTF-8)
  - isTruncated: bool

Result (error):

- success: false  
- errorCode: one of ALREADY_IN_CONTEXT, NOT_FOUND, INVALID_ARGUMENT, INTERNAL_ERROR  
- errors: string[]

Session and conversation identifiers should be included in the payload as defined by AGN-005.

---

## 5. Execution rules (backend)

High-level execution flow:

1. Deserialize `argumentsJson` into `{ path, maxBytes }`.  
2. Validate:
   - `path` is non-empty.  
3. Check `ActiveFiles` for `path`:
   - if present → return `ALREADY_IN_CONTEXT` error payload.  
4. If not present in `ActiveFiles`:
   - call the Aptix cloud source store with `path` exactly as provided.  
   - if found → read content (respecting `maxBytes` if supplied), return success payload.  
   - if not found → return `NOT_FOUND` error payload.  
5. On argument issues → return `INVALID_ARGUMENT`.  
6. On unexpected exceptions → log and return `INTERNAL_ERROR`.

The actual cloud source store implementation is outside the scope of this DDR; this document specifies the tool’s observable contract.
