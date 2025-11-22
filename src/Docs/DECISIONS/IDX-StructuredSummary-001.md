# IDX-StructuredSummary-001 — SummarySection Architecture & Rationale

**Status:** Accepted  
**Applies To:** All structured ChunkFlavors (ModelStructureDescription, Manager, Repository, Interface, Controller, Endpoint, etc.)  
**Primary Goal:** Convert rich, structured C# objects into LLM-friendly, human-readable sections that can be chunked and embedded consistently.

---

## 1. Problem & Context

We are introducing a family of *structured* chunk flavors (for example, `ModelStructureDescription` from IDX-0037, and newer Manager/Repository/Controller/Endpoint descriptions).

These objects:

- Are **highly structured** (C# classes projected from attributes, resource dictionaries, and reflection).
- Contain **rich semantics** (domains, capabilities, UI affordances, relationships).
- Are **not ideal** to embed as:
  - raw JSON,
  - raw C# object dumps,
  - or arbitrary serialization formats.

Instead, we convert them into **human-readable, semantically meaningful text** that LLMs are good at understanding and retrieving.

We also want this process to:

- Be **repeatable and deterministic**
- Be **decoupled** from the vector DB payload construction
- Allow each ChunkFlavor to control *how it describes itself in text*

---

## 2. Core Concept — SummarySection

To bridge structured data and embedding text, we use a small, stable abstraction:

```csharp
public sealed class SummarySection
{
    public string SectionKey { get; set; }       // "model-overview", "manager-methods"
    public string SectionType { get; set; }      // "Overview", "Methods", "Properties", etc.
    public string Flavor { get; set; }           // "ManagerDescription", "ModelStructureDescription"

    public string Symbol { get; set; }           // "Device"
    public string SymbolType { get; set; }       // "Model", "Manager", etc.

    public string DomainKey { get; set; }        // e.g. "DeviceMgmt"
    public string ModelClassName { get; set; }   // e.g. "Device"
    public string ModelName { get; set; }        // e.g. "Device"

    public string SectionNormalizedText { get; set; } // Human-readable text we embed
}
```

### Field Semantics

- **SectionKey** – Logical identifier for *what part* of the description this is
- **SectionType** – Low-cardinality section group (Overview, Methods, Properties, Relationships)
- **Flavor** – Which structured ChunkFlavor produced this section
- **Symbol / SymbolType** – What named thing this is, and what kind of thing it is
- **DomainKey / ModelClassName / ModelName** – Cross-linking hooks between related artifacts
- **SectionNormalizedText** – The ONLY content sent to the embedding model

All other fields exist to support:

- Filtering
- Reasoning loops (RSN-XXX)
- Cross-artifact linking
- RagScope narrowing

---

## 3. Interface for ChunkFlavors — ISummarySectionBuilder

Each structured ChunkFlavor is responsible for:

1. Knowing how to describe itself in human-readable form
2. Emitting one or more `SummarySection` instances

Standardized via:

```csharp
public interface ISummarySectionBuilder
{
    IEnumerable<SummarySection> BuildSections(DomainModelHeaderInformation headerInfo, int maxTokens = 6500);
}
```

Responsibilities:

- Decide how many sections to emit
- Decide what each section covers
- Populate:
  - SectionKey
  - SectionType
  - Flavor
  - Symbol / SymbolType
  - DomainKey / ModelClassName / ModelName
  - SectionNormalizedText

The **chunking / payload pipeline** then:

- Splits `SectionNormalizedText` if needed (by token limit)
- Preserves `SectionKey`, `SectionType`, `Symbol`, `SymbolType`
- Creates `RagChunk` + `RagVectorPayload`

> ChunkFlavors control **how they speak**; the pipeline controls **how that speech is sliced and stored**.

---

## 4. Processing Flow End-to-End

1. Populate structured object (e.g. `ManagerDescription`)
2. Build `SummarySection` objects via `BuildSections(...)`
3. Convert them to `RagChunk` objects
4. Use `RagPayloadFactory` to create `RagVectorPayload`
5. Store in Qdrant

At query time:

- Vector similarity uses `SectionNormalizedText`
- Filtering/reasoning uses metadata (SymbolType, SectionKey, Flavor, DomainKey, etc.)

---

## 5. Why Not Embed Raw JSON or C#?

We intentionally do **not** embed:

- Raw JSON projections
- Raw C# dumps
- Arbitrary serialized formats

LLMs reason best over **natural language**, not structural dumps. This approach:

- Compresses meaning into fewer tokens
- Improves consistency and recall
- Makes future schema evolution safer

---

## 6. Naming Strategy

We use **Symbol / SymbolType** instead of Class/SubKind

This design:

- Generalizes to non-C# assets (SQL, workflows, UI components, etc.)
- Keeps a unified mental model: *"Symbol = the named thing we’re talking about"*

---

## 7. Final Decision Summary

- ✅ Introduce `${SummarySection}` as the bridge between structure and text
- ✅ Each ChunkFlavor implements `ISummarySectionBuilder`
- ✅ Only `SectionNormalizedText` is embedded
- ✅ All other fields become metadata for filtering and reasoning
- ✅ Enables RagScope + RSN-XXX model

**Status: Accepted — locked for initial index run**