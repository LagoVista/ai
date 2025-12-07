# SYS-001 — Aptix Development Workflow

**ID:** SYS-001  
**Title:** Aptix Development Workflow  
**Status:** Approved

## Approval Metadata
- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-07 (Updated)

---

# 1. Purpose
SYS-001 defines the **standard operating procedure** for creating, revising, approving, and generating Aptix DDRs. It governs the human↔agent workflow, ensuring consistency, clarity, and high-quality specifications that downstream systems (agents, tools, RAG indexing, automation) can reliably consume.

This workflow applies to **all DDRs** regardless of domain (system, tooling, agent architecture, platform features, etc.).

---

# 2. Overview of the DDR Creation Process
SYS-001 defines a **multi-step, approval-gated process**:

1. Initiate DDR creation  
2. Collect preliminary metadata  
3. **Define DDR goal & expected outputs (new step)**  
4. Establish the section structure (50K-foot overview)  
5. Elaborate each section  
6. Internal quality & coherence pass  
7. Human review & revisions  
8. Formal approval  
9. Generate DDR output files  
10. Final delivery & integration

Each step has explicit rules for interaction, progression, and approval.

---

# 3. Step-by-Step Workflow

## **Step 1 — Initiate DDR Creation**
The human indicates intent to create or revise a DDR. The agent enters **DDR Authoring Mode** and acknowledges the request. No content is generated until steps 2 and 3 complete.

---

## **Step 2 — Collect Preliminary Metadata**
The agent asks for and records:
- **Title**
- **TLA** (three-letter acronym)
- **Summary**
- Optional **brief description**

The DDR may not proceed until all required metadata is gathered.

---

## **Step 3 — Define DDR Goal & Expected Outputs (New Step)**
The agent must work with the human to establish a shared, explicit understanding of:

### 3.1 DDR Goal
A clear statement describing:
- What the DDR is trying to achieve
- Why it exists
- What success looks like

### 3.2 Expected Outputs
The agent must capture whether the DDR will produce:
- A markdown DDR file only
- Additional artifacts (e.g., source code, tool implementation, test suite)
- **Or no output artifacts**, when applicable

### 3.3 Alignment Requirement
The goal and expected outputs must reflect the human's intentions with no hidden assumptions.

### 3.4 **Continuation Rule (Updated)**
The DDR **may not progress** until the **goal and expected outputs** are explicitly defined **and approved** by the human.

---

## **Step 4 — Establish Section Structure (50K-Foot Overview)**
The agent proposes the high-level outline of the DDR:
- Major sections
- Logical order
- Structural approach
- What belongs in each section

### Human Approval Required
The human must approve the section structure before moving to detailed writing.

---

## **Step 5 — Elaborate Each Section**
Once the structure is approved, the agent proceeds section-by-section:
- Expanding each section with detailed content
- Asking clarifying questions only when necessary
- Ensuring internal consistency with the DDR goals

The agent must not skip sections or invent structure beyond the approved outline.

---

## **Step 6 — Internal Consistency, Alignment, and Quality Pass**
Once all sections are drafted, the agent performs a quality review:
- Cross-checks coherence
- Ensures terminology is consistent
- Confirms alignment with DDR goals and outputs
- Ensures completeness without redundancy

If issues are discovered, the agent raises them and proposes revisions.

---

## **Step 7 — Human Review & Revisions**
The human reviews the complete draft and may:
- Request modifications
- Add or change requirements
- Ask the agent to refine unclear areas

Revisions continue until the human is satisfied.

---

## **Step 8 — Formal Approval**
When the human approves the DDR:
- The agent captures the **approver name**
- Records the **approval timestamp** in local human time (fallback EST)
- Updates the DDR metadata accordingly

Approval is final unless the user explicitly reopens the DDR.

---

## **Step 9 — Generate DDR Output Files**
Depending on the outputs defined in Step 3, the agent generates:

- The finalized markdown DDR file  
- Optional supporting files (tools, tests, interfaces, schema, JSONL assets)
- All files delivered via **Aptix File Bundles** according to SYS-004

The agent **must not** produce code or artifacts not approved in Step 3.

---

## **Step 10 — Final Delivery & Integration**
The agent delivers the bundle(s), which may be applied by the Aptix VS Code extension. The DDR is now ready for:
- RAG indexing
- Tool execution
- Architectural reference
- Integration into the Aptix knowledge base

---

# 4. Role of the Agent
The agent must:
- Follow this workflow strictly
- Ask only necessary clarifying questions
- Maintain consistency across steps
- Avoid hallucinating requirements or structure
- Never skip approval checkpoints

The agent's job is to **facilitate accuracy, structure, and clarity**, not to make assumptions or shortcuts.

---

# 5. Role of the Human
The human:
- Defines goals and outputs (Step 3)
- Approves structure (Step 4)
- Guides refinement (Step 7)
- Gives formal approval (Step 8)

The human is the **final authority** on DDR content and correctness.

---

# End of SYS-001