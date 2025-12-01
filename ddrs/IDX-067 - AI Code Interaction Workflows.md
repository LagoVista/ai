# IDX-067 — AI Code Interaction Workflows

**ID:** IDX-067  
**Title:** AI Code Interaction Workflows  
**Status:** Preliminary Approval  
**Owner:** Kevin Wolf & Aptix

---

# 1. Overview
This DDR defines the five foundational workflows that govern how Aptix interacts with the LagoVista codebase. Workflows represent **user intent**, not agent roles. They drive retrieval, tool selection, and reasoning strategy.

The workflows are:
1. **Discover**  
2. **Understand**  
3. **Change / Implement**  
4. **Verify**  
5. **Operate / Troubleshoot**

Each workflow has distinct responsibilities and safety requirements.

---

# 2. Workflow Specifications

## 2.1 Discover
**Purpose:** Identify where relevant behavior exists in the system.  
**Artifacts:** Finder Snippets only (no Backing Artifacts).  
**Outputs:** Ranked snippet list + conceptual location.  
**Constraints:** Minimal retrieval; deeper context fetched in later workflows.

---

## 2.2 Understand
**Purpose:** Build a conceptual graph of how a feature behaves across all layers.  
**Artifacts:** Entity models, managers, repositories, controllers, UI metadata, DDRs.  
**Outputs:** Multi-layer explanation grounded in authoritative artifacts.  
**Constraints:** No modifications; no assumptions; all structure must come from artifacts.  
**Note:** Conceptual graph is dynamically constructed from retrieved artifacts.

---

## 2.3 Change / Implement
**Purpose:** Safely modify or extend the system.  
**Requirements:** Pull all relevant Backing Artifacts; identify impacted components; assemble a Change Pack; produce artifact-first modifications.  
**Outputs:** Change Pack + validated diffs.  
**Constraints:** No hallucinations; no modification outside Change Pack; user approval required.

---

## 2.4 Verify
**Purpose:** Confirm correctness and completeness of changes or existing behavior.  
**Requirements:** Compare implementation to DDRs; detect omissions; validate invariants.  
**Outputs:** Verification Report with inconsistencies + recommended next actions.  
**Constraints:** Must rely solely on retrieved artifacts.

---

## 2.5 Operate / Troubleshoot
**Purpose:** Interpret runtime behavior (logs, errors, telemetry).  
**Requirements:** Map signals to execution paths; pull minimal required artifacts; reconstruct flow; suggest root causes.  
**Outputs:** Execution-path explanation + next-step workflow suggestions.  
**Constraints:** Cannot modify code; must rely on authoritative sources.

---

# 3. Workflow Interactions
Workflows may transition fluidly:
- Discover → Understand → Change  
- Operate → Understand → Change → Verify  
- Verify → Change  

Each workflow has no overlap with agent roles (Design/Build/QA/Oversight), which are defined separately.

---

# 4. Status
These workflows form the minimal foundation required for Finder Snippet design, Backing Artifact modeling, and safe indexing.  
They are marked **Preliminary Approval** pending integration with IDX-0XZ and related DDRs.
