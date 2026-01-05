# Metadata
ID: SYS-000001
Title: Aptix Development Workflow
Type: Instruction 
Summary: SYS-001 defines the **standard operating procedure** for creating, revising, and approving.
Status: Approved
Creator: Kevin D. Wolf
Creation Date: 2026-01-01T20:03:30.805Z
Last Updated Date: 2026-01-01T20:03:30.805Z
Last Updated By: Kevin D. Wolf

# Body
---

# 1. Purpose
SYS-001 defines the **standard operating procedure** for creating, revising, and approving. It governs the human↔agent workflow, ensuring consistency, clarity, and high-quality specifications that downstream systems (agents, tools, RAG indexing, automation) can reliably consume.

This workflow applies to **all DDRs** regardless of domain (system, tooling, agent architecture, platform features, etc.).

---

# 2. Overview of the DDR Creation Process
SYS-001 defines a **multi-step, approval-gated process**:

1. Initiate DDR creation  
2. Collect preliminary metadata  
3. **Define DDR type**  
4. Define DDR goal & expected outputs  
5. Establish the section structure (50K-foot overview)  
6. Elaborate each section  
7. Internal quality & coherence pass  
8. Human review & revisions  
9. Formal approval  
10. Generate DDR output files  
11. Final delivery & integration

Each step has explicit rules for interaction, progression, and approval.

---

# 3. Step-by-Step Workflow

## **Step 1 — Initiate DDR Creation**
The human indicates intent to create or revise a DDR. The agent enters **DDR Authoring Mode** and acknowledges the request. No content is generated until required early steps are completed.

---

## **Step 2 — Collect Preliminary Metadata**
The agent asks for and records:
- **Title**
- **TLA** (three-letter acronym)
- **Summary**
- Optional **brief description**

The DDR may not proceed until all required metadata is gathered.

---

## **Step 3 — Define DDR Type (Approval-Gated)**

Every DDR **must declare exactly one primary DDR type**.

### 3.1 Allowed DDR Types
At the time of this revision, the allowed DDR types are:

- **Instruction**  
- **Referential**  
- **Generation**  
- **Policy / Rules / Governance**

The DDR type **must match one of these names exactly**.

### 3.2 Intent Requirement
The selected DDR type must reflect the **core intent** of the DDR. If the DDR appears to serve multiple intents, this indicates that the DDR’s purpose has not been clearly defined and must be resolved before proceeding.

### 3.3 Approval Gate
The DDR **may not proceed** until:
- A single DDR type has been selected
- The type matches one of the allowed values exactly
- The human explicitly approves the selected type

If a new DDR type is introduced in the future, **SYS-001 must be updated** to include it before it can be used.

---

## **Step 4 — Define DDR Goal & Expected Outputs**
The agent must work with the human to establish a shared, explicit understanding of:

### 4.1 DDR Goal
A clear statement describing:
- What the DDR is trying to achieve
- Why it exists
- What success looks like

### 4.2 Expected Outputs
The agent must capture whether the DDR will produce:
- A markdown DDR file only
- Additional artifacts (e.g., source code, tool implementation, test suite)
- **Or no output artifacts**, when applicable

### 4.3 Alignment Requirement
The goal and expected outputs must align with the declared DDR type.

### 4.4 Continuation Rule
The DDR **may not progress** until the goal and expected outputs are explicitly defined **and approved** by the human.

---

## **Step 5 — Establish Section Structure (50K-Foot Overview)**
The agent proposes the high-level outline of the DDR:
- Major sections
- Logical order
- Structural approach
- What belongs in each section

### Human Approval Required
The human must approve the section structure before moving to detailed writing.

---

## **Step 6 — Elaborate Each Section**
Once the structure is approved, the agent proceeds section-by-section:
- Expanding each section with detailed content
- Asking clarifying questions only when necessary
- Ensuring internal consistency with the DDR type, goals, and outputs

The agent must not skip sections or invent structure beyond the approved outline.

---

## **Step 7 — Internal Consistency, Alignment, and Quality Pass**
Once all sections are drafted, the agent performs a quality review:
- Cross-checks coherence
- Ensures terminology is consistent
- Confirms alignment with the declared DDR type
- Confirms alignment with DDR goals and expected outputs
- Ensures completeness without redundancy

If issues are discovered, the agent raises them and proposes revisions.

---

## **Step 8 — Human Review & Revisions**
The human reviews the complete draft and may:
- Request modifications
- Add or change requirements
- Ask the agent to refine unclear areas

Revisions continue until the human is satisfied.

---

## **Step 9 — Formal Approval**
When the human approves the DDR:
- The agent captures the **approver name**
- Records the **approval timestamp** in local human time (fallback EST)
- Updates the DDR metadata accordingly

Approval is final unless the user explicitly reopens the DDR.

---

## **Step 10 — Generate DDR Output Files**
Depending on the outputs defined in Step 4, the agent generates:

- The finalized markdown DDR file  
- Optional supporting files (tools, tests, interfaces, schema, JSONL assets)
- All files delivered via **Aptix File Bundles** according to SYS-004

The agent **must not** produce code or artifacts not approved in Step 4.

---

## **Step 11 — Final Delivery & Integration**
The agent delivers the bundle(s), which may be applied by the Aptix VS Code extension. The DDR is now ready for:
- RAG indexing
- Tool execution
- Architectural reference
- Integration into the Aptix knowledge base

---

# 4. Role of the Agent
The agent must:
- Follow this workflow strictly
- Enforce DDR type selection and approval
- Ask only necessary clarifying questions
- Maintain consistency across steps
- Avoid hallucinating requirements or structure
- Never skip approval checkpoints

The agent's job is to **facilitate accuracy, structure, and clarity**, not to make assumptions or shortcuts.

---

# 5. Role of the Human
The human:
- Selects and approves the DDR type (Step 3)
- Defines goals and outputs (Step 4)
- Approves structure (Step 5)
- Guides refinement (Step 8)
- Gives formal approval (Step 9)

The human is the **final authority** on DDR content and correctness.

# Approval
Approver: Kevin D. Wolf
Approval Timestamp: 2026-01-01T20:03:30.805Z
