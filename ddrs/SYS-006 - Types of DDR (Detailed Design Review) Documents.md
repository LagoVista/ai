# SYS-006 — Types of DDR (Detailed Design Review) Documents

**ID:** SYS-006  
**Title:** Types of DDR (Detailed Design Review) Documents  
**Status:** Approved

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-20 (local time, fallback EST)

---

## 1. Purpose & Scope

SYS-006 defines the authoritative taxonomy of DDR (Detailed Design Review) document types used within Aptix. Its purpose is to ensure that every DDR has a clear intent, predictable interpretation, and appropriate lifecycle behavior.

This DDR governs classification and usage, not authoring mechanics. The process for creating DDRs is defined in SYS-001.

---

## 2. Why DDR Types Matter

DDR types remove ambiguity about intent and usage. Proper typing ensures consistent agent behavior, reliable tooling, accurate RAG retrieval, and long-term maintainability.

Mis-typed or untyped DDRs introduce confusion, incorrect execution, and governance drift.

---

## 3. DDR Type Overview (Authoritative)

Aptix defines four primary DDR types. Every DDR must declare exactly one primary type, based on intent.

### 3.1 Instruction DDRs
Define how a task is performed.

### 3.2 Referential DDRs
Define shared conceptual context used for reasoning.

### 3.3 Generation DDRs
Define what must be created or modified.

### 3.4 Policy / Rules / Governance DDRs (Draft)
Define authoritative constraints, invariants, and non-negotiable rules.

### 3.5 Intent Clarity Rule (Normative)

A DDR must have exactly one primary intent. If a DDR appears to serve multiple intents, this is a design smell indicating the purpose has not been crisply defined.

**Resolution order:**
1. Clarify the primary intent.
2. Refactor content to match the single intent.
3. Split the DDR only if multiple durable concerns remain.

### 3.6 Canonical Example (Intent Smell)

If a DDR contains:
- An explanation of what a Landing Page is → Referential
- Steps for how to publish a Landing Page → Instruction
- Requirements to add a new Landing Page field → Generation
- A rule stating HTML scripts are never allowed → Policy

This is not initially a case of a DDR that needs splitting. It is a signal that the DDR’s purpose has not been decided.

---

## 4. Instruction DDRs

Instruction DDRs define how a task is performed. They exist to remove ambiguity from execution and enable consistent, repeatable behavior.

They are long-lived, mode-scoped, and authoritative.

### 4.1 Purpose
Define explicit workflows, sequencing, validation, and completion criteria.

### 4.2 When to Use
Use when correct execution matters and steps must be followed deterministically.

### 4.3 When Not to Use
Do not use for explanation, creation mandates, or global rules.

### 4.4 Structural Expectations
Instruction DDRs are expected to define ordered steps, validations, and completion criteria.

### 4.5 Agent Consumption Model
Agents treat Instruction DDRs as authoritative and procedural. Creativity is out of scope.

### 4.6 Relationship to Other DDR Types
Instruction DDRs may reference Referential DDRs, be constrained by Policy DDRs, and support Generation DDRs.

### 4.7 Normative Rule
If success depends on correct execution, it is an Instruction DDR.

### 4.8 Association with Agent Modes (Normative)
Every Instruction DDR must be associated with one or more agent modes. An Instruction DDR not associated with a mode is incomplete.

### 4.9 Mode Instructions (Conceptual Definition)
Instruction DDRs produce Mode Instructions, which are the LLM-facing operational form of the DDR.

The mechanics of Mode Instruction creation are defined in AGN-018.

### 4.10 Runtime Injection Rule
When an associated mode is active, Mode Instructions must be injected into the LLM instruction context for the duration of the mode.

---

## 5. Referential DDRs

Referential DDRs define shared conceptual context used for reasoning. They inform understanding, not execution.

### 5.1 Purpose
Establish shared mental models, terminology, and relationships.

### 5.2 When to Use
Use when defining concepts, architecture, or explanatory context.

### 5.3 When Not to Use
Do not use to define workflows, outputs, or enforce rules.

### 5.4 Consumption Model
Referential DDRs are ingested for context, queried selectively, and used as reasoning scaffolding.

### 5.5 Selective Depth
They may intentionally summarize and defer detail.

### 5.6 Relationship to Other DDR Types
They support Instruction, Generation, and Policy DDRs but do not control execution.

### 5.7 Normative Rule
If the primary value is understanding, it is a Referential DDR.

> Detailed mechanics are defined in AGN-020.

---

## 6. Generation DDRs

Generation DDRs define what must be created or modified. They are outcome-driven and typically finite in lifetime.

### 6.1 Purpose
Define desired outputs, scope, and acceptance criteria.

### 6.2 When to Use
Use when a new asset must be created or an existing asset must change.

### 6.3 When Not to Use
Do not use to define workflows, concepts, or global rules.

### 6.4 Structural Expectations
Generation DDRs must clearly define scope, constraints, and success criteria.

### 6.5 Agent Consumption Model
Agents treat Generation DDRs as creation mandates, using other DDRs for execution and context.

### 6.6 Relationship to Other DDR Types
They depend on Instruction, Referential, and Policy DDRs.

### 6.7 Normative Rule
If success is measured by what is produced or changed, it is a Generation DDR.

### 6.8 Lifecycle and Longevity
Generation DDRs typically have a finite operational lifetime and are one-and-done by default.

### 6.9 Resurrection and Reuse
They may be resurrected for future modifications but are not required to be.

### 6.10 Lifecycle Contrast
Instruction and Referential DDRs are long-lived and mode-scoped. Generation DDRs are event-driven.

> Detailed semantics are defined in AGN-021.

---

## 7. Policy / Rules / Governance DDRs

Policy DDRs define authoritative constraints and invariants.

### 7.1 DRAFT SECTION — PLACEHOLDER

This section is explicitly marked as DRAFT. Detailed mechanics are deferred to POL-001.

### 7.2 Current Normative Guardrails
- Must not define workflows
- Must not define generation outputs
- May constrain other DDR types
- Are authoritative in conflict resolution

---

## 8. DDR Type Selection Guidelines

### 8.1 Start with Intent
Select DDR type based on why the DDR exists, not content.

### 8.2 Single-Intent Requirement
Every DDR must have exactly one primary intent.

### 8.3 Intent Smell
Mixed content indicates unclear intent, not a need to split immediately.

### 8.4 Mode Association Check
If the DDR must influence agent behavior across interactions, it is likely an Instruction DDR.

### 8.5 Lifecycle Check
Match expected lifetime to DDR type.

### 8.6 Common Mistakes
Avoid mixing intent, embedding policy incorrectly, or misusing Referential DDRs.

### 8.7 Final Validation Checklist
Confirm single intent, lifecycle alignment, and absence of off-intent content.

### 8.8 Goals & Expected Outcomes by DDR Type (Guidance)

This subsection provides loose guidance and will be aligned with SYS-001 in a future revision.

---

## 9. Relationship to Other SYS Protocols

SYS-006 defines classification and intent only.

- SYS-001 governs workflow
- SYS-004 governs file bundling
- AGN-018 governs Mode Instructions
- AGN-020 governs Referential DDR usage
- AGN-021 governs Generation DDR behavior
- POL-001 governs Policy DDRs

No document may redefine another’s authority.

---

## 10. Future Expansion & Governance

DDR taxonomy is foundational and must evolve conservatively.

New types require strong justification and formal approval.

Placeholder types must be explicitly formalized before use.

---

## 11. Summary & Normative Guidance

Every DDR must declare exactly one primary type based on intent.

Instruction controls execution, Referential supports understanding, Generation defines outcomes, and Policy constrains behavior.

If intent is unclear, stop and clarify before writing content.

---

# End of SYS-006
