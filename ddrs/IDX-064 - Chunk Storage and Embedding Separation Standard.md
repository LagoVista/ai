# IDX-064 — Chunk Storage and Embedding Separation Standard

**TLA:** IDX  
**Index:** 064  
**Status:** Draft  
**Owner:** Aptix / Kevin Wolf  

## 1. Purpose

IDX-064 defines a universal rule for how all chunks in the LagoVista/Aptix indexing ecosystem are:

1. **Stored** — as authoritative **structured JSON** in the Content Repository.
2. **Embedded** — as **deterministically flattened plain text** used to produce vector embeddings.
3. **Retrieved** — via vector search followed by **JSON rehydration** for LLM reasoning.

This standard applies across all SubKinds, present and future.

---

## 2. Goals

1. Ensure every chunk exists in **two representations**:
   - Structured JSON (system of record)
   - Flattened plain text (embedding payload)

2. Provide **consistent, deterministic embedding behavior** across all chunk types.
3. Enable **high-quality LLM reasoning** by supplying structured JSON after retrieval.
4. Support drift detection and reproducibility by having a stable serialized chunk.
5. Separate semantic content from embedding mechanics so LLM tools can reliably operate.

---

## 3. Scope

This DDR applies to **all chunks**, regardless of:
- Kind
- SubKind
- Flavor
- Symbol type
- Domain or layer

Examples include:
- InterfaceOverview (IDX-042 / IDX-063)
- ModelOverview (IDX-037/038)
- Manager/Repository Overviews (IDX-039/040)
- Controller EndpointDescriptions (IDX-041)
- Domain metadata chunks
- Any future Overview or metadata chunk

IDX-064 is cross-cutting and mandatory.

---

## 4. Storage Model

### 4.1 Structured JSON as System of Record

All chunks must be serialized as **structured JSON objects** and written to the central **Content Repository**. These files represent the authoritative, full-fidelity version of the chunk.

This stored JSON includes:
- Identity fields
- Structural metadata
- Enriched semantic fields
- Domain metadata
- Relationship/linkage data
- Method/property summaries
- Any additional SubKind-specific fields

### 4.2 No Raw Source Storage

Chunks must never store raw source code. They may store structured references (line numbers, entity names, metadata) but not the original code.

---

## 5. Embedding Model

### 5.1 Deterministic Flattening Required

Before embedding, each chunk’s structured JSON is passed through a deterministic **Flattening Algorithm** that:

- Converts structured JSON to plain text
- Produces a stable representation for identical input JSON
- Includes only fields relevant to semantic meaning
- Omits fields not useful for embeddings (e.g., CharStart/CharEnd)

### 5.2 Flattened Text is Embedding Payload

Only the flattened text is passed to the embedder. The vector DB stores:
- Vector
- Chunk key
- RagScope metadata
- Minimal identifiers

The vector DB **does not** store JSON.

---

## 6. Retrieval Model

### 6.1 Vector ➜ Chunk Lookup

After vector search returns neighbor IDs:

1. The pipeline resolves the chunk ID in the Content Repository.
2. The complete **structured JSON chunk** is fetched.
3. The JSON, not the flattened text, is supplied to the LLM.

### 6.2 Reasoner Receives Structured JSON

All LLM-based reasoning, analysis, code generation, and refactoring operations must operate on the **structured JSON**, never on the flattened embedding payload.

### 6.3 Consistency Requirement

The JSON used to create the embedding must match exactly the JSON stored in the Content Repository. This maintains deterministic embedding behavior and drift detection.

---

## 7. Drift Detection

The Content Repository acts as the canonical truth. During subsequent indexing runs:

- If the newly generated structured chunk JSON does not match what is stored, a **drift condition** is recorded.
- Drift may signal:
  - Source code changes
  - Chunker logic changes
  - Enrichment logic changes
  - Metadata divergence

Tools may respond by:
- Recomputing embeddings
- Requesting reindexing
- Surfacing alerts for review

Vector drift is not authoritative; **JSON drift is**.

---

## 8. Flattening Algorithm Expectations

(The precise algorithm will be defined in a future DDR.)

This DDR mandates:

- Deterministic ordering of fields
- Stable string formatting
- Inclusion of enriched fields defined by all other IDX DDRs
- Exclusion of fields irrelevant to semantic meaning
- Safe handling of arrays (Responsibilities, UsageNotes, Methods)
- Consistent whitespace rules

The flattening rules must be global and uniform across all chunk types.

---

## 9. Applicability to Existing DDRs

Effective immediately:
- All existing IDX DDRs must treat this DDR as a foundational rule.
- All future IDX DDRs must reference IDX-064 when defining embedding behavior.
- Existing DDRs (IDX-037, 038, 039, 040, 041, 042, 063) shall rely on IDX-064 for embedding.

---

## 10. Rationale

Separating **structured JSON storage** from **flattened embedding text** ensures:

- High-quality embeddings through dense, controlled text
- Reliable reasoning via structured JSON
- Deterministic and reproducible indexing
- Robust drift detection and change tracking
- Flexibility as DDRs evolve without changing embeddings unexpectedly

This DDR becomes a foundational invariant for the entire Aptix indexing ecosystem.
