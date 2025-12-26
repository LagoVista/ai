# SYS-009 — Code Generation Interaction Protocol

**ID:** SYS-009  
**Title:** Code Generation Interaction Protocol  
**DDR Type:** Instruction  
**Status:** Approved

## Approval Metadata
- **Approved By:** Kevin D. Wolf  
- **Approval Timestamp:** 2025-12-25 EST (UTC-05:00)

---

## 1. Purpose

This DDR defines the mandatory interaction protocol an LLM must follow whenever it is asked to generate or modify source files.

The goal is to ensure file generation is predictable, reviewable, and safe by requiring explicit human validation of file paths and operations before any files are created, replaced, deleted, or patched.

---

## 2. Core Interaction Rule

When generating files, the LLM **must** follow this sequence:

1. Declare all files and paths first
2. Wait for explicit human confirmation
3. Only then generate files

No file creation, replacement, deletion, or patching may occur before confirmation.

This rule is mandatory.

---

## 3. File Declaration Requirements

Before any file generation, the LLM must present a file plan.

For each file, the plan must include:
- **Path** (relative to repository root)
- **Operation**
  - `create`
  - `replace`
  - `delete`
  - `patch` (only if patch-safe)
- **Brief description**
  - What the file is
  - Why it is being generated or modified

Rules:
- File contents MUST NOT be included at this stage
- Paths must comply with SYS-004 path rules
- Ambiguous paths require clarification before proceeding

---

## 4. Human Confirmation Contract

The LLM must obtain explicit human approval of the file plan.

Valid confirmation examples:
- “Approved”
- “Looks good, proceed”
- “Yes, generate these files”

Invalid confirmation:
- Silence
- Partial acknowledgment
- Assumptions based on prior context

If the human:
- **Rejects** a file → it must be removed from the plan
- **Modifies** a path or operation → the plan must be restated and re-approved

There is no implicit approval.

---

## 5. Generation Execution Rules

After confirmation:
- The LLM must generate files exactly as approved
- Generation must use:
  - **SYS-004 Aptix File Bundle**, or
  - An explicitly approved file-generation tool

Rules:
- No additional files may be introduced
- No paths may be changed
- No operations may be altered

If changes are needed, the process restarts at Section 3.

---

## 6. Fixed Path Invariants

Some paths are non-negotiable and do not require confirmation:

- **DDR markdown files**
  - Must always be written to:
    ```
    ./ddrs/{ID} - {Title}.md
    ```
  - As defined in **SYS-004**

All other file paths always require human confirmation.

---

## 7. Guessing or Inferring on types

- When creating code you should not guess on types that you were not supplied to you.  
- You may infer the shape of classes based on properties that are used in the supplied code.
- You may not attempt to guess at what other methods, properties or constructors that the class may have if they do not exist in supplied code.

## 8. Non-Goals

This DDR does not define:
- Coding standards
- File contents
- Optimization strategies
- Patch heuristics
- Prompt formatting
- Tool invocation mechanics

It governs interaction and sequencing only.

---

**End of SYS-009**