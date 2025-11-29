# TUL-000 — Aptix Tool Catalog DDR
**Title:** Aptix Agent Tool Catalog  
**Status:** Draft  
**Index:** TUL-000  
**Owner:** Aptix Orchestrator / Reasoner

---

## 1. Purpose

This DDR defines the complete catalog of Aptix Agent Tools, establishes the authoritative, canonical **snake_case** names for all tools, and sets their priority levels for implementation sequencing.

Tools are grouped by category and assigned one of four priority levels: **CRITICAL**, **HIGH**, **MEDIUM**, or **LOW**.

This DDR does *not* define implementation details; each tool receives its own dedicated DDR (e.g., `TUL-001 workspace_read_file`).

All tools must conform to AGN-005 — **Agent Tool Implementation Specification**.

---

## 2. Naming Convention

All tools use the following canonical pattern:

```
<category>_<action>
```

Rules:
- snake_case only  
- **no dots**  
- names must be unique and stable across all sessions and model calls

Categories:
- `session_`
- `workspace_`
- `rag_`
- `code_`
- `exec_`
- `git_`
- `analysis_`
- `trace_`

---

## 3. Priority Definitions

| Priority    | Meaning |
|-------------|---------|
| **CRITICAL** | Required for first production workflows (code reading, patching, RAG, testing). |
| **HIGH**     | Needed shortly after MVP; strong leverage or governance. |
| **MEDIUM**   | Useful enhancement, adds workflow structure. |
| **LOW**      | Later-phase capabilities or optional workflow additions. |

---

# 4. Tool Catalog (Canonical Names + Priorities)

## 4.1 Session Tools

### 1. **session_get_context** — HIGH
Returns session metadata, current mode, active files, etc.

### 2. **session_append_note** — MEDIUM
Appends structured notes into the session timeline.

---

## 4.2 Workspace Tools

### 3. **workspace_list_active_files** — MEDIUM
Lists fresh user-provided active files (authoritative over cloud).

### 4. **workspace_read_file** — CRITICAL
Reads a full source file using canonical DocPath from RAG. (TUL-001)

### 5. **workspace_write_patch** — CRITICAL
Applies (or v1: generates) unified diff patches representing LLM edits.

### 6. **workspace_create_file** — CRITICAL
Creates new files (tests, configs, DDRs).

### 7. **workspace_list_files** — HIGH
Lists files known to the workspace.

---

## 4.3 RAG Tools

### 8. **rag_search_code** — CRITICAL
Semantic + filter-aware code search.

### 9. **rag_search_docs** — CRITICAL
Semantic search across DDRs, specs, policy documents.

### 10. **rag_get_chunk** — HIGH
Fetch specific RAG chunk content + metadata.

### 11. **rag_find_related_chunks** — MEDIUM
Similarity-based contextual expansion.

### 12. **rag_resolve_scope** — LOW
Not required—LLM will directly construct RagScope objects.

---

## 4.4 Code Intelligence Tools

### 13. **code_find_symbol** — CRITICAL
Maps a symbol to its defining source file.

### 14. **code_get_symbol_structure** — HIGH
Structured extraction of symbol members for planning/test generation.

### 15. **code_find_usages** — CRITICAL
Finds references / call-sites for safe refactoring.

---

## 4.5 Execution Tools

### 16. **exec_run_tests** — CRITICAL
Runs test suite.

### 17. **exec_run_build** — CRITICAL
Builds code to validate correctness.

### 18. **exec_run_checks** — MEDIUM
Runs formatters, analyzers, linters.

---

## 4.6 Git Tools

### 19. **git_get_status** — LOW
Git status of cloud workspace.

### 20. **git_stage_and_commit** — HIGH
Stage + commit changes in cloud workspace.

### 21. **git_create_patch** — LOW
Generates multi-file patch sets.

### 22. **git_apply_patch** — LOW
Applies multi-file patch sets.

---

## 4.7 Analysis Tools

### 23. **analysis_extract_requirements** — MEDIUM
Extracts structured requirements from user intent.

### 24. **analysis_generate_plan** — MEDIUM
Produces structured multi-step execution plans.

### 25. **analysis_validate_plan** — HIGH
Validates plans before human approval (coherence, safety).

### 26. **analysis_check_compatibility** — HIGH
LLM-powered compliance review of changes (DDRs, policies, architecture).

---

## 4.8 Trace Tools

### 27. **trace_log_step** — HIGH
Structured audit log of agent reasoning.

### 28. **trace_log_metric** — HIGH
Structured numeric metrics for analysis/observability.

---

# 5. Priority Summary

## 🟥 **CRITICAL**
- workspace_read_file
- workspace_write_patch
- workspace_create_file
- rag_search_code
- rag_search_docs
- code_find_symbol
- code_find_usages
- exec_run_tests
- exec_run_build

## 🟧 **HIGH**
- session_get_context
- workspace_list_files
- rag_get_chunk
- code_get_symbol_structure
- git_stage_and_commit
- analysis_validate_plan
- analysis_check_compatibility
- trace_log_step
- trace_log_metric

## 🟨 **MEDIUM**
- session_append_note
- workspace_list_active_files
- rag_find_related_chunks
- exec_run_checks
- analysis_extract_requirements
- analysis_generate_plan

## 🟦 **LOW**
- rag_resolve_scope
- git_get_status
- git_create_patch
- git_apply_patch

---

## 6. Notes
- All tool names are now canonicalized in **snake_case** with **no dot separators**.
- Individual tool DDRs (TUL-001, TUL-002, etc.) refine behavior and implementation.
- AGN-005 governs all execution, error handling, logging, and result formatting.

---

## 7. Next Steps
- Implement all CRITICAL tools (beginning with TUL-001).
- Add HIGH tools to the Reasoner boot manifest.
- Unit-test tool dispatcher integration.
