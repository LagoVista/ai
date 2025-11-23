# IDX-0051 – RESX Resource Indexing

**Status:** Accepted  
**Version:** 1.0  
**Owner:** Kevin / Aptix  
**System:** LagoVista.AI.Rag – Ingestion

---

## 1. Purpose

Define how `.resx` resource files are detected, parsed, normalized, and indexed into the RAG pipeline.

RESX entries represent the **vocabulary of the system** – labels, messages, prompts, and help text that shape user experience. By indexing these entries as first-class chunks, the LLM can:

- Discover existing wording and terminology before inventing new strings.
- Help unify and refactor wording across solutions.
- Prepare for future translation and resource consolidation workflows.

IDX-0051 focuses only on **resource entries themselves**. A separate DDR (IDX-0052 – Resource Usage Map) will define how code usage of these resources is modeled and stored.

---

## 2. Scope & Goals

### 2.1 In Scope

- Detection of `.resx` files as a supported input asset.
- Extraction of individual `<data>` elements as **1:1 resource chunks**.
- Normalized model for RESX resource chunks (`ResxResourceChunk`).
- Culture resolution (invariant vs. language/locale-specific resources).
- Light usage aggregation (counts, primary usage kinds) suitable for RAG payloads.
- Integration into the normalized chunk builder pipeline (Step 7.x).

### 2.2 Out of Scope (Handled Elsewhere)

- Detailed mapping of where each resource is used in code (controllers, views, view-models, etc.).
  - This will be covered by **IDX-0052 – Resource Usage Map (RESX → Code)** and stored in the Meta Data Registry.
- Translation workflows or language services.
- Linting or style enforcement for wording.

---

## 3. Content Classification

RESX resource chunks use the existing `RagVectorPayload` classification:

- `RagContentType = Reference`
- `Kind = \"Reference\"`
- `SubKind = \"Resource\"`

Within the Resource sub-kind, entries can be further classified:

```csharp
public enum ResourceSubKind
{
    Unknown = 0,
    UiString = 1,
    ErrorMessage = 2,
    StatusLabel = 3,
    ValidationMessage = 4,
    CommandLabel = 5,
    HelpText = 6,
    Other = 99
}
```

Classification may be inferred from naming patterns (e.g. `Error_`, `Status_`, `Cmd_`) or from usage data (IDX-0052). For V1 it is acceptable to default most entries to `UiString` or `Unknown` and refine over time.

---

## 4. Detection Rules

A file is considered a RESX resource file when **all** of the following are true:

1. File name ends with `.resx` (case-insensitive).
2. Content is XML with a `<root>` element.
3. The `<root>` element contains one or more `<data>` elements with a `name` attribute.

**Examples (valid):**
- `Resources.resx`
- `Resources.en.resx`
- `Resources.en-US.resx`
- `DeviceResources.resx`

**Non-examples (ignored by this DDR):**
- `Strings.json` (not XML)
- Arbitrary XML without `<data>` elements

Detection can be implemented as a small, non-Roslyn parser that runs before or alongside other asset detection.

---

## 5. Chunking Strategy

### 5.1 Chunk Granularity

Each `<data>` element becomes **exactly one** normalized chunk.

```xml
<data name="Save" xml:space="preserve">
  <value>Save</value>
  <comment>Standard save button</comment>
</data>
```

Yields one chunk representing the `Save` resource.

### 5.2 Benefits of 1:1 Resource Chunks

- High precision when searching for or modifying specific terms.
- Easy aggregation by key, value, or culture.
- Enables future cross-project resource normalization (e.g., find all variants of \"Save\").

---

## 6. Normalized Model (ResxResourceChunk)

The logical model for each chunk is:

```csharp
public sealed class ResxResourceChunk
{
    // Identity
    public string ResourceKey { get; set; }          // e.g. "Save", "Error_DeviceNotFound"
    public string ResourceValue { get; set; }        // Localized text
    public string Comment { get; set; }              // Optional developer comment

    // Location
    public string SourceFile { get; set; }           // Full or relative file name
    public string RelativePath { get; set; }         // Repo-relative path

    // Localization
    public string Culture { get; set; }              // "" | "en" | "en-US" etc.
    public ResourceSubKind SubKind { get; set; }     // UiString, ErrorMessage, etc.

    // Aggregated usage hints (from IDX-0052; optional for V1)
    public int UsageCount { get; set; }              // Total known usages across codebase
    public IReadOnlyList<string> PrimaryUsageKinds { get; set; } // e.g. ["ButtonLabel", "DialogTitle"]

    // Convenience flags
    public bool IsDuplicate { get; set; }            // Same value appears in multiple keys
    public bool IsUiSharedCandidate { get; set; }    // Candidate for global/shared UI resource
}
```

The `UsageCount`, `PrimaryUsageKinds`, `IsDuplicate`, and `IsUiSharedCandidate` fields are **optional** for V1 and may be populated by a later indexing pass defined in IDX-0052.

---

## 7. Culture Detection

Culture is resolved using the following precedence:

1. **File name pattern**
   - `Resources.resx`          → invariant (empty or null culture)
   - `Resources.en.resx`       → `en`
   - `Resources.en-US.resx`    → `en-US`

2. **XML metadata** (if present)
   - e.g., a `<resheader name="Language">` node or similar project-specific convention.

3. **Fallback**
   - If no culture can be determined, treat as invariant.

---

## 8. Embedding Text Template

The text sent to the embedding model for each resource chunk should be a small, self-contained description:

```text
RESX Resource Entry

Key: Save
Value: Save
Comment: Standard save button
Culture: invariant
Source: MyApp/Resources.resx
```

**Requirements:**

- Human-readable and LLM-friendly.
- Includes enough context (key, value, comment, culture, file) to differentiate similar entries.
- Avoids verbose usage maps – detailed usage will be handled via the Meta Data Registry.

---

## 9. RAG Payload & Classification

When converted into a `RagVectorPayload` entry, a RESX resource chunk should include:

- `RagContentType = Reference`
- `Kind = "Reference"`
- `SubKind = "Resource"`
- Project / tenant identity (OrgId, ProjectId, DocId) from existing indexing configuration.
- Minimal RESX-specific metadata:
  - `ResourceKey`
  - `Culture`
  - `ResourceSubKind` (as string or numeric code)
  - Optional usage aggregates (`UsageCount`, `IsUiSharedCandidate`).

The **full usage map (symbol-level edges)** is intentionally **not** stored in the vector database. Instead, it lives in the Meta Data Registry as defined by IDX-0052. The RESX chunk may carry light aggregates to help reasoning without bloating the vector payload.

---

## 10. Integration into Normalized Chunk Builder (Step 7)

RESX processing fits into the normalized chunk builder workflow as a dedicated step:

1. Load source file text.
2. Detect asset type / sub-kind.
3. If file is `.resx` → invoke **RESX extraction**.
4. Parse XML and enumerate `<data>` elements.
5. For each `<data>` element, build a `ResxResourceChunk`.
6. Map `ResxResourceChunk` into the normalized chunk representation used for embeddings.
7. Enqueue resulting chunks for vector indexing with `RagContentType = Reference`.

This step does **not** rely on Roslyn and can be implemented as a simple XML reader.

---

## 11. Non-Goals and Future Work

The following are explicitly out of scope for IDX-0051 and will be handled by future DDRs:

- **IDX-0052 – Resource Usage Map (RESX → Code)**
  - How resource keys map to specific symbols (controllers, view-models, pages, views).
  - How this map is stored in the Meta Data Registry.
  - How the reasoning layer can request this information as needed.

- Translation workflows (machine translation, language fallback behaviors).
- Style/lint rules for wording (e.g., enforcing consistent phrasing across modules).

---

## 12. Summary

- IDX-0051 treats each RESX `<data>` element as a **first-class, 1:1 chunk**.
- These chunks are indexed as `Reference` / `Resource` content in the vector database.
- The model captures key, value, comment, culture, and light usage aggregates.
- Detailed usage relationships are delegated to IDX-0052 and the Meta Data Registry.

This DDR makes RESX resources discoverable and usable in RAG workflows without mixing in heavy relational usage data that belongs in a dedicated registry.
