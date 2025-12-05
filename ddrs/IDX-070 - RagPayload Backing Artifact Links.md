# IDX-070 — RagPayload Backing Artifact Links

**ID:** IDX-070  
**Title:** RagPayload Backing Artifact Links  
**Status:** Draft  
**Owner:** Kevin Wolf & Aptix  
**Scope:** Defines how RagPayload references full documents, source slices, and description backing artifacts for finder snippets across the LagoVista ecosystem.

---

## 1. Purpose

This DDR formalizes how RagPayload links finder snippets to their backing artifacts.

For every indexable asset (model, manager, repository, endpoint, DDR section, etc.) we standardize four related artifacts:

1. **Full Raw Document** — the entire source or content file where the asset lives.  
2. **Asset Source Slice** — the portion of the raw document that defines this specific asset (class, interface, method, section, etc.).  
3. **Description Backing Artifact** — human/LLM-friendly descriptive text for the asset.  
4. **Finder Snippet** — the minimal, canonical snippet used for embeddings and vector search.

IDX-070 defines how RagPayload exposes artifacts (1)–(3) as resolvable links, and explicitly separates them from (4), which remains the text used for embeddings.

---

## 2. Context and Relationship

- **IDX-066** covers title/description refinement and catalog flows.  
- **IDX-068** defines how codebase artifacts become Finder Snippets and Backing Artifacts.  
- **IDX-070** focuses narrowly on the **linking contract**: which RagPayload fields represent which backing artifacts.

This DDR does **not** prescribe how the blob URIs are persisted (full file vs slice vs pointer), only that each URI is a stable handle to a resolvable backing artifact.

---

## 3. Four Artifacts per Asset

For each serious asset that participates in RAG (as defined by IDX-068), we model four related artifacts:

1. **Full Raw Document**  
   - Example: `Domain/Devices/DeviceManager.cs`, `api/UsersController.cs`, `docs/ddr/IDX-068.md`.  
   - Contains everything in the file, including multiple symbols or sections.

2. **Asset Source Slice**  
   - A narrower view limited to this asset, e.g. a single class, interface, endpoint method, or DDR section.  
   - May be further subdivided (e.g. very large classes → method-level slices), but all slices remain logically children of the same asset.

3. **Description Backing Artifact**  
   - Narrative or structured text that describes the asset in a way that is readable by humans and LLMs.  
   - Examples: ManagerDescription, EndpointDescription, ModelDescription, DomainDescription, DDR section summary.

4. **Finder Snippet**  
   - Short, canonical, high-signal text as defined in IDX-068.  
   - Only this text is embedded and used for vector search.

**Key rule:** For new indexing work, assets that warrant a Finder Snippet should have both an **Asset Source Slice** and a **Description Backing Artifact** available, not either/or.

---

## 4. RagPayload Contract

RagPayload is the structured payload stored alongside a Finder Snippet in the index. IDX-070 standardizes three backing artifact fields:

```csharp
public class RagPayload
{
    // Identity / categorization (examples; exact shape defined elsewhere)
    public string Domain { get; set; }
    public string Kind { get; set; }
    public string Artifact { get; set; }
    public string PrimaryEntity { get; set; }

    // (1) Full raw document — entire source or content file
    public string FullDocumentBlobUri { get; set; }

    // (2) Asset source slice — only the portion defining this asset
    public string SourceSliceBlobUri { get; set; }

    // (3) Description backing artifact — narrative or structured description
    public string DescriptionBlobUri { get; set; }
}
```

### 4.1 FullDocumentBlobUri

**FullDocumentBlobUri** MUST:

- Identify the full raw document where the asset lives.  
- Be stable across re-indexing runs for the same file (subject to path/identity rules from other DDRs).  
- Be shared by all assets that originate from the same underlying file.

The content behind `FullDocumentBlobUri` may be:

- The full original file contents, or  
- A faithful, lossless representation (e.g. normalized encoding),

but it must be sufficient to reconstruct all asset slices.

### 4.2 SourceSliceBlobUri

**SourceSliceBlobUri** represents the **asset source slice** backing artifact.

Requirements:

1. The content or pointer behind `SourceSliceBlobUri` MUST resolve to text that represents only this asset and its immediate definition context (for example, a class, interface, or endpoint method).  
2. The slice MUST be derivable from the full document referenced by `FullDocumentBlobUri` (e.g. via line ranges, offsets, or structural parsing).  
3. The slice SHOULD be stable across minor edits where possible, but indexers may regenerate it when files change.

Implementations MAY choose one of the following internal strategies (out of scope for this DDR):

- Store the slice as an independent plaintext blob.  
- Store a lightweight pointer (e.g. doc id + line/offset range) that is resolved at retrieval time.  
- Store a hybrid representation (e.g. serialization with both text and structural metadata).

From the perspective of consumers, `SourceSliceBlobUri` is simply a resolvable URI that yields the source slice for this asset.

### 4.3 DescriptionBlobUri

**DescriptionBlobUri** represents the **description backing artifact** for the asset.

Requirements:

1. The content behind `DescriptionBlobUri` MUST be human/LLM-readable text (plain text, markdown, or structured JSON) that explains the asset at an appropriate abstraction level.  
2. The description SHOULD be the primary source of explanatory context for the asset during RAG retrieval, especially for high-level questions.  
3. The description MAY be hand-authored, LLM-refined, or generated during indexing (e.g. from XML doc comments, attributes, DDRs, or resource files).

Description content SHOULD NOT be used directly for embeddings; instead, the Finder Snippet text defined in IDX-068 remains the embedding text.

---

## 5. Finder Snippet Responsibilities

IDX-068 remains authoritative for Finder Snippets. IDX-070 adds the following clarifications:

1. The Finder Snippet text is **not** loaded from any blob URI; it is stored directly in the index record.  
2. RagPayload’s `FullDocumentBlobUri`, `SourceSliceBlobUri`, and `DescriptionBlobUri` are used **after** a snippet has been retrieved to pull in detailed context.  
3. Retrieval flows SHOULD:
   - Start with the Finder Snippet to choose relevant assets.  
   - Then load the Description backing artifact for narrative/explanatory context.  
   - Optionally load the Source slice for implementation or low-level reasoning.

---

## 6. Backward Compatibility

- The existing `SnippetBlobUri` field in RagPayload is **deprecated** by this DDR.  
- New implementations MUST NOT write or rely on `SnippetBlobUri`.  
- Existing index content MAY be migrated by:
  - Mapping legacy snippet blobs into `DescriptionBlobUri` when they are descriptive, or  
  - Ignoring legacy snippet blobs when the Finder Snippet is already present in the index record.

Because current systems do not depend on `SnippetBlobUri` for live behavior, IDX-070 may be adopted without a complex migration.

---

## 7. Non Goals

IDX-070 intentionally does **not** specify:

- How blob URIs are persisted (e.g. Azure Blob vs file system vs other stores).  
- Whether the backing artifacts are stored as full documents, normalized text, or pointer encodings.  
- Naming conventions for blob containers or folders.  
- Versioning and retention policies for blobs.

Those concerns are left to storage and infrastructure DDRs.

IDX-070’s responsibility is to define **what each field means** and how it should be used by indexing and RAG orchestration.

---

## 8. Status and Next Steps

**Status:** Draft (pending approval under SYS-001 workflow)

**Next Steps:**

1. Update RagPayload model to:
   - Retain `FullDocumentBlobUri`.
   - Remove `SnippetBlobUri`.
   - Add `SourceSliceBlobUri` and `DescriptionBlobUri`.

2. Update indexing pipelines (IDX-066/IDX-068 implementations) so that:
   - Each new Finder Snippet populates all three backing fields where applicable.  
   - Assets that cannot yet supply a description or slice are logged for follow-up.

3. Update RAG retrieval flows to:
   - Prefer Description backing artifacts as the first augmentation after snippet retrieval.  
   - Use Source slices when implementation-level reasoning or code inspection is required.

4. Once validated on representative domains, update this DDR’s status to **Approved**, record approval metadata, and treat IDX-070 as the canonical contract for RagPayload backing artifact links.
