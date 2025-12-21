# AGN-022 — DDR RAG Representation & Quality Contract

**ID:** AGN-022  
**Title:** DDR RAG Representation & Quality Contract  
**Type:** Referential  
**Status:** Approved

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-21 06:45 EST (UTC-05:00)

---

## 1. Purpose & Scope

This DDR defines the **representation contract and quality criteria** for how Detailed Design Reviews (DDRs) are exposed to Retrieval-Augmented Generation (RAG) systems.

Its purpose is to ensure that:
- DDR retrieval is **deterministic, accurate, and trustworthy**
- LLMs can **correctly identify, classify, and apply DDRs**
- Normative rules are grounded in **authoritative DDR content**, not inferred or synthesized

This DDR governs **what must be represented and how correctness is evaluated**. It does **not** govern how RAG systems are implemented or optimized.

### In Scope
- DDR representation for RAG
- Identity, eligibility, and traceability invariants
- Quality evaluation criteria for DDR RAG content

### Out of Scope
- Embedding models or vector databases
- Chunking strategies or retrieval algorithms
- Runtime query behavior or UI integration
- Tooling, pipelines, or ingestion mechanics

This DDR applies uniformly to **all DDR types** and all future DDR indexing strategies.

---

## 2. What RAG Means for DDRs (Conceptual)

For DDRs, Retrieval-Augmented Generation (RAG) serves a routing and grounding role. Although retrieved context may influence model reasoning, DDR RAG content must not be relied upon as an authoritative source of normative rules or interpretations. All authoritative reasoning must defer to the primary Markdown DDR.

RAG is used to:
- Identify the **correct DDR asset**
- Establish authoritative context before reasoning begins

RAG is **not** used to:
- Interpret or modify DDR rules
- Synthesize new policy
- Resolve ambiguity between conflicting DDRs

### Guiding Principle

**Summaries route. Full DDRs answer.**

---

## 3. DDR Representation Model for RAG

For the purposes of RAG, each DDR is represented using a **three-form model**. These forms serve distinct roles and are intentionally optimized for **different stages of interaction** with an LLM.

The forms are **not interchangeable** and have a strict authority ordering.

Every DDR must exist in the following representations:

1. **Primary Human DDR (Markdown)**
2. **Condensed DDR Content (LLM-Supplied)**
3. **RAG Index Card (LLM Routing Representation)**

### 3.1 Primary Human DDR (Markdown)

The Markdown DDR is the **primary and authoritative source of truth**.

It:
- Defines all normative rules and constraints
- Contains full structure, context, and rationale
- Is the only representation suitable for normative interpretation, rule justification, and section-level reasoning

All authoritative conclusions about DDR behavior **must defer to this representation**.

### 3.2 Condensed DDR Content (LLM-Supplied)

The Condensed DDR Content is a **greatly summarized form** of the Markdown DDR.

It is:
- Optimized for **token efficiency**
- Intended to be **supplied to the LLM after DDR identification**
- Used to provide working context without incurring the full token cost of the Markdown DDR

This representation:
- May omit examples, narrative explanation, historical context, and non-normative elaboration
- Must preserve all **normative meaning**
- Must not introduce new rules or interpretations

This representation is generated from the primary Markdown DDR and **must not be authored independently**.

### 3.3 RAG Index Card (LLM Routing Representation)

The RAG Index Card is the **minimal LLM-facing representation** of a DDR.

It:
- Consists of **one to two sentences**
- Is optimized exclusively for **retrieval and routing**
- Exists to enable reliable identification of the correct DDR

The RAG Index Card:
- Is not a summary of the DDR
- Is not sufficient for reasoning
- Must not be used as a source of normative rules

The RAG Index Card is generated from the primary Markdown DDR as part of the DDR’s derived representations.

### Authority Ordering

1. **Primary Markdown DDR** — authoritative
2. **Condensed DDR Content** — contextual, default reasoning substrate
3. **RAG Index Card** — routing only

Any reasoning or rule application that relies solely on the RAG Index Card is invalid.

---

## 4. RAG Index Card Shape (Embedded Representation)

The **RAG Index Card** is the minimal representation of a DDR intended for **embedding and retrieval**.

Its sole purpose is to enable **reliable identification and routing** to the correct DDR.

### 4.1 Content Characteristics

The RAG Index Card:
- Consists of **one to two sentences**
- Is optimized for low token count and high disambiguation value
- Contains **no normative rules**
- Avoids implementation detail, examples, or elaboration

### 4.2 Required Elements

Every RAG Index Card **MUST** include, explicitly and verbatim:
- **DDR ID**
- **DDR Type**
- **Status**
- **Approval Metadata**
- **Concise purpose statement**

### 4.3 Explicit Constraints

The RAG Index Card **MUST NOT**:
- Contain MUST / MUST NOT rules
- Be treated as a summary of the DDR
- Be used as a source of normative reasoning

---

## 5. LLM DDR Representation Requirements (Invariants)

These invariants apply to all DDR representations exposed to RAG.

### 5.1 Required Identity Fields

Every DDR representation intended for RAG **MUST** include:
- DDR ID
- DDR Type
- Status
- Approval Metadata

### 5.2 Identity Integrity Rules

- DDR IDs and TLAs **MUST appear verbatim**
- IDs are **case-sensitive**
- A representation **MUST NOT** reference multiple DDR IDs

### 5.3 Purpose Statement Requirements

Each representation **MUST** include a one-to-two sentence purpose statement describing **why the DDR exists**.

### 5.4 Authority & Usage Constraints

- The RAG Index Card is valid only for identification and routing
- The Condensed DDR Content is the default reasoning substrate
- The Markdown DDR remains authoritative

### 5.6 Condensed DDR Content Requirements

The Condensed DDR Content:
- MUST preserve all normative meaning
- MUST include all rules required for correct reasoning
- MAY omit examples and non-normative elaboration
- MUST NOT introduce new rules or interpretations

### 5.7 Derived Representation Generation (Invariant)

The primary Markdown DDR is the authoritative source used to generate:
- The RAG Index Card
- The Condensed DDR Content

Derived representations:
- MUST NOT be authored independently
- MUST preserve identity, approval metadata, and normative meaning
- MUST be rejected and regenerated if confidence cannot be established

---

## 6. Indexing & Eligibility Rules

- Only **Approved** DDRs are indexed by default
- Draft or Deprecated DDRs require explicit override

The RAG Index Card is the only representation embedded by default.

Upon identification, **only the Condensed DDR Content is forwarded to the LLM** by default.

---

## 7. Identity, Traceability, and Reference Integrity

Any DDR usage must be explainable and auditable.

- DDR ID, Type, and Status must be stated
- Normative claims must trace to Condensed or Markdown DDR
- Section-level justification should be possible
- If no DDR applies, the system must state so

---

## 8. Quality Yardstick & Evaluation Criteria

DDR RAG quality is evaluated based on:
- Identification accuracy
- Type awareness
- Normative fidelity
- Justification ability
- Hallucination resistance
- Regression detection over time

---

## 9. Non-Goals & Explicit Exclusions

This DDR does **not** define:
- Retrieval implementations
- Tooling or pipelines
- Runtime behavior or UI
- Optimization strategies

---

## 10. Position in the DDR System

AGN-022 is a **capstone Referential DDR** defining correctness and quality for DDR representation in RAG contexts.

It introduces no new workflows or tools, but serves as the authoritative reference for determining whether DDRs have been represented and indexed correctly.

---

**End of AGN-022**
