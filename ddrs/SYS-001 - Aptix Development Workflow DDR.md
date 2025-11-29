# SYS-001 — Aptix Development Workflow DDR

**ID:** SYS-001  
**Title:** Aptix Agent Development Workflow  
**Status:** Approved  
**Owner:** Kevin Wolf & Aptix  
**Scope:** Applies to all DDRs, tools, specs, and system design work across the LagoVista / Aptix ecosystem.

---

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-11-29 05:20:00 EST (UTC-05:00)

---

## 1. DDR Identifier Assignment (TLA + Index System)

Every DDR begins with identifier allocation.

1. DDR identifiers follow the pattern: **TLA-###** (for example: `TUL-002`, `SYS-001`).  
2. **TLA (Three-Letter Acronym)** represents a logical domain, such as:
   - `AGN` — Agent / Reasoner
   - `TUL` — Tools
   - `SYS` — System Workflows
   - `UIX` — UI/UX
   - Additional TLAs may be introduced over time as needed.
3. **Index** is a zero-padded integer (`001`, `002`, …) and must be **globally unique within each TLA across all repositories**. For example, only one `TUL-002` may exist in the entire ecosystem.  
4. Aptix must:
   - Track all TLAs introduced
   - Track the next sequence number per TLA
   - Guarantee unique assignment per TLA
   - Never reuse numbers once allocated
5. DDR approval metadata must include:
   - Approver identity (for now typically `Kevin Wolf`, but future approvers are also allowed)
   - Approval timestamp including timezone
   - The user’s local timezone should be used when possible; if not available, default to U.S. Eastern Time.

---

## 2. High-Level Workflow Overview

This section summarizes the development workflow governed by SYS-001.

1. **Assign DDR Identifier (TLA-###).**  
2. **Draft DDR spec in-stream (before any implementation).**  
3. **Begin review with a 50K-foot summary.**  
4. **Review each bullet from the summary one-by-one in detail.**  
5. **Aptix asks clarifying questions only when they add value.**  
6. **User explicitly approves the DDR (with approver + timestamp).**  
7. **Aptix generates the DDR file as a bundle into `./ddrs/`.**  
8. **Code generation occurs only when explicitly requested (deferred to a separate DDR).**  
9. **Test-generation expectations are recognized but deferred to a separate DDR.**  
10. **Aptix must remember and follow this workflow long-term.**

---

## 3. Review Process

### 3.1 50K-Foot Summary

Before deep review, Aptix must generate a **high-altitude bullet list** summarizing the DDR:

- Short, concise, and focused on major themes.
- No implementation details, field names, or deep edge cases.
- Used to validate alignment on direction before going into detail.

This step ensures early detection of misunderstanding and allows cheap corrections before extensive work is done.

### 3.2 Deep-Dive Review (One Bullet at a Time)

After the 50K-foot summary is accepted:

1. Aptix takes the first bullet from the summary.
2. Expands it into detailed text including rationale, responsibilities, and edge cases.
3. Waits for user feedback and approval.
4. Moves to the next bullet.
5. Repeats until all bullets have been reviewed and refined.

This one-by-one process guarantees comprehensive understanding and avoids glossing over important points.

### 3.3 Clarifying Questions and Pushback

- Aptix **should** ask clarifying questions when there is real ambiguity, missing information, or a significant risk of misinterpretation.  
- Aptix **must not** invent questions solely to meet a quota or pattern (for example, always asking exactly 5 questions per bullet).  
- The number of questions is **adaptive**, based solely on the needs of the design.
- If clarification is needed, questions should be asked **before** producing the detailed expansion for that bullet.
- If no questions are needed, Aptix should explicitly note this (for example: “No clarifying questions — proceeding to detailed expansion.”).
- Aptix is expected to push back when:
  - Requirements appear inconsistent with existing DDRs
  - The design introduces clear risk or complexity
  - There is a significantly better design direction available

The goal is balanced collaboration: ask when it matters, skip when it doesn’t.

---

## 4. DDR Approval

1. A DDR is only considered complete when it is **explicitly approved** by the user.  
2. Aptix must not generate file bundles, code, or tests prior to explicit approval.  
3. Upon approval, Aptix must capture:
   - Approver identity
   - Approval timestamp with timezone
4. If the DDR is modified after approval, Aptix must:
   - Treat the DDR as unapproved
   - Present the updated spec
   - Request and capture re-approval

Explicit approval is required to prevent premature implementation and to keep the specification and implementation in sync.

---

## 5. DDR File Bundling Rules

### 5.1 DDR Bundle Contents

- Upon approval, Aptix generates an Aptix file bundle that contains **exactly one file**: the DDR markdown file.  
- The file path follows the pattern:

  ```
  ./ddrs/[DDR_ID] - [Title].md
  ```

- The bundle generated at this step must contain **only the DDR file** and no other artifacts.

### 5.2 Separation of Spec and Implementation

- Implementation code, helper classes, orchestrators, validators, or test suites **must not** be included in the DDR bundle.
- Additional files (implementation or tests) are generated **only when explicitly requested by the user** and always as **separate Aptix file bundles**.
- Aptix must never assume that code or tests should accompany a DDR by default.

### 5.3 DDR File Content

The DDR markdown file must include:

- DDR ID
- Title
- Status (for example, Draft, Approved)
- Approval metadata (approver and timestamp) once approved
- The full DDR specification text

This ensures the DDR file is a self-contained artifact describing the behavior or design it governs.

---

## 6. Distributed DDR Repository Architecture

### 6.1 Repository Structure

- Every LagoVista / Aptix repository must contain a top-level directory named:

  ```
  ./ddrs
  ```

- All DDRs associated with that repository are stored within this directory.

### 6.2 Cross-Repository Indexing

- All DDRs from all repositories are indexed into a global knowledge space (for example, a RAG index) so that agents can search **across the entire LagoVista universe**.
- Indexing should occur whenever DDR files are pushed to source control so that the indexed copy and the repo copy remain in sync.

### 6.3 Drift Detection

- The content of DDRs stored in source control and the content of DDRs stored in the global index must match exactly.
- If drift (mismatch) between the indexed DDR and the repository DDR is ever detected:
  - Aptix or the surrounding system should treat this as a **critical error.**
  - Automated processes that rely on that DDR should halt or fail fast.
  - The discrepancy must require **manual user intervention** and review.
- No silent auto-correction or automatic reconciliation is allowed for DDR drift.

DDR documents are governance artifacts and must remain authoritative and auditable.

### 6.4 Repository Assignment Flexibility

- DDRs should normally live in the repository that best matches their semantics (for example, agent-related specs in an AI/agent repo, UI DDRs in the UI repo).  
- The system does **not** strictly enforce which repo a DDR must be in, beyond the requirement that each repo maintain a `./ddrs` folder.  
- The only hard global constraint is DDR ID uniqueness within each TLA.

---

## 7. Code Generation (Deferred to Separate DDR)

The SYS-001 workflow acknowledges that:

- Code generation occurs **only when explicitly requested** by the user.  
- The DDR must be approved and bundled before code is generated.  

However, the detailed rules governing:

- Code structure and patterns
- Required use of helpers, validators, and orchestrators
- Naming and organizational conventions
- Prompting practices for LLM-based generation

are deferred to a future, dedicated DDR (for example: **SYS-00X — Code Generation Standards DDR**). SYS-001 only establishes that such standards will exist and that implementation must follow them.

---

## 8. Test Generation (Deferred to Separate DDR)

SYS-001 recognizes that:

- All implementation code should be accompanied by tests (for example, NUnit-based tests).
- Tests live in project-specific test folders (for example, `./tests/[Project].Tests/`).

The detailed standards for:

- Test structure and naming
- Mocking and isolation strategies
- Required coverage
- How LLM-generated tests are validated or reviewed

are deferred to a future, dedicated DDR (for example: **SYS-00Y — Test Generation Standards DDR**).

SYS-001 only notes that such standards will be defined and that test generation is an expected part of the implementation phase, once the relevant DDRs exist.

---

## 9. Persistence of Workflow

SYS-001 defines a **long-term systemic workflow rule** for Aptix and associated agents:

- Every significant feature, tool, behavioral rule, or system design change must begin with a DDR generated in-stream.
- DDRs must be reviewed starting with a high-level summary, then deep-dived bullet-by-bullet.
- Clarifying questions and pushback are encouraged when they add value.
- DDRs must be explicitly approved before any artifacts are generated.
- Approved DDRs must be stored under `./ddrs` and indexed globally.
- DDR ID allocation must follow the TLA + index scheme with global per-TLA uniqueness.
- This workflow persists across sessions and future workstreams; Aptix is expected to remember and apply SYS-001 automatically going forward.
