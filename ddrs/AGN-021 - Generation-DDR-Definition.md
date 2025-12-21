# AGN-021 — Generation DDR Definition

**ID:** AGN-021  
**Title:** Generation DDR Definition  
**Type:** Referential  
**Status:** Approved

## Approval Metadata
- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-20 16:45:00 EST (UTC-05:00)

---

## 1. Purpose & Scope

### 1.1 Purpose
The purpose of this DDR is to formally define the **Generation DDR** type within the Aptix ecosystem. It establishes a shared, unambiguous understanding of what a Generation DDR is, why it exists, and how it is intended to be used by humans, agents, and supporting tooling.

This DDR exists to eliminate ambiguity in DDR classification and to ensure that Generation DDRs are authored, reviewed, approved, and consumed in a consistent and enforceable manner.

### 1.2 Scope
This DDR applies to:
- All DDRs that declare their type as **Generation**
- All agents responsible for authoring, interpreting, validating, or executing Generation DDRs
- All tooling that consumes DDR metadata or content

This DDR defines **what a Generation DDR is**, not how to implement or execute one.

### 1.3 Out of Scope
This DDR does not:
- Provide authoring templates
- Define execution workflows
- Specify tooling implementations
- Contain executable logic or examples

### 1.4 Intended Audience
This DDR is written for human authors, agents, and tooling. Its content is normative and authoritative.

---

## 2. What Is a Generation DDR

### 2.1 Definition
A **Generation DDR** is a Detailed Design Review whose primary purpose is to **authorize and constrain the creation or modification of concrete artifacts** by an agent or tool.

A Generation DDR captures the **agreed-upon plan** for what is to be generated, including scope, constraints, and boundaries. A DDR that does not explicitly authorize generation is not a Generation DDR.

### 2.2 Core Intent
The core intent of a Generation DDR is to:
- Establish what may be generated
- Define constraints and limits
- Provide explicit authorization for creation
- Ensure outputs are predictable and reviewable

### 2.3 Conversational Planning as a First-Class Property
A Generation DDR is expected to emerge through structured conversation. That conversation is required to surface ambiguity, expose assumptions, and converge on a shared plan. Rushing to implementation undermines the purpose of the DDR.

### 2.4 Distinguishing Characteristics
A Generation DDR is:
- Plan-defining
- Output-authorizing
- Constraint-driven
- Intended for agent and tool consumption

### 2.5 When a Generation DDR Should Be Used
Use a Generation DDR when artifacts must be created or modified under explicit, reviewable constraints.

### 2.6 When a Generation DDR Must Not Be Used
A Generation DDR must not be used for explanation, instruction-only guidance, or policy definition.

### 2.7 Authority Boundary
A Generation DDR is the sole authority for generation within its scope. Unclear authorization must block generation.

---

## 3. Relationship to Other DDR Types

### 3.1 Generation vs Instruction
Generation DDRs authorize creation. Instruction DDRs govern behavior.

### 3.2 Generation vs Referential
Referential DDRs define meaning. Generation DDRs enable creation.

### 3.3 Generation vs Policy / Rules / Governance
Policy DDRs constrain globally. Generation DDRs authorize locally.

### 3.4 Boundary Rules and Anti-Patterns
Using Generation DDRs for explanation, policy bypass, or conversational scripting is invalid.

### 3.5 Classification Rule
Each DDR must have exactly one primary intent.

---

## 4. Required Characteristics of a Generation DDR

A Generation DDR must:
- Explicitly authorize generation
- Define output scope
- Define constraints
- Represent a stable plan
- Be reviewable and auditable
- Maintain determinism over creativity

---

## 5. Allowed and Disallowed Outputs

### 5.1 Allowed Outputs
Generation DDRs may authorize creation or modification of explicitly scoped artifacts, including structured domain-level objects.

### 5.2 Output Mechanisms and Execution Context
Output mechanisms depend on agent capabilities. Generation DDRs must not assume unavailable capabilities.

### 5.3 Modification of Existing Artifacts
Modification of existing artifacts is allowed when explicitly authorized. Implicit system mutation is prohibited.

### 5.4 No Hidden Side Effects
All side effects must be declared and approved.

### 5.5 Failure on Output Ambiguity
Ambiguity must cause a pause.

### 5.6 Structured Domain Artifacts
Structured domain artifacts are first-class outputs and must preserve structural integrity.

---

## 6. Lifecycle and Usage

This section defines safe consumption and execution, including:
- Approval gates
- Dependency discovery
- No-guessing rules
- Mandatory human approval
- Blocking semantics
- Contract modification disclosure
- Tests as first-class impacted artifacts

All contract changes and test impacts require explicit human approval.

---

## 7. Validation and Compliance

### 7.0 Journey-First Principle
Discovery may invalidate intent. DDR types must not change midstream. If intent changes, the DDR must be closed and restarted.

Validation applies only when execution is considered.

---

## 8. Non-Goals and Explicit Exclusions

This DDR does not provide authoring guides, execution specifications, subflavor definitions, or policy governance. Approval does not guarantee execution.

---

## 9. Future Evolution

### 9.6 Asset Traceability Requirement (Normative)

Any asset created or modified under a Generation DDR **MUST** record the DDR’s TLA identifier. Lack of a natural storage location does not exempt an asset from this requirement. Assets without provenance are non-compliant.

---

## 10. Summary Checklist (Normative)

This section defines enforceable checklists for qualification, readiness, execution, contract impact, traceability, and valid outcomes.

Discovery that generation should not proceed is a successful outcome.

---

**End of AGN-021**
