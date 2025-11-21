# IDX-StructuredSummary-001 — SummarySection Architecture & Rationale

**Status:** Accepted  
**Applies To:** All structured ChunkFlavors (ModelStructureDescription, Manager, Repository, Interface, Controller, etc.)  
**Primary Goal:** Convert rich, structured C# objects into LLM-friendly, human-readable sections that can be chunked and embedded consistently.

---

## 1. Problem & Context

We are introducing a family of *structured* chunk flavors (for example, `ModelStructureDescription` from IDX-0037, and future Manager/Repository/Controller descriptions).

These objects:

- Are **highly structured** (C# classes projected from attributes, resource dictionaries, and reflection).
- Contain **rich semantics** (domains, capabilities, UI affordances, relationships).
- Are **not ideal** to embed as:
  - raw JSON,
  - raw C# object dumps,
  - or arbitrary serialization formats.

Instead, we want to convert them into **human-readable, semantically meaningful text** that LLMs are good at understanding and retrieving.

We also want this process to:

- Be **repeatable and deterministic**.
- Be **decoupled** from the vector DB payload construction.
- Allow each ChunkFlavor to control *how it describes itself* in text.

---

## 2. Core Concept — `SummarySection`

To bridge structured data and embedding text, we introduce a small, stable abstraction:

```csharp
public sealed class SummarySection
{
    public string SectionKey { get; set; }          // e.g. "model-overview", "model-properties"
    public string Symbol { get; set; }              // e.g. "Device"
    public string SymbolType { get; set; }          // e.g. "Model", "Manager", "Repository"
    public string SectionNormalizedText { get; set; } // Human-readable text we embed
}
```

### 2.1 Field Semantics

- **SectionKey**  
  Logical identifier for *what part* of the description this is.

  Examples:
  - `"model-overview"`
  - `"model-properties"`
  - `"model-relationships"`
  - `"manager-overview"`
  - `"manager-methods"`

  Used later as:
  - `RagChunk.SectionKey`  
  - Payload `SectionKey` for filtering and debugging.

- **Symbol**  
  Answers: *"what is this section about?"*  
  Typically the **class or logical object name**.

  Examples:
  - `"Device"`, `"Customer"` for models.
  - `"DeviceManager"`, `"AlertManager"` for managers.

  Mirrors the idea from `RagChunk.Symbol` and `RagVectorPayload.Symbol`.

- **SymbolType**  
  Answers: *"what kind of thing is `Symbol`?"*

  Examples:
  - `"Model"`
  - `"Manager"`
  - `"Repository"`
  - `"Interface"`
  - `"Controller"`

  Usually derived from Kind/SubKind or the structured flavor type.  
  Mirrors the idea from `RagChunk.SymbolType` and `RagVectorPayload.SymbolType`.

- **SectionNormalizedText**  
  The **actual text** that will be:

  1. Chunked into one or more `RagChunk` instances.
  2. Embedded and stored in the vector DB.

  Requirements:

  - Human-readable and stable.
  - Deterministic given the same underlying structured object.
  - Focused on the **essentials**:
    - Identity (model name, domain, etc.)
    - Key properties
    - Relationships
    - Capabilities
    - Important URLs / affordances

  We do **not** store additional title/type fields here; those concepts are folded into the text itself (for example, as headings or leading sentences).

---

## 3. Interface for ChunkFlavors — `ISummarySectionBuilder`

Each structured ChunkFlavor is responsible for:

1. Knowing how to describe itself in human-readable form.
2. Emitting one or more `SummarySection` instances.

To standardize this, we define:

```csharp
public interface ISummarySectionBuilder
{
    IEnumerable<SummarySection> BuildSections();
}
```

### 3.1 Responsibilities

**ChunkFlavor classes** (for example, a `ModelStructureDescription` adapter):

- Implement `ISummarySectionBuilder`.
- Decide **how many** sections to emit.
- Decide **what each section covers**, such as:
  - overview,
  - properties,
  - relationships,
  - capabilities,
  - endpoints.
- Populate for each section:
  - `SectionKey` (for example, `"model-overview"`, `"model-properties"`),
  - `Symbol` (for example, `"Device"`),
  - `SymbolType` (for example, `"Model"`),
  - `SectionNormalizedText` (human-readable summary text).

**Chunking / payload pipeline**:

- Takes the `SummarySection` instances.
- Applies token-based splitting (using `TokenEstimator` and existing RagChunk rules).
- Produces `RagChunk` instances with:
  - `SectionKey`,
  - `Symbol`,
  - `SymbolType`,
  - `TextNormalized`,
  - `EstimatedTokens`, line/char ranges as appropriate.
- Builds `RagVectorPayload` using existing primitives (`RagPayloadFactory`, `RagVectorPayload`, etc.).

> Key point: ChunkFlavors control **how they speak**; the pipeline controls **how that speech is sliced and stored**.

---

## 4. Processing Flow End-to-End

Using `ModelStructureDescription` (IDX-0037) as a concrete example:

1. **Populate structured object**  
   Upstream code extracts metadata via attributes, resource files, reflection, etc., and builds a populated `ModelStructureDescription` instance.

2. **Build sections**  
   A flavor-specific adapter implements `ISummarySectionBuilder` and, given the `ModelStructureDescription`, returns an `IEnumerable<SummarySection>` such as:

   - `SectionKey = "model-overview"` — overall description of the model.
   - `SectionKey = "model-properties"` — properties, groups, key flags.
   - `SectionKey = "model-relationships"` — entity headers, children, relationships.

3. **Convert sections to RagChunks**  
   For each `SummarySection`:

   - Take `SectionNormalizedText` as the base text.
   - Use `TokenEstimator` to keep within the configured token budget.
   - If a section is too large, split it into multiple `RagChunk` slices while preserving:
     - `SectionKey`,
     - `Symbol`,
     - `SymbolType`.
   - For each slice:
     - Set `RagChunk.TextNormalized` to the slice.
     - Set `RagChunk.SectionKey = SummarySection.SectionKey`.
     - Set `RagChunk.Symbol = SummarySection.Symbol`.
     - Set `RagChunk.SymbolType = SummarySection.SymbolType`.

4. **Build payloads & persist**  
   The normal payload factory (`RagPayloadFactory`) converts chunks into `RagVectorPayload` instances with the usual metadata:

   - Identity (Org, Project, DocId),
   - Content classification, Subtype, SectionKey,
   - Source pointers (Repo, Path, line ranges, hashes),
   - Embedding metadata (IndexVersion, model name, content hash).

   The resulting vectors + payloads are written to Qdrant.

5. **Query time**  
   When the LLM retrieves data:

   - Each chunk carries `Symbol` and `SymbolType`, indicating *which* object it refers to.
   - `SectionKey` indicates *which aspect* of that object is described (overview, properties, relationships, etc.).
   - `TextNormalized` is human-readable, making it straightforward for the LLM to stitch together a coherent view of the model/manager/repository.

---

## 5. Why Not Embed Raw JSON or C#?

We intentionally **do not** embed:

- Raw JSON projections of the structured models.
- Raw `ToString()` dumps of C# objects.
- Auto-serialized data structures that are not optimized for semantic meaning.

**Reasons:**

- LLMs reason more effectively over **natural language** than over configuration-shaped JSON.
- Raw formats are noisy and increase token usage without improving recall.
- Human-readable summaries compress meaning into fewer tokens and give us better control.
- This approach is more stable as schemas evolve; we can adjust the summary shape without changing the storage contract.

---

## 6. Symbol & SymbolType Naming Rationale

We deliberately use **Symbol** and **SymbolType** instead of `ClassName` and `SubKind` so that the design:

- Generalizes beyond C# types (for example, SQL objects, views, workflows, UI components).
- Keeps the same mental model across different content types:
  - *"Symbol" = the named thing we are talking about.*
  - *"SymbolType" = what kind of thing that symbol is.*
- Aligns with future plans to embed non-code assets while using the same query patterns.

For typical .NET models and services today:

- `Symbol` will usually be the **class name** (for example, `"Device"`, `"DeviceManager"`).
- `SymbolType` will usually be the **SubKind** or high-level category (for example, `"Model"`, `"Manager"`, `"Repository"`).

---

## 7. Final Decision Summary

- ✅ Introduce a small `SummarySection` abstraction as the bridge between structured objects and embedded text.
- ✅ All structured ChunkFlavors implement `ISummarySectionBuilder` and are responsible for their own human-readable summaries.
- ✅ The chunking pipeline focuses on token safety and RagChunk construction, not on formatting text.
- ✅ `Symbol` / `SymbolType` become the cross-cutting identifiers used in both chunks and payloads.
- ✅ We avoid embedding raw JSON/C# and instead feed LLMs the kind of text they understand best.

This document serves as the reference for implementing structured summary generation and should be kept in sync with future DDRs that extend ChunkFlavor coverage.
