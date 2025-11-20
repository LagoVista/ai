### IDX-001  
**Title:** DocId Generation Strategy  
**Status:** Accepted  

**Description:**  
The **DocId** is the stable identifier assigned to a *document* (i.e., a source file or artifact) within the indexing system. Each document may be split into multiple chunks, but **all of those chunks share the same DocId**. The DocId ensures consistent grouping, retrieval, and tracking across ingestion runs.

**Decision:**  
- Use a deterministic GUID v5 (UUID version 5) generated from a canonical string composed of `<RepoUrl>|<NormalizedPath>`.  
  - Normalize `RepoUrl` (trim whitespace, lowercase, remove trailing slash).  
  - Normalize `Path` (forward slashes, collapse duplicate slashes, lowercase).  
  - Canonical input string = `repoNormalized + "|" + pathNormalized`.  
- Compute `DocId = UUIDv5(NamespaceCodeFiles, canonicalString)`.  
- Store `DocId` as a **string** in the metadata contract.  
  - Format: 32 uppercase hexadecimal characters, no braces `{}`, no hyphens `-`. Example: `A1B2C3D4E5F67890ABCDEF1234567890`.  
- Guarantee global uniqueness across all indexed content (all projects/repos/orgs).  
- If the canonical string changes (e.g., file moved/renamed), treat it as a *new* DocId (no aliasing built into `DocId`).  
- Keep a single fixed namespace GUID (`NamespaceCodeFiles`) for now; only change it if/when canonicalization rules change and you intentionally form a “V2” migration.

**Rationale:**  
- Provides stable, deterministic identifiers without requiring centralized state or database counters.  
- GUID v5 is well-supported, deterministic, and aligns with existing code (`CodeDocId`).  
- Storing as string simplifies serialization, interop, and flexibility for future ID formats.  
- Excluding aliasing keeps contract clean and deterministic; if historical linkage is needed later, it can be handled outside `DocId`.  
- Ensures global uniqueness, avoiding collisions and simplifying tooling across all content.

**Resolved Questions:**  
1. **Include `OrgId` in canonical string?** → No (excluded).  
2. **Store `DocId` as string or Guid?** → String, formatted uppercase, no hyphens.  
3. **Namespace GUID versioning?** → Single fixed namespace for now.  
4. **Uniqueness scope?** → Global uniqueness across content.  
5. **File renamed/moved → new DocId or alias?** → New DocId; aliasing handled externally if needed.


### IDX-002  
**Title:** PointId Generation Strategy  
**Status:** Accepted  

**Description:**  
The **PointId** is the unique identifier assigned to each vector chunk in the vector database. It allows each chunk to be upserted, retrieved, updated, or deleted independently.

**Decision:**  
- Generate the PointId as a **GUID** (UUID v4 or v5) in string form.  
- Store the PointId as a **string** in the metadata contract.  
- Ensure the GUID is globally unique across all collections, docs and chunks.  
- Do *not* use custom slug formats or embed document/section info directly into the PointId; keep it simple GUID only, which aligns with the technical requirements of Qdrant that accept UUIDs for point IDs. :contentReference[oaicite:1]{index=1}  

**Rationale:**  
- Simplifies generation and avoids complications with slug formatting.  
- Matches Qdrant’s constraint that the `id` must be either an integer or UUID string. :contentReference[oaicite:2]{index=2}  
- Keeps metadata clean and focused: human-readable linking can still be done via other metadata fields (DocId, SectionKey, PartIndex).  
- Minimizes risk of invalid IDs or collisions.

---

### IDX-003  
**Title:** Canonical Path & BlobUri Normalization Rules  
**Status:** Accepted  

**Description:**  
This decision defines how we normalize file paths and blob URIs for consistency, deduplication, and deterministic ID generation. Both `Path` (the normalized path metadata) and `BlobUri` (the storage reference) must follow strict rules so that content is reliably identified across ingestion runs, repositories, and storage systems.

**Decision:**  
- The first segment of `Path` is the unique **project identifier** provided when setting up the repo (e.g., `nuvos`, `ai.machinelearning`, `co.core`).  
- Use forward slashes (`/`) only; convert any backslashes (`\`) into `/`.  
- Lowercase the entire path string.  
- Collapse repeated slashes (`//` → `/`).  
- Trim leading/trailing slashes, but ensure `Path` begins with a single `/`. Example: `/projectId/libs/primitives/src/lib/button/button.component.ts`.  
- `BlobUri` mirrors this normalized `Path` relative to the storage root, with the same conventions.  
- Do *not* enforce reserved prefix folders (like `/libs`, `/apps`, `/docs`); classification is handled via metadata (Kind, Domain, Layer, Role).  
- If a file is moved or renamed (path change), treat it as a new canonical `Path` (and yields a new `DocId`).  
- Do *not* enforce a fixed maximum path length at this stage (given Azure Blob limits of up to 1,024 characters). :contentReference[oaicite:0]{index=0}

**Rationale:**  
- Ensuring stable, normalized paths prevents variation in separators, case, or segment structure from creating multiple identities for the same file.  
- Using the project identifier as top segment provides path portability across repos and storage setups.  
- Avoiding prefix enforcement keeps the system flexible for diverse repository structures.  
- Opting not to impose path length limits now leverages the large practical limits of your storage system, while keeping the contract simpler.

**Resolved Questions:**  
1. **Strip file extensions for Title/ComponentName?** → No, keep extension.  
2. **Enforce reserved path prefixes?** → No.  
3. **Enforce maximum path length?** → No, defer until needed.  
4. **If file moved/renamed, preserve old path segment?** → No, treat as new path.

### IDX-004  
**Title:** Kind & SubKind Classification Rules  
**Status:** Accepted  

**Description:**  
The `Kind` field classifies each document/chunk at a broad category level (e.g., `Code`, `Specs`, `DomainObjects`, `Documentation`, `Metadata`).  
The `SubKind` field provides a more specific classification under its `Kind` (e.g., if `Kind = Code`, then `SubKind` might be `PrimitiveComponent`, `CompositeComponent`, `Service`, `Model`, `Repository`, etc.).  
Both fields are mandatory for each processed chunk/document. For now, the values are *free-form strings*, with a naming convention of **PascalCase** (e.g., `PrimitiveComponent`). A glossary of allowed values will be defined later.

**Decision:**  
- Introduce `Kind` (required) and `SubKind` (required) as metadata fields.  
- Both fields use PascalCase naming convention.  
- Values are free form for now; a controlled vocabulary will be defined later.  
- Ingest pipelines must populate both fields based on file type/role.  
- Only one `Kind` and one `SubKind` per item (no multiple values).  

**Rationale:**  
- Two-level classification gives finer granularity without creating an explosion of kinds.  
- `Kind` and `SubKind` improve filtering and retrieval (broad vs specific).  
- PascalCase aligns with .NET naming conventions for public properties. :contentReference[oaicite:0]{index=0}  
- Free-form now gives agility; glossary can be formalized once stable.  

**Resolved Questions:**  
- Should we have Kind + SubKind? → **Yes**.  
- Should `SubKind` be required? → **Yes**, required.  
- Naming convention for both? → **PascalCase**.  
- Controlled vocabulary now? → **No**, free-form for now; glossary to follow.  

### IDX-005  
**Title:** ContentType / ContentTypeId Rules  
**Status:** Accepted  

**Description:**  
The `ContentType` and `ContentTypeId` fields classify each document or chunk at a technical level (e.g., source code vs markdown vs HTML). `ContentTypeId` is the numeric enum and `ContentType` is the human-readable string.

**Decision:**  
- Define `ContentTypeId` as an integer enum (`RagContentType`).  
- Define `ContentType` as the string name of that enum value, using the same naming.  
- Both fields are required for each chunk/document.  
- `ContentTypeId` and `ContentType` reflect higher-level semantic categories (e.g., `SourceCode`) rather than file-extension-specific types.  
- Naming convention for `ContentType` is PascalCase.  
- Allow exactly one content type per document/chunk (no arrays).  
- Do *not* include version modifiers or variants in the `ContentType` string.  
- Maintain the existing set of ids/values (e.g., Unknown = 0, DomainDocument = 1, SourceCode = 2, etc.). Extensions will be added but stable once published.

**Rationale:**  
- Having a numeric ID plus descriptive string supports efficient querying and readability.  
- Semantic categories simplify mapping, avoid explosion of types per file extension, and map well to ingestion logic.  
- PascalCase aligns with C# and .NET naming conventions. :contentReference[oaicite:0]{index=0}  
- Restricting to one content type per item keeps the metadata simpler and avoids ambiguity.

**Resolved Questions:**  
- Q1: Initial set of values – defined via enum.  
- Q2: Should reflect higher-level semantics? → Yes.  
- Q3: Multiple content types allowed? → No.  
- Q4: Versioning/variant in `ContentType` string? → No.  
- Q5: Naming convention for `ContentType`? → PascalCase.  

### IDX-006  
**Title:** Subtype Rules  
**Status:** Accepted  

**Description:**  
The `SubKind` metadata field provides a second level of classification beneath the broad `Kind` category. It indicates more specific structural or behavioral roles of a document or chunk (e.g., if `Kind = Code`, then `SubKind` might be `Manager`, `Repository`, `Model`, `PrimitiveComponent`, etc.).

**Decision:**  
- Introduce `SubKind` as a required metadata field alongside `Kind`.  
- Both `Kind` and `SubKind` use **PascalCase** naming (e.g., `PrimitiveComponent`, `DomainObject`).  
- `SubKind` values are treated as free-form strings for now; a glossary/registry will be defined later.  
- Only a single `SubKind` per item (no arrays).  
- `SubKind` values are *not* required to be globally unique across all `Kind` values (i.e., the same `SubKind` string may appear under different `Kind`).  
- Version indicators or variant suffixes (e.g., `V2`) are *not* allowed in `SubKind` values.  
- No runtime validation enforcement for `Kind` + `SubKind` combinations at this stage; governance will be retrospective.

**Rationale:**  
- The two-level classification adds granularity without creating an explosion of top-level kinds.  
- Requiring `SubKind` avoids metadata items with only broad `Kind` and missing specificity, improving filtering and querying.  
- PascalCase aligns with C# naming conventions for public properties and types. :contentReference[oaicite:0]{index=0}  
- Free-form initially gives agility; once stable, a glossary ensures governance without slowing initial work.

**Resolved Questions:**  
- Q1: Glossary/formalization process → *Deferred*  
- Q2: Versioning in `SubKind` → *No*  
- Q3: Is `SubKind` optional? → *No*, it’s required (though value `"None"` is acceptable)  
- Q4: Are `SubKind` values globally unique? → *No*  
- Q5: Runtime validation of `Kind` + `SubKind`? → *No*

### IDX-007  
**Title:** Domain / Layer / Role Semantics  
**Status:** Accepted  

**Description:**  
The metadata fields `Domain`, `Layer`, and `Role` provide orthogonal classification axes to describe *where* and *how* a document or code artifact fits in the overall system architecture:
- `Domain`: the bounded context or functional area (e.g., `UI`, `Backend`, `Docs`, `Integration`).
- `Layer`: the architectural layer or tier the artifact belongs to (e.g., `Primitives`, `Composites`, `Implementation`, `Infrastructure`).
- `Role`: the responsibility or purpose of the artifact within its Domain and Layer (e.g., `Component`, `Service`, `Style`, `RagMetadata`).

**Decision:**  
- The fields `Domain`, `Layer`, and `Role` are **optional**, but must be populated for **code-type assets**.
- Use **PascalCase** naming convention for all three fields (e.g., `UI`, `Backend`, `Docs`, `Component`, `Service`).
- For now, allow free-form values (no controlled vocabulary), but enforce PascalCase formatting.
- Only a **single Role** value per item (no arrays).
- `Layer` is free-form at this stage (i.e., we do *not* enforce ordering or a fixed set).
- Role values may be reused across Domains (i.e., Role names are not globally unique).
- Ingestion logic to determine these values is deferred; no runtime validation enforced yet.

**Rationale:**  
- Making these fields optional for non-code assets keeps the metadata lighter where full architectural classification isn’t needed.
- PascalCase naming aligns with other metadata conventions and reduces inconsistency in tooling.
- Free-form values give flexibility during initial ingestion; when mature, formal glossary may follow.
- A single Role simplifies classification; reuse across domains supports logical consistency.

**Resolved Questions:**  
1. Should we enforce a controlled vocabulary for Domain/Layer/Role? → *No*, allow free-form for now.  
2. Can multiple Role values apply to a single artifact? → *No*, only one Role.  
3. How will the ingestion engine determine these values? → *Deferred*.  
4. Should `Layer` ordering be ordinal or descriptive? → *No*, keep free-form.  
5. Are Role names globally unique across domains? → *No*, they may be reused.  


### IDX-008  
**Title:** SymbolType Rules  
**Status:** Accepted  

**Description:**  
The `SymbolType` metadata field classifies the nature of the symbol that a chunk represents (for example, whether the chunk corresponds to a file, component, method, class, or other symbol). This aids retrieval and query by identifying semantic roles of content.

**Decision:**  
- `SymbolType` is **optional**, but **required for source-code assets** (when `Kind = Code`).  
- Naming convention: Use **PascalCase** for `SymbolType` values (e.g., `File`, `Component`, `Method`, `Class`).  
- For source-code assets, the top-level (file-level) symbol will always use `SymbolType = File`.  
- Only a single `SymbolType` value per item (no arrays).  
- `SymbolType` values are free-form for now (no controlled vocabulary); specific mappings for non-code assets will be defined later.  
- Defer decision on global uniqueness and runtime validation of `Kind + SymbolType` combinations.

**Rationale:**  
- Making `SymbolType` optional for non-code assets keeps metadata lighter where full semantic classification isn't needed.  
- PascalCase naming aligns with overall metadata field conventions and aids developer clarity.  
- Ensuring file-level chunks use `SymbolType = File` gives a predictable anchor for the document root.  
- Free-form now provides agility; taxonomy governance can follow once stable.

**Resolved Questions:**  
- Q1: Should we treat file-level chunks specially? → **Yes**, `SymbolType = File`.  
- Q2: Should we allow multiple `SymbolType` values per chunk? → **No**, only one.  
- Q3: Are symbol type values globally unique across `Kind` categories? → **Deferred**.  
- Q4: Should we translate chunker’s internal `SymbolType` value into our canonical form? → **No**, minimal mapping but alignment.  
- Q5: For non-code assets, should we pre-define `SymbolType` values now? → **Deferred**.

### IDX-009  
**Title:** ComponentType & ComponentName Rules  
**Status:** Preliminary  

**Description:**  
The `ComponentType` and `ComponentName` metadata fields provide specialized classification for component-oriented artifacts in the index.  
- `ComponentType` describes the classification of the component (e.g., *primitive*, *composite*, or *other*).  
- `ComponentName` identifies the formal name of the component (e.g., `SliderComponent`, `Button`, `GraphRenderer`).  
These fields enable fine-grained filtering, retrieval, and UI surfacing of component-centric code artifacts.

**How this differs from `Symbol` / `SymbolType`:**  
- `SymbolType` describes the kind of programming construct or symbol the chunk represents (e.g., `File`, `Class`, `Method`, `Component`). Its purpose is structural and language-centric.  
- `ComponentType` / `ComponentName` apply only when the artifact is a *component* (in the UI/library sense). These fields provide semantic classification within that subset—distinguishing between *primitive* vs *composite* components and identifying the specific component name.  
- Example: If a chunk corresponds to a UI component:  
  - `SymbolType = ComponentFile` (or `Component`)  
  - `ComponentType = primitive` or `composite`  
  - `ComponentName = SliderComponent`  
- If the fragment is not a component (e.g., a service or model):  
  - `SymbolType = Service` or `Class`  
  - `ComponentType` and `ComponentName` = `null` or omitted  

**Decision (Preliminary):**  
- Introduce `ComponentType` (string) with allowed values:  
  - `primitive` — a basic building-block component  
  - `composite` — a component built from primitives or other composites  
  - `other` — any component that doesn’t fit primitive/composite taxonomy  
- `ComponentType` uses **lowercase** values as shown (`primitive`, `composite`, `other`).  
- Introduce `ComponentName` (string) — the PascalCase name of the component symbol (e.g., `SliderComponent`, `Button`).  
- Both fields are **populated only when `Kind` indicates the artifact is a component** (e.g., `Kind = primitiveComponent` or `Kind = compositeComponent`). For non-component artifacts, these fields may be `null`.  
- If the ingestion tool cannot determine a meaningful `ComponentType`, default to `"other"`.  
- `ComponentName` should match the actual symbol name in code as closely as possible to maintain traceability.

**Rationale:**  
- Distinguishing primitive vs composite components allows richer filtering (e.g., show only primitives for UI building).  
- Aligning `ComponentName` with the actual code symbol name improves traceability, linking, and tooling support.  
- Restricting `ComponentType` values reduces uncontrolled proliferation of categories and supports consistent querying.  
- Keeping the fields optional for non-component artifacts keeps metadata streamlined for other types.

**Resolved Questions:**  
1. Should we extend `ComponentType` taxonomy beyond the three values (primitive, composite, other)? → No, initial set only.  
2. For composite components that themselves contain primitives, should `ComponentType = composite` or a more layered classification? → Use `composite`.  
3. Should `ComponentName` include namespace or path prefix (e.g., `libs.primitives.SliderComponent`)? → No, just the symbol name in PascalCase.  
4. If a component is platform-agnostic (e.g., used in both UI and backend), should we tag via another field rather than `ComponentType`? → Platform variation is handled via other metadata (e.g., `Domain`/`Layer`), not `ComponentType`.  
5. For non-component `Kind` values (e.g., service, model), should `ComponentType`/`ComponentName` be omitted entirely or allowed but optional? → Omitted (null) for non-component artifacts.

### IDX-010  
**Title:** LabelSlugs & LabelIds Semantics  
**Status:** Accepted  

**Description:**  
The metadata fields `LabelSlugs` (an array of strings) and `LabelIds` (an array of strings) provide tagging and categorization metadata for each chunk/document. These fields enable faceted filtering, boolean logic queries, and human-readable classification of content via labels. `LabelSlugs` are human-readable tags (e.g., `["primitive","button","ui-component"]`), and `LabelIds` are stable internal identifiers (e.g., `["LBL001","LBL002"]`) corresponding to those slugs.  

**Decision:**  
- `LabelSlugs` is required (non-null) but may be an empty list if no labels apply.  
- `LabelIds` is required (non-null) but may be an empty list if no labels apply.  
- Naming convention for `LabelSlugs`: lowercase hyphen-separated words (e.g., `ui-component`, `backend-service`).  
- `LabelIds` format: uppercase alphanumeric string codes with a prefix (e.g., `LBL001`, `LG002`).  
- Both lists must maintain alignment (same index corresponds between slug and ID).  
- The ingestion pipeline populates `LabelSlugs` and `LabelIds` based on project-specific rules/taxonomy.  
- The lists preserve order (though queries treat them as sets).  
- For now, allow free-form tags in both fields (no enforced controlled vocabulary).  
- Avoid duplicates inside each list for a given chunk/document.  
- Do *not* allow hierarchical slugs (e.g., `ui/component/button`); only flat tags.  
- No fixed limit on number of labels per item at this time.  
- `LabelIds` are scoped per organization/project, not required to be globally unique across orgs.

**Rationale:**  
- Tagging supports flexible metadata dimensions beyond structural classification, enabling richer search & filtering.  
- Use of lowercase hyphen-separated slugs improves uniformity and reduces variation in queries.  
- Stable label IDs support backend tooling, roll-ups, and linkage even when slug text may evolve.  
- Requiring both fields ensures consistency and simplifies tooling logic.  
- Flat tags simplify ingestion and avoid hierarchical complexity initially.

**Resolved Questions:**  
1. Should we restrict `LabelSlugs` to a controlled vocabulary now? → *No*.  
2. Do we need to specify a maximum number of labels per chunk/document? → *No*.  
3. Should `LabelIds` be globally unique across all projects/orgs? → *No*.  
4. Should `LabelSlugs` allow hierarchical values? → *No*, only flat tags.  
5. Should metadata track label application timestamp or version? → *No*.

### IDX-011  
**Title:** Priority System  
**Status:** Accepted  

**Description:**  
The `Priority` metadata field denotes the relative importance of each chunk/document in retrieval, ranking, or downstream workflows. Lower numeric values correspond to higher importance (i.e., “1” means highest priority).

**Decision:**  
- Use an integer range of **1–10** for `Priority`.  
- Allow different asset kinds (e.g., code, docs) to apply tailored heuristics within this range.  
- Values are strictly integers (no decimals/fractions).  
- `Priority` values are **adjustable over time**, allowing dynamic recomputation (e.g., for trending or updated content).  
- The exact meaning of each level (1-10) will be documented in the project guidelines; not fully defined now.

**Rationale:**  
- A 1–10 integer scale offers granular prioritization without excessive complexity.  
- Different asset kinds sometimes have distinct importance criteria, so flexible heuristics are beneficial.  
- Restricting to integers simplifies ranking logic and avoids floating-point ambiguity.  
- Allowing dynamic updates accommodates evolving content relevance rather than treating priority as immutable.

**Resolved Questions:**  
1. What integer range? → 1–10.  
2. Should each level be explicitly defined now? → Deferred / defined later.  
3. Should different asset kinds have separate scales? → Yes, allowed.  
4. Should fractional values be allowed? → No, integers only.  
5. Should priority be immutable? → No, adjustable over time.  

### IDX-012  
**Title:** JSON Field Naming Convention  
**Status:** Accepted  

**Description:**  
Defines the naming style and field-presence rules for JSON payloads in the vector database metadata contract. Ensures consistency across ingestion, storage, tooling, and client systems.

**Decision:**  
- JSON property names will use **PascalCase** exactly matching the corresponding C# property names (e.g., `OrgId`, `ProjectId`, `DocId`, `ContentTypeId`, `ContentType`, `IsRagMetadata`).  
- No exceptions: all keys must use PascalCase (matching the C# class), not camelCase, snake_case, kebab-case, or mixed.  
- Fields whose values are `null` will be **omitted entirely** (i.e., the property is not serialized when the value is null).  
- Serialize only present properties; clients and tooling must handle missing keys as equivalent to null.  
- External systems requiring different casing or naming for display should handle transformation at the client layer; the storage schema remains PascalCase.

**Rationale:**  
- Matching C# property names exactly simplifies serialization/deserialization and reduces mapping complexity for .NET-centric tooling.  
- Enforcing a single consistent naming convention (PascalCase) avoids confusion and reduces the chance of metadata inconsistencies.  
- Omitting null-valued fields reduces payload size and clearly signals non-applicability of a property rather than “unknown” or “empty” value. :contentReference[oaicite:0]{index=0}  
- Since many metadata fields are optional, omitting nulls leads to leaner JSON and fewer boilerplate keys.

**Resolved Questions:**  
1. Should we allow aliasing of key names for backward compatibility? → *No*.  
2. Should null-valued properties be omitted or included with `"PropertyName": null`? → *Omit properties entirely*.  
3. Will clients/tools external to .NET expect different casing (e.g., camelCase)? → *Handled at client layer; storage schema remains PascalCase*.  

### IDX-013  
**Title:** UpdatedUtc Logic  
**Status:** Accepted  

**Description:**  
The `UpdatedUtc` metadata field (which would represent when the underlying source content was last modified) is **omitted** from the indexing contract. Since indexing will only happen when content changes, tracking a separate “last‐modified” timestamp is deemed unnecessary at this time.

**Decision:**  
- Remove the `UpdatedUtc` field from the metadata contract.  
- The ingestion/indexing workflow will only index or re‐index when “required” — i.e., when a content change is detected.  
- As a result, there is no need for a separate timestamp to indicate staleness or modification beyond the `IndexedUtc`.  
- The field is retained here as a design placeholder if future requirements change; otherwise, it will not be used.

**Rationale:**  
- Simplifies the metadata model by eliminating a field that does not add value under current indexing practices.  
- Avoids unnecessary tracking of timestamps when the system already controls when re‐indexing occurs.  
- Keeps the payload lean and avoids storing unused or redundant fields.

**Notes:**  
We keep this decision in the contract for possible future reconsideration. If ingestion logic evolves to support incremental updates, change detection, or version auditing, the `UpdatedUtc` field can be introduced at that time.

### IDX-014  
**Title:** Token Field Definitions  
**Status:** Accepted  

**Description:**  
The fields `EstimatedTokens`, `ChunkSizeTokens`, and `OverlapTokens` capture key metrics about how source text is broken into chunks for embedding and indexing.  
- `EstimatedTokens`: Estimate of the number of tokens in the chunk before embedding.  
- `ChunkSizeTokens`: (Optional) The actual token count of the chunk at embedding time.  
- `OverlapTokens`: (Optional) The number of tokens shared with the previous chunk in a sliding-window context.  

**Decision:**  
- `EstimatedTokens` is **required** for every chunk/document and must be a positive integer.  
- `ChunkSizeTokens` and `OverlapTokens` are **optional** (nullable) since not all chunkers compute them.  
- Use **PascalCase** naming: `EstimatedTokens`, `ChunkSizeTokens`, `OverlapTokens`.  
- Populate `EstimatedTokens` using the chunker’s heuristic; if actual token count is known, populate `ChunkSizeTokens`.  
- Slide-window overlap logic can populate `OverlapTokens`; if not used, leave null.  
- Downstream analytics/monitoring use these fields for diagnostics, cost estimation, and chunk-strategy tuning.  

**Rationale:**  
- Having an estimate benchmark enables analytics on chunking strategy performance and embedding cost. :contentReference[oaicite:0]{index=0}  
- Requiring `EstimatedTokens` ensures every chunk reports at least size approx, which aids comparability.  
- Optional fields allow richer metrics when available without forcing all chunkers to compute them.  
- PascalCase naming stays consistent with other metadata field conventions.  

**Resolved Questions:**  
1. Should we enforce a maximum token threshold for `ChunkSizeTokens`? → *Deferred*.  
2. Should `OverlapTokens` always be smaller than `ChunkSizeTokens` and validated? → *No enforcement now*.  
3. If a chunker splits by lines rather than tokens, should `EstimatedTokens` be null? → *No*, still compute a reasonable estimate.  
4. Should we enforce rounding/truncation of token counts? → *No specification now*.  
5. Should we track token budget or model usage separately (input vs output)? → *No, may revisit later*.  

### IDX-015  
**Title:** SourceSha256 Rules  
**Status:** Deprecated  

**Description:**  
Previously the `SourceSha256` field stored a SHA-256 hash of the entire document’s normalized content for change detection. Under the updated indexing strategy, this document-level hash is no longer required and is removed from the metadata contract.

**Decision:**  
- The `SourceSha256` field is **removed** from the contract and will not be used for new ingestion runs.  
- Change detection will instead rely on `ContentHash` at the chunk (or file-level chunk) granularity.  
- Existing indexed data containing `SourceSha256` may remain for archival or audit purposes but the ingestion engine will ignore it for change-detection logic going forward.  
- The contract must be updated to reflect absence of `SourceSha256`, and any tooling / serialization logic must not expect it.

**Rationale:**  
- Given the current “re-index entire file on any change” strategy, the document-level hash adds redundancy without additional benefit.  
- Simplifying the contract reduces metadata fields and avoids maintaining dual hashes for document and chunks.  
- Focusing on `ContentHash` provides sufficient change-detection granularity and aligns with ingestion workflow.  
- Removing `SourceSha256` avoids confusion and ensures the contract remains lean and easy to maintain.

**Notes:**  
Should future versions of the ingestion process adopt incremental chunk-level updates, the `SourceSha256` field could be re-introduced if needed. For now it is deprecated and excluded from new data.


### IDX-016  
**Title:** ContentHash Rules  
**Status:** Accepted  

**Description:**  
The `ContentHash` field stores a hash value representing the normalized text content of a chunk (or file). This fingerprint is used for detecting changes at chunk-level and determining when re-indexing is needed.

**Decision:**  
- `ContentHash` is **required** for each chunk (or file-level object when treated as a single chunk).  
- Compute the hash using the chunk’s full normalized text (normalize line‐endings to `\n`, apply repository/ingestion normalisation steps such as trimming) **after** chunking (or for a full‐file chunk).  
- Use the **SHA-256** algorithm producing a 64-hex lowercase string.  
- Store the resulting hash in `ContentHash`. No separate `SourceSha256` field will be used.  
- At ingestion time, before embedding or indexing: retrieve the prior `ContentHash` (if available). If the newly computed `ContentHash` differs from the stored value (or new chunks appear/disappear), then treat the chunk/file as changed and trigger re-indexing of all relevant points.  
- The chunking process must remain consistent (splitting logic, normalization) so that unchanged text produces identical hashes across runs.  
- If chunking logic changes significantly (e.g., different overlap, token budget, splitting rules), ingestion should treat the change as a full re-index-trigger, because prior hashes may no longer align.

**Rationale:**  
- Having a single reliable hash (`ContentHash`) simplifies change detection and indexing workflows by focusing on normalized text content.  
- Using SHA-256 ensures collision resistance and repeatability across platforms. :contentReference[oaicite:0]{index=0}  
- Avoiding a separate document-level hash (`SourceSha256`) reduces redundancy and aligns with the current strategy of re-indexing a file entirely on any content change.  
- Normalizing line endings ensures deterministic hashing across different OS/platform sources.

**Resolved Questions:**  
1. Should the hash algorithm always be SHA-256? → *Yes*.  
2. If chunking/normalization changes, how handle old hashes? → *Full re-index logic triggered.*  
3. Should chunk identity mapping be impacted when PartIndex changes? → *Chunking logic must be stable; if not, treat as changed set.*  
4. Should embedding vector or metadata be included in the hash? → *No; only normalized text content.*  
5. Should we record a timestamp for when `ContentHash` is computed? → *No; outside scope for now.*

**Notes:**  
This design assumes the ingestion pipeline will reliably compute and compare `ContentHash` values for change detection and skip outdated embeddings. If in future the strategy evolves to preserve unchanged chunks selectively, the contract can be extended (e.g., record old chunk IDs, versioning).  

### IDX-017  
**Title:** BlobVersionId Rules  
**Status:** Accepted  

**Description:**  
The `BlobVersionId` field was originally intended to store the version identifier of the underlying blob/file at the time of indexing. Under the current ingestion strategy—where we keep only fresh indexes in the vector database and maintain a 1:1 mapping with the latest blob content—version tracking is not required. Hence, `BlobVersionId` will remain optional and unused for version-control logic.

**Decision:**  
- `BlobVersionId` will be **optional** and may remain `null`.  
- We will **not** rely on `BlobVersionId` for change detection or indexing triggers.  
- If a storage system returns a version ID and it is captured, we may store it in the metadata payload for informational/audit purposes, but ingestion logic will ignore it.  
- The ingestion/indexer assumes the newest blob content is current; any changes are detected via content-hashing workflows not via version ID.  
- No legacy reliance on `BlobVersionId` needed—the contract and tooling should not require it.

**Rationale:**  
- Since the workflow is simpler (always index fresh content), tracking storage-version identifiers adds unnecessary complexity.  
- Removing the dependency on versioning decouples blob storage features (e.g., Azure versioning) from indexing logic, simplifying ingestion.  
- Keeps the metadata model lean by not enforcing fields that carry no functional value under the current strategy.  
- If future requirements evolve (e.g., support for incremental updates, historical versions, rollback), the contract can be extended to re-introduce version semantics.

**Resolved Questions:**  
1. Should `BlobVersionId` be required in supported storage systems? → *No*.  
2. Should we store a timestamp of the version alongside `BlobVersionId`? → *No*.  
3. How do we handle storage systems that disable versioning (after previously enabled)? → *Not applicable for change detection.*  
4. Should `BlobVersionId` be used in `DocId`/`PointId` generation? → *No*.  
5. If a blob is replaced without versioning, should we treat it specially? → *No — we rely on content-hash logic instead.*

### IDX-018  
**Title:** PDF / HTML Mapping  
**Status:** Proposed  

**Description:**  
The fields `PdfPages` and `HtmlAnchor` support mappings from content chunks to their location in non-plain-text source artifacts (specifically PDF documents or rendered HTML). This allows linking a vector chunk back to a specific page number(s) in a PDF or a fragment/anchor in HTML, enabling richer user navigation and traceability of search results.

**Decision:**  
- Include `PdfPages` (int[]; 1-based page numbers) and `HtmlAnchor` (string fragment identifier) as **optional** metadata in the contract.  
- `PdfPages` should list one or more page numbers that the chunk text corresponds to. If spanning multiple pages, list them or provide a contiguous range.  
- `HtmlAnchor` should hold the fragment/anchor string (e.g., `#section-3-overview`) in the rendered HTML that matches the chunk.  
- For content types where PDF/HTML mapping is not relevant (e.g., source code, plain Markdown), these fields remain null.  
- If both PDF and HTML formats exist for a document, populate whichever mapping is available; optionally both if supported.  
- The ingestion pipeline may attempt to extract mapping data during processing; failure to extract should *not* block indexing — fields remain null.  

**Rationale:**  
- Many documents (user guides, specs, white papers) come in PDF or HTML formats; mapping vector chunks to page numbers or anchors enhances navigation from results to source context.  
- Page/anchor mapping supports better user experience in retrieval applications (e.g., “See page 45 of this document”).  
- Making fields optional preserves contract simplicity for asset types where it’s irrelevant.  
- Aligns with metadata best practice: optional navigation pointers complement core content metadata. :contentReference[oaicite:0]{index=0}  

**Resolved Questions:**  
1. Should we enforce that at least one of `PdfPages` or `HtmlAnchor` is provided for PDF/HTML assets? → *Deferred.*  
2. Should `PdfPages` support compact ranges (e.g., “12-15”) vs list of individual numbers? → *Deferred.*  
3. Should `HtmlAnchor` include full URL/fragment or only fragment? → *Deferred.*  
4. If both PDF and HTML formats exist, which mapping do we prioritise? → *Deferred.*  
5. Should we link to both page and anchor when available, or only one? → *Deferred.*  

1. ### IDX-019  
**Title:** PartIndex / PartTotal Guarantees  
**Status:** Accepted  

**Description:**  
The metadata fields `PartIndex` and `PartTotal` represent the position of a chunk within a document (or file) and the total number of chunks derived from that document, respectively. These fields provide ordering and completeness guarantees for chunk sets in the indexing pipeline.

**Decision:**  
- `PartIndex` and `PartTotal` are **required** for every chunk.  
- `PartIndex` uses 1-based numbering (i.e., the first chunk has `PartIndex = 1`).  
- `PartTotal` equals the total number of chunks produced for the document in the ingestion run, and is calculated *after* the chunks are identified.  
- Guarantee that for each chunk: `1 ≤ PartIndex ≤ PartTotal`.  
- All chunks derived for a given document in that ingestion run share the same `PartTotal` value.  
- Chunk consumers (UI, analytics, tooling) may rely on ordering via `PartIndex` and completeness via `PartTotal`.  
- Simplified system: no enforcement of maximum limits, no chunk run ID, no optional ordering—system remains simple and deterministic.

**Rationale:**  
Providing explicit ordering and completeness metadata simplifies downstream tooling and enhances clarity (e.g., “chunk 3 of 12”). The simplicity of always calculating `PartTotal` and using 1-based indexing aligns with deterministic ingestion workflows and avoids ambiguity.

### IDX-020  
**Title:** LineStart / LineEnd Expectations  
**Status:** Accepted  

**Description:**  
The metadata fields `LineStart` and `LineEnd` indicate the 1-based inclusive line number range within the source document that a given chunk covers. They enable traceability from the chunk back to the exact source location.

**Decision:**  
- For all **text-based** chunks, `LineStart` and `LineEnd` are **required** integer fields.  
- `LineStart` must be ≥ 1.  
- `LineEnd` must be ≥ LineStart.  
- `LineStart` and `LineEnd` define an inclusive range: the chunk covers source lines from `LineStart` through `LineEnd`.  
- If the chunker splits mid-line (due to token limits or overlaps), then `LineEnd` may equal `LineStart`.  
- `CharStart` and `CharEnd` (character offsets) are **optional**; if the chunker does not track them, they should remain `null`.  
- No fixed maximum span on number of lines a chunk may cover.  
- Additional rule: If a single source line exceeds **500 characters**, the ingestion logic will truncate that line at 500 characters before chunking to avoid oversized single-line chunks (e.g., containing base-64 blobs).  
- For non-text assets or when line numbering is not meaningful, `LineStart`/`LineEnd` may remain `null`.

**Rationale:**  
- Including line-number boundaries enhances traceability and gives consumers (UI, tooling) something tangible: e.g., “see lines 101-128 of file X”.  
- Making the fields required for text content ensures consistent metadata coverage rather than leaving important location data blank.  
- Allowing `LineEnd = LineStart` addresses cases where splitting mid-line is unavoidable (e.g., huge single lines).  
- The 500-character truncation rule prevents pathological cases (very long blob lines) that could distort token budgets, embedding performance, or chunk counts.  
- Keeping `CharStart`/`CharEnd` optional keeps the metadata model flexible for chunkers that don’t track character offsets.

**Resolved Questions:**  
1. Should `LineStart`/`LineEnd` be required for text-based chunks? → *Yes.*  
2. Should `CharStart`/`CharEnd` be tracked? → *Yes (optional).*  
3. If splitting mid-line, should `LineEnd` point to the same line? → *Yes.*  
4. Should there be a maximum number of lines per chunk? → *No.*  
5. Should `CharStart`/`CharEnd` be null if not tracked? → *Yes.*

**Notes:**  
Ingestion tooling must apply consistent line numbering and truncation logic so that downstream consumers can rely on `LineStart`/`LineEnd`. The truncation rule for very long lines should be documented and tested (especially for code files or embedded blobs).

### IDX-021  
**Title:** CharStart / CharEnd Semantics  
**Status:** Accepted  

**Description:**  
The metadata fields `CharStart` and `CharEnd` represent the 0-based character offset range within the normalized source text that a given chunk covers. They provide fine-grained location pointers that complement `LineStart`/`LineEnd`, enabling tools to precisely locate the chunk text for uses such as snippet extraction or editor linking.

**Decision:**  
- `CharStart` and `CharEnd` are **optional** integer fields and may be `null` if the chunker does not compute exact character offsets.  
- If populated:  
  - `CharStart` must be ≥ 0 and points to the index of the first character of the chunk in the normalized source text.  
  - `CharEnd` must be ≥ `CharStart` and points to the index of the last character of the chunk (inclusive).  
  - Offsets are based on the normalized text used for embedding (after line-ending normalization, trimming, etc.).  
- Splitting mid-line is permitted; in such cases both offsets still reflect the range within the normalized text.  
- If `CharStart`/`CharEnd` are present, downstream consumers may use them for exact snippet extraction; if absent, consumers may fall back to `LineStart`/`LineEnd`.  
- No requirement to compute offsets for all chunk types; text-based chunks are **encouraged** but not mandatory to supply them.

**Rationale:**  
- Character offsets provide higher precision than line numbers alone—beneficial for UI tooling such as highlights or deep linking.  
- Making them optional preserves metadata flexibility for chunkers that cannot (or choose not to) compute exact offsets.  
- Definition of inclusive ranges (start & end) avoids ambiguity around range boundaries.  
- This supports consistent chunk metadata without over-mandating complexity where it may be unnecessary.

**Resolved Questions:**  
1. Must `CharStart`/`CharEnd` be required for all text-based chunks? → *No.*  
2. If the chunker splits mid-character, how should `CharEnd` be defined? → *Inclusive (last character index included).*  
3. Should the substring from `CharStart` to `CharEnd` exactly match `TextNormalized`? → *Yes.*  
4. Should we enforce offset rounding/truncation when preprocessing? → *No.*  
5. Should offsets only be computed when `EstimatedTokens` or `ChunkSizeTokens` exceed a threshold? → *No.*  

**Notes:**  
Ingestion tools must ensure any computed offsets align correctly with the normalized source text to prevent mismatches in snippet extraction or linking. When `CharStart`/`CharEnd` are omitted, consumer tools must rely on `LineStart`/`LineEnd` or other fallback logic.

### IDX-022  
**Title:** How We Store Example Values in Spec  
**Status:** Proposed  

**Description:**  
This decision governs how we include and format example values in our specification documents (Markdown) and JSON-L records for each metadata field in the indexing contract. The goal is to improve clarity, tooling support, and developer comprehension by providing concrete illustrative data.

**Decision:**  
- Example values will be provided **in-line** in the Markdown spec for each metadata field that is non-trivial or may be ambiguous. For example:  
  ```json
  "DocId": "A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6"

we will provide examples from live data once our vector database is populated.

### IDX-023  
**Title:** ExternalId Field Strategy  
**Status:** Accepted  

**Description:**  
The `ExternalId` field stores an optional external locator for the chunk/document, enabling linking to external systems, UI paths or debugging references.

**Decision:**  
- `ExternalId` is optional (nullable).  
- No enforced pattern/format is required; any string may be used if provided.  
- Uniqueness is *not* required across all items.  
- It may be left `null` when no external reference is available; indexing proceeds regardless.  
- Downstream clients may use `ExternalId` for deep-linking or diagnostics but should *not* rely on it for core logic.  
- No versioning or timestamping of `ExternalId` is required when source changes.

**Rationale:**  
Allowing flexible, optional external identifiers supports a wide variety of reference use-cases without complicating ingestion. Keeping this auxiliary ensures core indexing logic remains stable and uncoupled from external link systems.

**Notes:**  
If future versions require mandatory external referencing or versioned linking, this decision may be revisited.

### IDX-024  
**Title:** SubKind Definitions for Server Code Base  
**Status:** Accepted  

**Description:**  
Defines the allowed `SubKind` values and their semantics for server-side C# source files in the indexing pipeline, and how they will be classified and handled.

**Decision:**  
- The following `SubKind` values are defined (PascalCase):  
  - `DomainDescription`  
  - `Model`  
  - `Manager`  
  - `Repository`  
  - `Controller`  
  - `Service`  
  - `Interface`  
  - `Other` (fallback)  
- Heuristic detection order: DomainDescription → Model → Manager → Repository → Controller → Service → Interface → Other.  
- Once a file/class matches a higher-priority SubKind, lower-priority checks are not applied.  
- `SymbolType` metadata: “component” for classes, “interface” for interface types.  
- Chunking strategy will vary by SubKind (e.g., specific rules per kind).  
- Additional decisions:  
  - Chunking strategy variation **yes** (we will design different chunk strategies per SubKind).  
  - Mixed-purpose classes should be **flagged** in the index output for manual review.  
  - Automatic re-classification when role changes = **no**; this scenario will not be supported.  
- Unmapped files do **not** trigger manual review unless flagged per the mixed-purpose rule.  
- The SubKind list is **not** extensible/versioned at this time.

**Rationale:**  
A controlled set of `SubKind` values allows consistent classification, filtering, retrieval, and analytics of chunks across the codebase. The priority and fallback logic minimize ambiguity in a legacy codebase with inconsistent naming conventions. Differentiating chunking behaviour by SubKind supports more efficient embeddings and retrieval for different code artifact types.

### IDX-030  
**Title:** Local Indexing & Persisted SubKind Override  
**Status:** Accepted  

**Description:**  
Defines the mechanism by which the ingestion engine maintains a local index of previously processed source files, enabling efficient change detection, stable use of manually corrected `SubKind` values, and control over reindexing behaviour via a `Reindex` directive.

**Decision:**  
- Local index record for each file will include:  
  - `FilePath` (normalized)  
  - `DocId`  
  - `ContentHash`  
  - `SubKind`  
  - `LastIndexedUtc`  
  - `FlagForReview` (boolean)  
  - `Reindex` (optional string: “chunk”, “full”, or null/missing)  
- Ingestion logic:  
  1. Compute `ContentHash`.  
  2. Retrieve index record by `FilePath`.  
     - If record exists **and** `ContentHash` matches **and** `SubKind` matches **and** `Reindex` is null/missing → update `LastIndexedUtc`; skip file in indexing pipeline.  
     - If record exists **and** (`ContentHash` differs **or** `SubKind` differs **or** `Reindex = “chunk”`) → include file in pipeline; use stored `SubKind`; reset `Reindex` to null.  
     - If record does not exist **or** `Reindex = “full”` → include file; run heuristic detection to determine `SubKind`; persist new record; reset `Reindex` to null.  
- Manual override of `SubKind` may set `Reindex = “chunk”` to force re-chunking/embedding/upload while preserving the manually set `SubKind`.  
- Files flagged for review (`FlagForReview = true`) should appear in indexing logs for manual correction.  
- The local index serves as authoritative for `SubKind`; heuristic detection only runs when needed.

**Rationale:**  
- Preserves classification stability by reusing `SubKind` unless changes necessitate update.  
- Efficiently skips unchanged files, reducing redundant chunking/embedding.  
- `Reindex` field gives control over forced re-chunking/embedding without needing to change heuristics.  
- Manual override support ensures mis-classified files can be corrected and re-indexed appropriately.

**Notes:**  
The local index approach is core to the ingestion pipeline’s performance and correctness. Future enhancements may include propagation of changes to related files (e.g., dependent managers when a model changes) and extended metadata in the local index.

### IDX-031  
**Title:** SubKind Heuristics & Detection Rules  
**Status:** Accepted  

**Description:**  
Defines the detection heuristics and rules by which the ingestion engine classifies server-side C# source files into the defined `SubKind` categories (as per IDX-024). The heuristics leverage attributes, base-class inheritance, interface naming patterns, namespace/folder conventions, and file-path metadata. Priority ordering and conflict resolution logic are included.

**Decision:**  
- The following heuristic detection order and criteria apply for each `SubKind` (evaluated top-to-bottom):  
  1. **DomainDescription** — Class is decorated with `[DomainDescriptor]` or `[DomainDescription]` attribute, or contains static properties of type `DomainDescription`, or namespace/folder includes `.Domain` or `.Descriptors`.  
  2. **Model** — Class decorated with `[EntityDescription]`, or inherits (directly or transitively) from `EntityBase`, or resides in a namespace/folder segment containing `.Models`.  
  3. **Manager** — Class implements an interface whose name ends with `Manager`, or inherits from `ManagerBase`, or is in namespace/folder segment `.Managers`.  
  4. **Repository** — Class inherits from one of `DocumentDBRepoBase`, `TableStorageBase`, `CloudFileStorage`, or implements interface whose name ends with `Repository`, or namespace/folder segment `.Repositories`.  
  5. **Controller** — Class inherits from `LagoVistaBaseController` (or equivalent), or namespace/folder segment `.Controllers`, or decorated with `[ApiController]`.  
  6. **Service** — Class implements interface with suffix `Service`, or resides in namespace/folder segment `.Services`, provided it has not matched a higher-priority SubKind.  
  7. **Interface** — File declares an `interface IXXX` and does not match any higher SubKind heuristic.  
  8. **Other** — Fallback for all files not matching any of the above heuristics.  
- Once a `SubKind` is matched, lower-priority heuristics are **not evaluated**.  
- If a file/class matches multiple heuristics at the same priority level, it shall be **flagged for review** and assigned the highest-priority SubKind.  
- The ingestion engine shall apply Roslyn or equivalent syntax/semantic analysis to detect:  
  - Attributes (e.g., `Symbol.GetAttributes()`)  
  - Base-class inheritance (`Symbol.BaseType` or `AllBaseTypes()`)  
  - Interface implementations (`Symbol.AllInterfaces.Any(i => i.Name.EndsWith(...))`)  
  - Namespace segments (`Symbol.ContainingNamespace.ToDisplayString()` or `SyntaxTree.FilePath`)  
- `SubKind` is set to the exact PascalCase value matched (e.g., `"Model"`).  
- `SymbolType` is set as follows:  
  - For classes: `"component"`  
  - For interfaces: `"interface"`  
- Chunking strategy may differ by `SubKind`. For example:  
  - **Model**: chunk at class level or per property if large.  
  - **Manager/Repository/Service**: chunk per public method.  
  - **Controller**: chunk per HTTP action method.  
  - **Interface**: chunk per method signature or entire interface.  
  - **DomainDescription**: chunk per static description property.  
  - **Other**: chunk the entire file as a single chunk.  
- Files classified as `Other` or flagged for review should appear in indexing logs for manual inspection and possible future heuristics refinement.

**Rationale:**  
- Provides a systematic, consistent method for classifying a large and evolving legacy codebase where naming conventions are imperfect.  
- Enables richer metadata filtering, search and analytics based on semantic roles (models vs managers vs controllers).  
- Helps chunking engine optimize embed size and context by adapting strategy per SubKind.  
- Flagging ambiguous files allows incremental refinement of classification heuristics and avoids mis-classification risk.

**Resolved Questions:**  
1. Should we vary embedding/chunking parameters by `SubKind`? → *No*  
2. Are there additional heuristics needed? → *No*  
3. How should the ingestion engine handle nested types or partial classes across files relative to `SubKind` detection? → *No*  
4. Should we permit project-specific custom `SubKind` values beyond the standard list? → *No*  
5. What thresholds determine when a chunking strategy should escalate from class-level to property/method-level? → *Deferred*

**Notes:**  
This heuristics spec supports stable classification via detection rules. As you index your code base, you may refine heuristics to reduce the number of `Other` classifications and minimize flagging over time.

### IDX-033  
**Title:** Meta Data Registry – Facet Upload Contract  
**Status:** Accepted  

---

#### Description  

Defines how the indexer reports **unique metadata facets** (things we can filter on) to a central **Meta Data Registry** service after an indexing run.

This DDR is about the **shape of the data the indexer sends** and the basic rules for how it is produced. How the server stores or uses this information is out of scope for this decision.

Typical examples of facets include:

- `Kind = "SourceCode"`
- `Kind = "SourceCode", SubKind = "Model"`
- `Kind = "SourceCode", SubKind = "Model", ChunkFlavor = "Raw"`

---

#### Decision  

##### 1. Registry Entry Concept  

- The indexer will send a collection of **Meta Registry Entries** to a server endpoint after each indexing run.  
- Each entry represents **one unique combination of facet key/value pairs** discovered during that run for a given org + project.  
- The indexer is responsible for de-duplicating entries **within the current run**;  
  the server is responsible for reconciling against anything it already knows.

##### 2. Core Fields  

Each entry contains:

- `OrgId` – the organization identifier (string, required).  
- `ProjectId` – the project identifier (string, required).  
- `ComponentName` – the logical producer of the entry, e.g. `"ServerCodeIndexer"` or `"NuvOS.DesignSystem.Indexer"` (string, required).  
- `Facets` – an ordered list of key/value pairs (name/value) representing a **facet path** (array, required, length ≥ 1).

Example facet paths:

- Single facet:  
  - `Facets = [ { "Key": "Kind", "Value": "SourceCode" } ]`
- Parent/child relationship:  
  - `Facets = [ { "Key": "Kind", "Value": "SourceCode" }, { "Key": "SubKind", "Value": "Model" } ]`
- Multi-level (Kind + SubKind + ChunkFlavor):  
  - `Facets = [ { "Key": "Kind", "Value": "SourceCode" }, { "Key": "SubKind", "Value": "Model" }, { "Key": "ChunkFlavor", "Value": "Raw" } ]`

> Note: The **parent/child semantics** (e.g., “Kind = SourceCode” is parent of “SubKind = Model”) are implied by the ordered list of facets; the indexer does not need explicit parent/child fields.

##### 3. Uniqueness Rules  

- For a given indexing run, the indexer **must only send unique entries**.  
  - Uniqueness is defined by the tuple: `(OrgId, ProjectId, ComponentName, Facets[])`.  
- The indexer may maintain an in-memory `HashSet` keyed by a normalized representation (e.g. `"OrgId|ProjectId|ComponentName|Kind=SourceCode;SubKind=Model;ChunkFlavor=Raw"`).  
- The server is responsible for any cross-run reconciliation and storage, so the client does **not** need to query existing registry contents.

##### 4. Scope  

- This registry is **indexing-only concern**:  
  - Indexer discovers which facet combinations exist in the current corpus.  
  - Indexer sends them to the registry endpoint.  
  - Indexer does **not** care how they are presented or used later (filter UI, analytics, etc.).  
- At this stage we **do not** include additional fields about how facets are used in UI or RAG flows.

##### 5. ComponentName  

- `ComponentName` is included to distinguish which indexer or subsystem produced the facet entry (e.g., server-side indexer vs UI indexer).  
- For now, the indexer will simply set a fixed component name (e.g. `"ServerCodeIndexer"`); future subsystems can define their own values.  
- No behavioral logic is tied to `ComponentName` in the indexer; it is purely descriptive metadata for the registry.

---

#### Example JSON Payload  

A single entry:

```json
{
  "OrgId": "AA2C78499D0140A5A9CE4B7581EF9691",
  "ProjectId": "LagoVista.Server",
  "ComponentName": "ServerCodeIndexer",
  "Facets": [
    { "Key": "Kind", "Value": "SourceCode" },
    { "Key": "SubKind", "Value": "Model" },
    { "Key": "ChunkFlavor", "Value": "Raw" }
  ]
}


# IDX-0034 — Deletion of Stale Chunks
**Status:** Accepted

## Description
Defines when and how vector chunks are considered stale and must be removed from the vector database whenever a document is re-indexed.

---

## Decision

### What counts as a stale chunk
A chunk is considered **stale** if:
- It belongs to a **DocId** being re-indexed in the current run, **and**
- It does **not** appear in the newly generated chunk set for that DocId.

Evaluation is always done **per DocId**, not globally.

---

### Deletion strategy: Replace-All-Per-DocId
When a file is re-indexed:

1. **Delete all existing chunks** in the vector DB where payload.DocId equals that file’s DocId.  
2. **Insert all new chunks**, each with a freshly generated PointId (GUID).  
3. No diffing of individual chunks is ever attempted.

This leaves no orphans and ensures strict determinism.

---

### When deletion happens
Deletion is triggered only when the local index indicates the file must be re-indexed:

- **ContentHash unchanged & no Reindex flag**  
  → File is skipped; no deletion occurs.

- **ContentHash changed or Reindex = 'chunk' or 'full'**  
  → File is re-chunked and all existing chunks for its DocId are deleted and replaced.

Chunk-level ContentHash is **not** considered.

---

### Dry-run behavior
In dry-run mode:
- Report which DocIds *would* be deleted
- Report quantity of chunks affected
- No actual deletions or insertions occur

---

### Non-text assets
Binary/PDF/HTML/etc. follow the **same** DocId-based delete-and-replace rule.

---

### Consistency guarantees
At the completion of a successful run:
- Only **one generation** of chunks exists for each DocId  
- Temporary internal duplication during processing is allowed but never part of the contract  
- PointIds are **not** stable across runs; DocId is the stable identity boundary

---

## Rationale
Using DocId as the ownership boundary for chunks provides a clean and deterministic lifecycle for vector data. When a file is re-indexed, the safest and most predictable strategy is to delete all previous chunks for that DocId and re-insert the new ones. This removes the need for complicated diffing, avoids stale or orphaned chunks, and ensures the vector database accurately reflects the latest state of the file.

The local index (via ContentHash and Reindex flags) determines if a file should be processed; once selected, stale chunk deletion is unconditional and complete.

---

## Resolved Questions

### Do we attempt fine-grained diffing?
No. Any DocId chosen for re-indexing has all prior chunks removed.

### Do we delete chunks for DocIds not processed this run?
No. That is handled separately under IDX-0035.

### Are PointIds stable across runs?
No. They are regenerated every re-index.

### Does chunk-level ContentHash matter?
No. The decision to re-index is always at the file level, not the chunk level.

# IDX-0035 — Deletion of Chunks When Local File No Longer Exists
**Status:** Accepted

## Description
Defines when and how chunks must be removed from the vector database when a file that previously existed in the repository is no longer present in the current filesystem or project structure during an indexing run.

This handles the case where the file has been deleted, renamed, moved outside the indexed scope, or excluded by filters.

---

## Decision

### Detection Rule
A document is considered **orphaned** when:
- Its prior record exists in the *local index*, **and**
- The physical file **does not exist** at the path recorded in the local index during the current run.

This condition holds regardless of:
- Whether the file was deleted
- Renamed
- Moved outside the indexing root
- Ignored due to updated filters  
- Excluded from the current project configuration

---

### Deletion Strategy
When a file is determined to be missing:

1. **Delete all chunks** in the vector database where `payload.DocId == DocId`.
2. **Remove the file’s entry** from the local index.
3. **No new chunks** are produced for this DocId.
4. **PointId values are not reused**; deletion is total.

This is a one-way cleanup operation: only removal, no replacement.

---

### Trigger Conditions
Deletion occurs when:

- The file is not present in the current filesystem state  
**AND**
- The local index contains a previous record for that file

This is independent of ContentHash, SubKind, or Reindex flags.

---

### Filter and Ignore Scenarios
If a file is now excluded due to:
- New ignore rules  
- Path-based exclusions  
- Repo reconfiguration  
- Language or extension filters  

…it is treated exactly the same as a deleted file:
→ **Delete its chunks and remove its local index entry.**

---

### Dry-run behavior
In dry-run mode:
- Report each DocId that would be deleted  
- Report how many chunks would be removed  
- Do **not** modify the vector DB  
- Do **not** modify the local index

---

### Safety and Determinism
The system guarantees:
- No DocId remains in the vector store without a corresponding local file  
- No orphaned chunks persist across runs  
- Local index always mirrors the authoritative filesystem state at run completion

---

## Rationale
Files can disappear for many reasons—actual deletion, renaming, moves, scope changes, or ignore rule updates. Treating all of these as a single “file is missing” condition creates a uniform rule that is easy to reason about.

By removing all chunks tied to the missing file’s DocId and cleaning the local index, we maintain a consistent mapping between:

- **Local filesystem**
- **Local index**
- **Vector database**

This avoids stale data, supports incremental ingestion, and prevents ghost search results for content that no longer exists.

---

## Resolved Questions

### Should we attempt to recover renamed files?
**No.** Renames produce a new canonical path → new DocId (per IDX-001 and IDX-003).  
The old DocId is deleted here; the new DocId is handled as a new document.

### Do we delete chunks for files excluded by updated filters?
**Yes.** Exclusion = non-existence for indexing purposes.

### Should deletion depend on ContentHash or Reindex flags?
**No.** Missing file state overrides all other indicators.

### Should local index entries for missing files remain for historical tracking?
**No.** They are removed; the local index represents only active files.


# IDX-0036 — Local Index File Format & Maintenance Rules
**Status:** Accepted

## Description
Defines the canonical structure, storage location, update rules, lifecycle behavior, and persistence guarantees of the local index file used during ingestion.  
The local index determines:

- Whether a file must be re-indexed  
- Whether a DocId should be deleted  
- Whether SubKind overrides persist  
- How Reindex flags drive behavior  
- Which files are “active” and must be sent directly to the LLM  

The file is local-only; it is never uploaded or synchronized externally.

---

## Decision

### Local Index Storage Format
Stored as a UTF-8 JSON file containing an array of file-record objects:

```json
{
  "FilePath": "string",
  "DocId": "string",
  "ContentHash": "string",
  "ActiveContentHash": "string or null",
  "SubKind": "string or null",
  "LastIndexedUtc": "ISO-8601 timestamp",
  "FlagForReview": "boolean or null",
  "Reindex": "null | \"chunk\" | \"full\""
}
```

- **ContentHash** = last content successfully indexed into the vector DB (normalized SHA-256).  
- **ActiveContentHash** = current on-disk content hash; if different from ContentHash, the file is considered **Active** and must be provided to the LLM.

All keys use PascalCase and omit nulls (IDX-012).

---

## Local Index Location
The file is stored at:

```
<project-root>/.nuvos/index/local-index.json
```

- Folder created automatically  
- File created automatically  
- Always local-only  

---

## Maintenance & Lifecycle Rules

### 1. Startup Behavior
- If the file exists → load it.  
- If corrupt → rename to `local-index.corrupt.json` and start fresh.  
- If missing → start with an empty index.

---

### 2. For Each File Processed
1. Compute `ActiveContentHash` from on-disk content.  
2. Match by canonical `FilePath`.  
3. If entry exists:
   - Update `ActiveContentHash`.
   - Compare `ActiveContentHash` and `ContentHash`:
     - If different → file is **Active** and should be sent to LLM.
   - Apply SubKind override (if present).
   - Honor `Reindex` flag.
4. If entry does not exist:
   - Create a new entry with `ActiveContentHash` and `DocId`.

All updates are persisted **immediately after processing each file** (see Write Frequency).

---

### 3. SubKind Override Persistence
Manual SubKind overrides persist and never get replaced by automatic detection.

---

### 4. Reindex Behavior
- "chunk" → force re-chunk & re-embed  
- "full" → identical behavior for now  
- After successful ingestion → **clear the flag**

---

### 5. Missing File Behavior (IDX-0035)
When a previously indexed file is missing:
- Delete all chunks for its DocId  
- Remove the entry from the local index  
- Do not retain tombstones

---

### 6. After Successful Ingestion
For a file that is successfully indexed:
- Set `ContentHash = ActiveContentHash`  
- Update `LastIndexedUtc`  
- Apply automatic SubKind only if no override exists  
- Clear `Reindex`  
- File is no longer Active

---

### 7. Write Frequency & Atomicity
After **every file** is processed:
1. Update the in-memory index  
2. Sort entries by FilePath  
3. Write to `local-index.json.tmp`  
4. Atomically replace `local-index.json`

Optional final no-op write occurs at end-of-run.

---

## Safety Guarantees
- No entries remain for files that do not exist  
- Canonical paths only  
- `ContentHash` always reflects last vectorized content  
- `ActiveContentHash` always reflects the latest on-disk content  
- Active files identified by hash mismatch  
- SubKind overrides persist  
- Reindex cleared on success  
- Atomic writes ensure crash-safe recovery  

---

## Rationale
Writing after every file guarantees crash-safe incremental progress.  
Separating `ContentHash` (what the vector DB represents) from `ActiveContentHash` (what is currently on disk) allows the LLM workflow to correctly identify which files require direct injection of local content and which can be satisfied via the vector DB.  
The local index remains small, deterministic, and tightly aligned with the actual filesystem.

---

## Resolved Questions

### Should historical entries remain?
No — entries for missing files are removed.

### Does indexing clear active status?
Yes — by setting `ContentHash = ActiveContentHash`.

### Track OS metadata?
No — ContentHash determines re-index.

### Multiple index files?
No — one index per project.

### Should SubKind overrides persist?
Yes — indefinitely until changed.


# IDX-0037 – Model Structure Description (Structured Chunk)

**Status:** Accepted

## Description
Defines the structure, fields, and expected content of the `ChunkFlavor = Structured` representation for `Kind = Model`. This chunk provides the LLM with a high-level structural understanding of an entity: its identity, domain, properties, entity header references, child objects, relationships, and high-level operational affordances (supported actions and URLs).

## Decision

### Purpose
This structured representation answers:
- *What is this model?*
- *How is it composed?*
- *What operations exist around it?*
- *How does it relate to other entities?*

### ChunkFlavor
`ChunkFlavor = "Structured"`

### Field Schema
The structured model description includes:

#### Identity
- `ModelName`
- `Namespace`
- `QualifiedName`
- `Domain`

#### Human Text (resolved from EntityDescription attribute + resource dictionary)
- `Title`
- `Help`
- `Description`

#### Operational Affordances
- `Cloneable`
- `CanImport`
- `CanExport`

**UI URLs**
- `ListUIUrl`, `EditUIUrl`, `CreateUIUrl`, `HelpUrl`

**API URLs**
- `InsertUrl`, `SaveUrl`, `UpdateUrl`, `FactoryUrl`, `GetUrl`, `GetListUrl`, `DeleteUrl`

#### Structural Components
- `Properties[]`
- `EntityHeaderRefs[]`
- `ChildObjects[]`
- `Relationships[]`

### Implementation Note
This chunk is built from:
- `[EntityDescription]` at class level  
- `[FormField]`, `[LabelResource]`, `[HelpResource]`, `[FKeyProperty]` at property level  
- Resource dictionaries for resolving human-readable text  
- Reflection-based extraction and structural mapping pipelines

### Rationale
Provides the LLM with a complete, semantic, structural view of the entity that is ideal for:
- Code generation  
- Documentation  
- Workflow reasoning  
- System understanding  

while leaving detailed UI/validation semantics to IDX-0038.


# IDX-0038 – Model Metadata & UI Description

**Status:** Accepted

## Description
Defines the metadata/UI description chunk for `Kind = Model` with `ChunkFlavor = Metadata`.  
This chunk provides detailed UI, validation, labeling, picker, layout, interaction metadata, and form layout groupings used to render forms, list views, and field-level behaviors.

## Purpose
While IDX-0037 provides structural and operational affordances, IDX-0038 provides the detailed UI contract.  
It answers:  
- How should this field be displayed?  
- What validation applies?  
- What control type is used?  
- What help/label resources define the user-facing text?  
- How do pickers, lists, child collections, and file uploads function?  
- How should fields be grouped into forms, columns, advanced sections, mobile views, and quick-create views?  
- What additional UI actions are available on create/edit forms?

## ChunkFlavor
`ChunkFlavor = "Metadata"`

## Top-Level Fields
- `ModelName`  
- `Namespace`  
- `Domain`  
- `ResourceLibrary`  
- `Fields[]` – array of field metadata objects  
- `Layouts` – object describing how fields are arranged into forms and views (derived from form-descriptor interfaces)  
- Optional model-level UI hints:
  - `Title`  
  - `Help`  
  - `Description`  
  - `ListView`  
  - `FormLayout`  

## Field Schema (Normalized)
Fields derive primarily from FormFieldAttribute, LabelResource, HelpResource, and related attributes.

### Identity & Resources
- `PropertyName`  
- `Label`  
- `LabelResourceKey`  
- `HelpText`  
- `HelpResourceKey`  
- `RequiredMessageResourceKey`  
- `NamespaceUniqueMessageResourceKey`  
- `ResourceLibrary`  

### Data & Field Kind
- `FieldType` (from FieldTypes enum)  
- `CustomFieldType`  
- `DataType`  
- `EnumType`  
- `NamespaceType`  
- `IsReferenceField`  

### Validation
- `IsRequired`  
- `MinLength`  
- `MaxLength`  
- `Regex`  
- `RegexMessageResourceKey`  
- `CompareToProperty`  
- `CompareToMessageResourceKey`  

### UI Behavior & Layout
- `IsUserEditable`  
- `IsMarkDown`  
- `WaterMark`  
- `AllowAddChild`  
- `InPlaceEditing`  
- `OpenByDefault`  
- `Rows`  
- `EditorPath`  
- `Tags`  

### Child / Collection Semantics
- `ChildListDisplayMember`  
- `ChildListDisplayMembers`  
- `ParentRowName`  
- `ParentRowIndex`  
- `PickerProviderFieldName`  

### Picker / Lookup Semantics
- `PickerType`  
- `PickerFor`  
- `EntityHeaderPickerUrl`  
- `LookupFactoryUrl`  
- `LookupGetUrl`  
- `IsLookup`  

### File / Image / Media
- `UploadUrl`  
- `IsFileUploadImage`  
- `ImageUpload`  
- `PrivateFileUpload`  
- `GeneratedImageSize`  
- `DisplayImageSize`  
- `ThumbnailField`  
- `SharedContentKey`  

### Enum / Options Behavior
- `SortEnums`  
- `AddEnumSelect`  
- `CustomCategoryType`  

### AI & Advanced Metadata
- `AiChatPrompt`  
- `ScriptTemplateName`  
- `TagsCSVUrls`  

## Layouts from Form Descriptor Interfaces

Many models implement one or more form-descriptor interfaces that return **lists of property names** indicating how fields should be arranged in various views.  
IDX-0038 captures this in a top-level `Layouts` object.

### Layouts Object Shape

```jsonc
"Layouts": {
  "Form": {
    "Col1Fields": [ "Name", "Key", "Description" ],          // IFormDescriptor.GetFormFields
    "Col2Fields": [ "IsEnabled", "IsPublic" ],               // IFormDescriptorCol2.GetFormFieldsCol2
    "BottomFields": [ "Notes" ],                             // IFormDescriptorBottom.GetFormFieldsBottom
    "TabFields": [ "AdvancedSettings", "Security" ]          // IFormDescriptorTabs.GetFormFieldsTabs
  },
  "Advanced": {
    "Col1Fields": [ "DebugMode" ],                           // IFormDescriptorAdvanced.GetAdvancedFields
    "Col2Fields": [ "TraceLevel" ]                           // IFormDescriptorAdvancedCol2.GetAdvancedFieldsCol2
  },
  "InlineFields": [ "Status", "LastUpdated" ],               // IFormDescriptorInlineFields.GetInlineFields
  "MobileFields": [ "Name", "Status" ],                      // IFormMobileFields.GetMobileFields
  "SimpleFields": [ "Name", "IsEnabled" ],                   // IFormDescriptorSimple.GetSimpleFields
  "QuickCreateFields": [ "Name", "Key" ],                    // IFormDescriptorQuickCreate.GetQuickCreateFields
  "AdditionalActions": [
    {
      "Title": "Test Connection",
      "Icon": "fa-plug",
      "Help": "Runs a connectivity test.",
      "Key": "TestConnection",
      "ForCreate": false,
      "ForEdit": true
    }
  ]
}
```

### Interface Mapping

- `IFormDescriptor` → `Layouts.Form.Col1Fields` (GetFormFields)  
- `IFormDescriptorCol2` → `Layouts.Form.Col2Fields` (GetFormFieldsCol2)  
- `IFormDescriptorBottom` → `Layouts.Form.BottomFields` (GetFormFieldsBottom)  
- `IFormDescriptorTabs` → `Layouts.Form.TabFields` (GetFormFieldsTabs)  

- `IFormDescriptorAdvanced` → `Layouts.Advanced.Col1Fields` (GetAdvancedFields)  
- `IFormDescriptorAdvancedCol2` → `Layouts.Advanced.Col2Fields` (GetAdvancedFieldsCol2)  

- `IFormDescriptorInlineFields` → `Layouts.InlineFields` (GetInlineFields)  
- `IFormMobileFields` → `Layouts.MobileFields` (GetMobileFields)  
- `IFormDescriptorSimple` → `Layouts.SimpleFields` (GetSimpleFields)  
- `IFormDescriptorQuickCreate` → `Layouts.QuickCreateFields` (GetQuickCreateFields)  

- `IFormAdditionalActions` → `Layouts.AdditionalActions` (GetAdditionalActions; serialized `FormAdditionalAction` items)

### FormAdditionalAction Shape

Each `FormAdditionalAction` returned by `GetAdditionalActions()` is serialized as:

- `Title` (string): Display name of the action.  
- `Icon` (string): Icon identifier (e.g., FontAwesome, custom icon set).  
- `Help` (string): Help/tooltip text for the action.  
- `Key` (string): Stable programmatic key used for routing, automation, or LLM reasoning.  
- `ForCreate` (bool): Whether the action is shown on create forms.  
- `ForEdit` (bool): Whether the action is shown on edit forms.

Example:

```jsonc
{
  "Title": "Test Connection",
  "Icon": "fa-plug",
  "Help": "Runs a connectivity test.",
  "Key": "TestConnection",
  "ForCreate": false,
  "ForEdit": true
}
```

### Per-Field Layout Hints (Optional)
Consumers MAY also reflect layout membership back into each field entry via a `Layouts` array, e.g.:

```jsonc
{
  "PropertyName": "Name",
  "Label": "Name",
  "Layouts": [ "Form.Col1", "Mobile", "Simple", "QuickCreate" ]
}
```

This is optional but recommended for LLM consumption; the canonical source of truth remains the `Layouts` object.

## Implementation Note
The `Fields[]` metadata is derived from FormFieldAttribute properties including LabelResource, HelpResource, FieldType, ResourceType, IsRequired, MinLength, MaxLength, ValidationRegEx, CompareTo, NamespaceUniqueMessageResource, UploadUrl, FactoryUrl, GetUrl, EntityHeaderPickerUrl, HelpUrl, InPlaceEditing, EnumType, SortEnums, AddEnumSelect, IsReferenceField, AiChatPrompt, ScriptTemplateName, Rows, ImageUpload, CustomCategoryType, ThumbnailField, Tags, PickerType, PickerFor, PickerProviderFieldName, ParentRowName, ParentRowIndex, and file/media metadata.

The `Layouts` object is derived from the presence and implementations of the following interfaces on the model type:
- `IFormDescriptor`  
- `IFormDescriptorCol2`  
- `IFormDescriptorBottom`  
- `IFormDescriptorTabs`  
- `IFormDescriptorAdvanced`  
- `IFormDescriptorAdvancedCol2`  
- `IFormDescriptorInlineFields`  
- `IFormMobileFields`  
- `IFormDescriptorSimple`  
- `IFormDescriptorQuickCreate`  
- `IFormAdditionalActions`  

Each interface contributes a list of property names (or additional action descriptors) which are mapped into the corresponding `Layouts` collections.

## Rationale
This chunk captures the complete UI/UX contract for each model entity.  
It enables:
- Automatic form generation  
- Validation extraction  
- Picker/lookup configuration  
- Display naming/help text resolution  
- Advanced user experience patterns  
- Mobile, simple, quick-create, and advanced variants  
- Additional actions on create/edit forms  
- Enhanced LLM reasoning for UI code generation and layout decisions

# IDX-0039 – Chunking Strategy for Kind=Manager

**Status:** Accepted

## Description
Defines the deterministic, semantics-aware chunking strategy for Manager classes, including stable chunk ordering, per-method chunks, overflow handling, and **PrimaryEntity** detection. Also defines how we capture Manager–interface relationships for cross-linking from controllers.

Managers orchestrate workflows and business rules for a primary entity, typically delegating persistence to repositories.

## Scope

- `Kind = "SourceCode"`  
- `SubKind = "Manager"`  
- Language: C#  
- Applies to classes detected as Managers via existing heuristics (IDX-031), e.g.:
  - Implements `I*Manager`
  - Inherits `*ManagerBase`
  - Namespace includes `.Managers`

All chunks for a given Manager file share:
- The same `DocId` (IDX-001)  
- Per-chunk `ContentHash` (IDX-016)  
- `PartIndex` / `PartTotal` (IDX-019)  
- `LineStart` / `LineEnd` (IDX-020)  
- Optional `CharStart` / `CharEnd` (IDX-021)  

SymbolType conventions:
- Manager overview: `Class`  
- Manager methods: `Method`  

## ChunkFlavors

Manager code uses three logical chunk flavors:

- `ManagerOverview` — class-level overview  
- `ManagerMethod` — per-method chunks  
- `ManagerMethodOverflow` — continuation chunks for very large methods  

These may be represented as values in a `ChunkFlavor` field or as a combination of fields; the key requirement is that they are distinguishable by metadata.

## ManagerOverview Chunk

Exactly one `ManagerOverview` chunk per Manager class.

**Content (in source order):**
- `using` directives (optional)  
- `namespace` declaration  
- XML doc comments on the Manager class  
- Class-level attributes  
- Class signature (including base types and interfaces)  
- Private field declarations (signatures only)  
- Public/protected properties (signatures only)  
- A method index: list of public methods with their signatures only (no bodies)

**Required metadata:**
- `Kind = "SourceCode"`  
- `SubKind = "Manager"`  
- `SymbolType = "Class"`  
- `ChunkFlavor = "ManagerOverview"`  
- `PrimaryEntity` — the main entity type this Manager operates on (see below)  

**Interface metadata for cross-linking:**
- `ImplementedInterfaces` (string[])  
  - All interfaces implemented by the Manager class, by simple type name (e.g., `["IDeviceManager", "IDisposable"]`).  
- `PrimaryInterface` (string, optional)  
  - The primary DI/contract interface for this Manager, selected using deterministic heuristics:

    1. If the class is `FooManager` and it implements `IFooManager`, set `PrimaryInterface = "IFooManager"`.  
    2. Else, if `PrimaryEntity = Device` and it implements `IDeviceManager`, set `PrimaryInterface = "IDeviceManager"`.  
    3. Else, if there is exactly one implemented interface whose name ends with `Manager`, use that.  
    4. Otherwise, leave `PrimaryInterface` null.

This enables EndpointDescription chunks (IDX-0041) to reference Manager interfaces via:

```jsonc
"Handler": {
  "Interface": "IDeviceManager",
  "Method": "CreateDeviceAsync",
  "Kind": "Manager"
}
```

and be cleanly joined back to the Manager metadata.

## ManagerMethod Chunks

One `ManagerMethod` chunk per significant method.

**Content (contiguous, in source order):**
- XML doc comments for the method (if present)  
- Method-level attributes  
- Full method signature (modifiers, parameters, generic constraints)  
- Full method body `{ ... }`  

**Included methods:**
- All public methods  
- Protected/internal methods that participate in workflows  
- Private methods only when they encapsulate non-trivial business logic

**Metadata:**
- `Kind = "SourceCode"`  
- `SubKind = "Manager"`  
- `SymbolType = "Method"`  
- `ChunkFlavor = "ManagerMethod"`  
- `PrimaryEntity` — same value as in overview  
- Optional `MethodKind` classification (e.g., `Create`, `Update`, `Delete`, `Query`, `Validation`, `Other`).  

## ManagerMethodOverflow Chunks

For methods whose bodies exceed chunk/token limits, a method may be split into multiple chunks:

- Never split across method boundaries; a chunk belongs to exactly one method.  
- When a single method body exceeds thresholds:
  - Split inside the method body at “safe” boundaries (blank lines, regions, comment blocks).  
  - The primary `ManagerMethod` chunk contains the signature and the initial portion of the body.  
  - Additional `ManagerMethodOverflow` chunks contain the remaining body content.

**Metadata:**
- `Kind = "SourceCode"`  
- `SubKind = "Manager"`  
- `SymbolType = "Method"`  
- `ChunkFlavor = "ManagerMethodOverflow"`  
- `OverflowOf = "<MethodName>"`  
- `PrimaryEntity` — same as overview  

## Ordering and PartIndex

For each Manager class (single DocId), chunks MUST be emitted in source order:

1. `ManagerOverview`  
2. `ManagerMethod` chunks in source order  
3. `ManagerMethodOverflow` chunks immediately after their corresponding `ManagerMethod` chunk  

`PartIndex` and `PartTotal` are assigned based on this physical order across **all** chunks for that file.

### Why Ordering Matters

- Keeps `PartIndex` / `PartTotal` stable across runs for unchanged files.  
- Avoids unnecessary reembedding when only trivial changes occur or when nothing changes.  
- Preserves adjacency for multi-chunk methods, allowing the LLM to reconstruct the full method body.  
- Mirrors the source file structure, simplifying tooling and RAG reconstruction.

## PrimaryEntity Detection

Each Manager chunk (overview and method-level) MUST include:

```jsonc
"PrimaryEntity": "Device"
```

The value is the simple class name of the main entity this Manager orchestrates.

### Heuristics (applied in order)

1. **Class Name Pattern (Strongest)**
   - If the class name matches `<EntityName>Manager`:
     - `PrimaryEntity = EntityName`

2. **Add/Create First-Parameter Heuristic**
   - If a method name begins with `Add*` or `Create*` and the first parameter type is a recognized Entity class:
     - `PrimaryEntity = <FirstParameterTypeName>`

3. **Method Signature Dominance**
   - Count entity types in method parameters and return types.  
   - When no stronger rule applies, choose the entity type that appears most frequently.

4. **Field/Property References (Final Fallback)**
   - If a single entity type is apparent from repository/service field names (e.g., `_deviceRepository`), use that entity as a tie-breaker.

### Ambiguity Handling

If no clear dominant entity is identified after applying the heuristics:

```jsonc
"PrimaryEntity": null
```

This situation is expected to be rare; most Managers in this system are single-entity-centric.

## Rationale

- Managers encapsulate key workflows and business operations for a primary entity.  
- Per-method chunks allow fine-grained reasoning about individual behaviors.  
- A class-level overview preserves workflow context and catalog.  
- `PrimaryEntity` connects Manager behavior to Models and UI metadata (IDX-0037, IDX-0038).  
- `PrimaryInterface` and `ImplementedInterfaces` enable clean cross-linking from Controllers (IDX-0041), which reference the Manager via DI interfaces.  
- Stable ordering reduces reindexing cost and enhances RAG quality.

## Resolved Questions

- **Do we always emit a ManagerOverview chunk?** Yes, one per Manager class.  
- **Do we allow method bodies to span multiple chunks?** Yes, via `ManagerMethodOverflow` chunks, but never across method boundaries.  
- **Must every Manager chunk include PrimaryEntity?** Yes, with null only in rare ambiguous cases.  
- **Do we track Manager<->interface relationships?** Yes, via `ImplementedInterfaces` and `PrimaryInterface`.  



# IDX-0040 – Chunking Strategy for Kind=Repository

**Status:** Accepted

## Description
Defines the deterministic, semantics-aware chunking strategy for Repository classes, including stable chunk ordering, per-method chunks, overflow handling, and **PrimaryEntity** detection.

Repositories are responsible for persistence semantics: how entities are stored, queried, and deleted.

## Scope

- `Kind = "SourceCode"`  
- `SubKind = "Repository"`  
- Language: C#  
- Applies to classes detected as repositories via existing heuristics (e.g., `InheritsDocumentDBRepoBase`, `InheritsTableStorageBase`, `Implements I*Repository`, `NamespaceIncludes("Repositories")`).

All chunks for a given repository file share:
- The same `DocId` (IDX-001)  
- Per-chunk `ContentHash` (IDX-016)  
- `PartIndex` / `PartTotal` (IDX-019)  
- `LineStart` / `LineEnd` (IDX-020)  
- Optional `CharStart` / `CharEnd` (IDX-021)  

SymbolType conventions:
- File-level chunk (optional): `File`  
- Repository overview: `Class`  
- Repository methods: `Method`  

## ChunkFlavors

Repository code uses three logical chunk flavors:

- `RepositoryOverview` — class-level overview  
- `RepositoryMethod` — per-method chunks  
- `RepositoryMethodOverflow` — continuation chunks for very large methods  

These may be represented as values in a `ChunkFlavor` field or as a combination of fields; the key requirement is that they are distinguishable by metadata.

## RepositoryOverview Chunk

Exactly one `RepositoryOverview` chunk per repository class.

**Content (in source order):**
- `using` directives (optional)  
- `namespace` declaration  
- XML doc comments on the repository class  
- Class-level attributes  
- Class signature (including base types and interfaces)  
- Private field declarations (signatures only)  
- Public/protected properties (signatures only)  
- A simple method index: list of public methods with their signatures only (no bodies)

**Required metadata:**
- `Kind = "SourceCode"`  
- `SubKind = "Repository"`  
- `SymbolType = "Class"`  
- `ChunkFlavor = "RepositoryOverview"`  
- `PrimaryEntity` — the main Entity type persisted by this repository (see below)  

**Optional metadata (when derivable):**
- `StorageProfile`:
  - `StorageKind` — e.g. `DocumentDb`, `TableStorage`, `Sql`, `InMemory`, `Other`  
  - `EntityType` — typically same as `PrimaryEntity`  
  - `CollectionOrTable` — if a constant or attribute reveals it  
  - `PartitionKeyField` — if clearly encoded in base class or attributes  

If a StorageProfile cannot be inferred reliably, it MAY be omitted or its fields set to null.

## RepositoryMethod Chunks

One `RepositoryMethod` chunk per significant method.

**Content (contiguous, in source order):**
- XML doc comments for the method (if present)  
- Method-level attributes  
- Full method signature (modifiers, parameters, generic constraints)  
- Full method body `{ ... }`  

**Included methods:**
- All public methods  
- Protected/internal methods that contain meaningful persistence/query/update logic  
- Private methods only when they encapsulate non-trivial query/update behavior

**Examples of high-value repository methods:**
- `GetByIdAsync`, `GetDeviceAsync`, `GetListAsync`  
- Query methods such as `GetDevicesForOrgAsync`  
- Persistence operations: `Add*`, `Insert*`, `Upsert*`, `Save*`, `Delete*`, `Remove*`  

**Metadata:**
- `Kind = "SourceCode"`  
- `SubKind = "Repository"`  
- `SymbolType = "Method"`  
- `ChunkFlavor = "RepositoryMethod"`  
- `PrimaryEntity` — same as overview  
- Optional `MethodKind` derived from name and behavior, e.g.:
  - `Query`, `GetById`, `Insert`, `Update`, `Delete`, `Upsert`, `Other`  

## RepositoryMethodOverflow Chunks

For methods whose bodies exceed chunk/token limits, a method may be split into multiple chunks:

- Never split across method boundaries; a chunk belongs to exactly one method.  
- When a single method body exceeds thresholds:
  - Split inside the method body at “safe” boundaries (blank lines, regions, comment blocks).  
  - The primary `RepositoryMethod` chunk contains the signature and the initial portion of the body.  
  - Additional `RepositoryMethodOverflow` chunks contain the remaining body content.

**Metadata:**
- `Kind = "SourceCode"`  
- `SubKind = "Repository"`  
- `SymbolType = "Method"`  
- `ChunkFlavor = "RepositoryMethodOverflow"`  
- `OverflowOf = "<MethodName>"`  
- `PrimaryEntity` — same as overview  

## Ordering and PartIndex

For each repository class (single DocId), chunks MUST be emitted in source order:

1. `RepositoryOverview`  
2. `RepositoryMethod` chunks in source order  
3. `RepositoryMethodOverflow` chunks immediately after their corresponding `RepositoryMethod` chunk  

`PartIndex` and `PartTotal` are assigned based on this physical order across **all** chunks for that file.

### Why Ordering Matters (Short Version)

- Keeps `PartIndex` / `PartTotal` stable across runs for unchanged files.  
- Avoids unnecessary reembedding when only trivial changes occur or when nothing changes.  
- Preserves adjacency for multi-chunk methods, allowing the LLM to reconstruct the full method body.  
- Mirrors the source file structure, simplifying tooling and RAG reconstruction.

## PrimaryEntity Detection

Each repository chunk (overview and method-level) MUST include:

```jsonc
"PrimaryEntity": "Device"
```

The value is the simple class name of the main entity this repository persists.

### Heuristics (applied in order)

1. **Base Class Generic Argument (Strongest)**
   - If the repository inherits a generic base such as `DocumentDBRepoBase<T>` or `TableStorageRepoBase<T>`:  
     → `PrimaryEntity = typeof(T).Name`

2. **Class Name Pattern**
   - If the class name matches:
     - `<EntityName>Repository`  
     - `<EntityName>Repo`  
   - Then:  
     → `PrimaryEntity = EntityName`

3. **Add/Insert/Upsert/Save First Parameter Heuristic**
   - If methods with names starting with `Add*`, `Insert*`, `Upsert*`, or `Save*` exist and their **first parameter type** is a recognized entity class:  
     → `PrimaryEntity = <FirstParameterTypeName>`

4. **Method Signature Dominance**
   - Count entity types appearing in method parameters and return types.  
   - When no stronger rule applies, choose the entity type that appears most frequently.

5. **Field/Property References (Final Fallback)**
   - If only one entity type is apparent from field/property names (e.g., `_deviceCollection`, `_deviceContainer`):  
     → use that entity as `PrimaryEntity`.

### Ambiguity Handling

If no clear dominant entity is identified after applying the heuristics:

```jsonc
"PrimaryEntity": null
```

This situation is expected to be rare; most repositories in this system are single-entity-centric.

## Optional StorageProfile

Where derivable, repository chunks MAY include:

```jsonc
"StorageProfile": {
  "StorageKind": "DocumentDb",
  "EntityType": "Device",
  "CollectionOrTable": "devices",
  "PartitionKeyField": "OwnerOrganizationId"
}
```

This is optional and used for enhanced reasoning and tooling; omission does not block indexing.

## Rationale

- Repository classes encode how entities are persisted and queried.  
- Per-method chunks make it easy for the LLM and tools to inspect or modify individual query/update behaviors.  
- A class-level overview keeps storage context and entity focus together.  
- `PrimaryEntity` connects repository behavior back to model structure and metadata (IDX-0037 and IDX-0038).  
- Stable ordering and method-grouped chunks reduce reindexing cost while improving retrieval quality and context reconstruction.

## Resolved Questions

- **Do we always emit a RepositoryOverview chunk?** Yes, one per repository class.  
- **Do we allow method bodies to span multiple chunks?** Yes, via `RepositoryMethodOverflow` chunks, but never across method boundaries.  
- **Must every repository chunk include PrimaryEntity?** Yes, with null only in rare ambiguous cases.  
- **Is StorageProfile required?** No, it is optional and populated only when easily and reliably inferred.

# IDX-0041 – Controller Endpoint Description Chunks

**Status:** Accepted

## 1. Description

Defines the **EndpointDescription** chunk format for HTTP controller endpoints.

Each EndpointDescription chunk captures a **1:1 semantic description of a single HTTP endpoint** (one controller action method), including:

- Identity (controller, action, route, HTTP methods)
- Linkage to Managers (handler interface/method)
- Summary and description text
- Request shape (parameters, body)
- Response shape (status codes, payloads, wrappers)
- Authorization and tenancy semantics

Raw controller chunks (`ChunkFlavor = "Raw"`) are defined elsewhere and are separate from this structured description.

## 2. Scope

- `Kind = "SourceCode"`
- `SubKind = "Controller"`
- `ChunkFlavor = "EndpointDescription"`
- Language: C#
- Applies to methods detected as HTTP endpoints via:
  - `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`, `[HttpPatch]`, or derived attributes

### 2.1 Chunk Cardinality

- Exactly **one EndpointDescription chunk per HTTP endpoint** (per controller action method).

### 2.2 Shared Metadata

Each EndpointDescription chunk includes:

- `Kind = "SourceCode"`
- `SubKind = "Controller"`
- `SymbolType = "Endpoint"`
- `ChunkFlavor = "EndpointDescription"`
- `DocId` – same as the controller source file (IDX-001)
- `PartIndex`, `PartTotal` – across all chunks for that `DocId` (IDX-019)
- `LineStart`, `LineEnd` – inclusive 1-based line range for the method (IDX-020)
- `CharStart`, `CharEnd` – optional, 0-based character offsets (IDX-021)
- `PrimaryEntity` – primary entity operated on by this endpoint (see Section 8)

EndpointDescription chunks do **not** contain the raw source code; they are a structured, summarized view.

## 3. High-Level Endpoint Identity (3.1)

The identity fields answer: **“Which endpoint is this?”**

```jsonc
{
  "ControllerName": "DeviceController",
  "ActionName": "GetDeviceAsync",
  "EndpointKey": "DeviceController.GetDevice",
  "RouteTemplate": "api/devices/{id}",
  "HttpMethods": ["GET"],
  "ApiVersion": "1.0",
  "Area": "DeviceManagement",
  "PrimaryEntity": "Device",
  "Handler": {
    "Interface": "IDeviceManager",
    "Method": "GetDeviceAsync",
    "Kind": "Manager"
  }
}
```

### 3.1.1 Required Identity Fields

- **ControllerName** (string)
  - C# controller class name, e.g. `DeviceController`.
- **ActionName** (string)
  - Method name, e.g. `GetDeviceAsync`.
- **EndpointKey** (string)
  - Stable identifier for this endpoint. Recommended pattern:
    - `"<ControllerName>.<ActionNameWithoutAsyncSuffix>"`
    - Example: `DeviceController.GetDeviceAsync` → `DeviceController.GetDevice`.
- **RouteTemplate** (string)
  - Effective route template, e.g. `"api/devices/{id}"`, combining controller- and method-level routes.
- **HttpMethods** (string array)
  - HTTP methods for this endpoint, e.g. `["GET"]`, `["POST"]`, `["PUT","PATCH"]`.

### 3.1.2 Optional Identity Fields

- **ApiVersion** (string, optional)
  - From `[ApiVersion]` or equivalent. Example: `"1.0"`, `"v2"`.
- **Area** (string, optional)
  - From `[Area]` attribute or naming convention. Example: `"Admin"`, `"DeviceManagement"`.
- **PrimaryEntity** (string, optional)
  - Simple name of the primary entity type this endpoint operates on, e.g. `"Device"`. See Section 8.

### 3.1.3 Handler (Manager Linkage)

Controllers are thin and delegate real work to Managers (or similar services). EndpointDescription records this via a `Handler` object:

```jsonc
"Handler": {
  "Interface": "IDeviceManager",
  "Method": "CreateDeviceAsync",
  "Kind": "Manager"
}
```

- **Interface** (string)
  - Name of the DI-injected interface, e.g. `IDeviceManager`.
- **Method** (string)
  - Name of the handler method invoked from the controller action, e.g. `CreateDeviceAsync`.
- **Kind** (string)
  - Logical type of handler. Initially `"Manager"` for Manager-based handlers.

Detection (high-level):

1. Inspect constructor-injected interfaces on the controller (e.g., `IDeviceManager`).
2. For each endpoint method, scan the body for the first non-trivial call on these injected fields.
3. Use that interface and method as the Handler.

This is cross-linked with IDX-0039:

- Manager metadata exposes `PrimaryInterface` and `ImplementedInterfaces`.
- EndpointDescription `Handler.Interface` can be joined to `ManagerOverview.PrimaryInterface`.

## 4. Summary & Description (3.2)

These fields describe **what the endpoint does** in human-readable terms.

```jsonc
{
  "Summary": "Gets a single device for the current organization.",
  "Description": "Returns a Device with configuration, status, and ownership validation for the authenticated organization.",
  "Tags": ["Device", "Read", "OrgScoped"],
  "Notes": [
    "Uses IDeviceManager.GetDeviceAsync internally.",
    "Returns InvokeResult<Device> with standard error handling."
  ]
}
```

- **Summary** (string, required)
  - One-line description.
  - Preferred source: XML `<summary>` on the action method.
  - If missing, synthesize from:
    - HTTP method,
    - Route template,
    - PrimaryEntity,
    - ActionName verb (`Get`, `Create`, `Update`, etc.).

- **Description** (string, optional)
  - Longer explanation of behavior.
  - Preferred source: XML `<remarks>` or nearby comments.
  - If missing, may be synthesized based on Manager, Model, and Tenancy semantics.

- **Tags** (string array, optional)
  - Free-form tags for classification, e.g. `["Device", "Read", "OrgScoped"]`.
  - May be derived from Area, PrimaryEntity, HTTP method, Tenancy, and/or custom attributes.

- **Notes** (string array, optional)
  - Additional implementation hints, e.g.:
    - `"Requires organization context from OrgEntityHeader."`
    - `"Returns InvokeResult<Device> for consistent error handling."`

XML documentation coverage is currently partial; missing summaries/descriptions are tolerated and may be synthesized by tooling/LLMs.

## 5. Request Shape (3.3)

The Request section answers **“what does the caller send?”** and distinguishes:

- Non-body parameters (Route/Query/Header)
- Request body (if any)

### 5.1 Parameter Classification

Each method parameter is classified as one of:

- `"Route"` – appears in the route template or marked `[FromRoute]`.
- `"Query"` – marked `[FromQuery]` or scalar in GET/DELETE by convention.
- `"Header"` – marked `[FromHeader]`.
- **Body parameter** – complex parameter forming the RequestBody (Section 5.3).
- **Service parameter (ignored)** – marked `[FromServices]` or clearly DI types (e.g. `ILogger<>`).

Classification heuristics (in order):

1. `[FromRoute]` → `Source = "Route"`.
2. `[FromQuery]` → `Source = "Query"`.
3. `[FromHeader]` → `Source = "Header"`.
4. Parameter name appears in `{...}` segment of `RouteTemplate` → `Source = "Route"`.
5. Complex type (class/record) in `POST`/`PUT`/`PATCH` and no RequestBody selected yet → RequestBody.
6. Simple scalar type (string, Guid, numeric, bool, DateTime, enum, etc.):
   - For `GET`/`DELETE`: `Source = "Query"`.
   - For `POST`/`PUT`/`PATCH`: `Source = "Query"` unless `[FromBody]` is present.
7. `[FromServices]` or DI-like types → ignored (not part of public request shape).
8. Otherwise → `Source = "Unknown"`.

### 5.2 Parameters (Non-Body)

All non-body parameters are represented in a `Parameters` array:

```jsonc
"Parameters": [
  {
    "Name": "id",
    "Source": "Route",
    "Type": "Guid",
    "IsRequired": true,
    "IsCollection": false,
    "DefaultValue": null,
    "Description": "Unique identifier of the device."
  },
  {
    "Name": "includeOffline",
    "Source": "Query",
    "Type": "bool",
    "IsRequired": false,
    "IsCollection": false,
    "DefaultValue": "false",
    "Description": "If true, includes offline devices in the results."
  }
]
```

Per-parameter fields:

- **Name** (string)
  - C# parameter name.
- **Source** (string)
  - `"Route" | "Query" | "Header" | "Unknown"`.
- **Type** (string)
  - Type name, e.g. `string`, `Guid`, `int`, `DeviceQueryRequest`.
  - Nullable types use underlying type in the string (e.g. `int?` → `int`).
- **IsRequired** (bool)
  - Route params → `true`.
  - Non-nullable scalars with no default → `true`.
  - Nullable or defaulted parameters → `false`.
  - `[Required]` attribute can force `true`.
- **IsCollection** (bool)
  - `true` for arrays/lists (`List<T>`, `T[]`, etc.).
- **DefaultValue** (string, optional)
  - Serialized default value when present (e.g. `"false"`, `"10"`).
- **Description** (string, optional)
  - From XML `<param name="...">` when available.

### 5.3 RequestBody (Main Payload)

Endpoints may have **zero or one** logical request body.

```jsonc
"RequestBody": {
  "ModelType": "Device",
  "IsCollection": false,
  "IsPrimitive": false,
  "ContentTypes": ["application/json"],
  "Description": "Device to create for the current organization."
}
```

Fields:

- **ModelType** (string, required when RequestBody present)
  - Underlying payload model type, e.g. `Device`, `CreateDeviceRequest`.
- **IsCollection** (bool, required)
  - `true` if the body is a collection (e.g. `List<Device>`, `Device[]`).

- **IsPrimitive** (bool, required)
  - `true` if the body is a scalar type (e.g. `string`, `Guid`).
- **ContentTypes** (string array, required)
  - Effective content types, default `["application/json"]` unless overridden by attributes like `[Consumes]`.
- **Description** (string, optional)
  - Description of the body, from XML comments when available or synthesized.

If the endpoint has no body, the `RequestBody` field is omitted (per IDX-012 null omission).

## 6. Response Shape (3.4)

Each endpoint has a `Responses` array, with one entry per status code.

```jsonc
"Responses": [
  {
    "StatusCode": 201,
    "Description": "Device created successfully.",
    "ModelType": "Device",
    "IsCollection": false,
    "IsWrapped": true,
    "WrapperType": "InvokeResult<Device>",
    "ContentTypes": ["application/json"],
    "IsError": false
  },
  {
    "StatusCode": 400,
    "Description": "Validation or input error.",
    "ModelType": "InvokeResult",
    "IsCollection": false,
    "IsWrapped": false,
    "ContentTypes": ["application/json"],
    "IsError": true,
    "ErrorShape": "InvokeResult"
  }
]
```

Per-response fields:

- **StatusCode** (int, required)
  - Example: `200`, `201`, `400`, `404`, `500`.
  - From explicit attributes where available; otherwise inferred from HTTP verb and conventions.

- **Description** (string, optional)
  - Human readable explanation. If no attribute-defined description is present, fall back to sensible defaults based on the status code.

- **ModelType** (string, optional)
  - Logical payload model type, **inside any framework wrapper**, e.g.:
    - `Task<InvokeResult<Device>>` → `"Device"`.
    - `Task<InvokeResult<List<Device>>>` → `"Device"` (with `IsCollection = true`).
    - `Task<InvokeResult>` → `null` (no payload model).

- **IsCollection** (bool, required when ModelType is present)
  - `true` if payload is a list/array (`List<T>`, `T[]`, etc.).
  - `false` otherwise.

- **IsWrapped** (bool, optional)
  - `true` when using a standard wrapper type (e.g., `InvokeResult`, `InvokeResult<T>`).

- **WrapperType** (string, optional)
  - Wrapper type name, e.g. `"InvokeResult<Device>"`, `"InvokeResult<List<Device>>"`.

- **ContentTypes** (string array, optional)
  - Content types for the response, e.g. `["application/json"]`.

- **IsError** (bool, optional)
  - `true` for typical error codes (4xx, 5xx).
  - `false` for successful responses (2xx).

- **ErrorShape** (string, optional)
  - Describes the error envelope, e.g. `"InvokeResult"` when errors are represented via `InvokeResult` with `Successful = false` and `Errors` populated.

This is V1; fields are designed to be extensible when code generation and real attributes demand tighter modeling.

## 7. Authorization & Access Control (3.5)

Authorization metadata is captured in an `Authorization` object:

```jsonc
"Authorization": {
  "RequiresAuthentication": true,
  "AllowAnonymous": false,
  "Roles": ["OrgAdmin"],
  "Policies": ["DeviceWrite"],
  "Scopes": [],
  "Tenancy": "OrgScoped"
}
```

Fields:

- **RequiresAuthentication** (bool, required)
  - `true` when class or method has `[Authorize]` (or equivalent) according to project defaults.
  - `false` when explicitly anonymous via `[AllowAnonymous]` or conventions.

- **AllowAnonymous** (bool, required)
  - `true` if `[AllowAnonymous]` is present on the method.
  - `false` otherwise.

Consistency rule:

- If `AllowAnonymous = true`, then `RequiresAuthentication = false`.

- **Roles** (string array, optional)
  - From attributes like `[Authorize(Roles = "OrgAdmin,Support")]`, split by comma and trimmed.
  - Example: `["OrgAdmin", "Support"]`.

- **Policies** (string array, optional)
  - From `[Authorize(Policy = "...")]` and **custom authorization attributes** that map to policy names.

- **Scopes** (string array, optional)
  - For OAuth-style scope requirements, e.g. `["devices.read", "devices.write"]`.
  - Future-friendly; optional if not used today.

- **Tenancy** (string, optional but recommended)
  - Logical tenancy context for the endpoint. Suggested values:
    - `"OrgScoped"` – requires organization context, uses org headers.
    - `"UserScoped"` – tied primarily to current user.
    - `"System"` – cross-organization or administrative operations.
    - `"Public"` – available without authentication.
  - Derived from custom attributes, use of `OrgEntityHeader`/`UserEntityHeader`, route patterns, or explicit annotations.

## 8. PrimaryEntity Semantics

Each EndpointDescription chunk includes `PrimaryEntity` where determinable. This is the simple class name of the main entity the endpoint acts upon (e.g., `Device`, `Customer`).

Heuristics (high-level, aligned with the broader system):

- Use controller naming (`DeviceController` → `Device`) when obvious.
- Use route segments (`api/devices/...` → `Device`).
- Use request/response model types (entity models and DTOs).
- Use handler Manager’s `PrimaryEntity` when available (from IDX-0039).

If no clear entity can be determined, `PrimaryEntity` is omitted (null).

## 9. Ordering & PartIndex

For each controller file (`DocId`), all EndpointDescription chunks are ordered in **source order**:

1. Controllers in file order.
2. Within each controller, methods in declaration order.

`PartIndex` and `PartTotal` (IDX-019) are assigned based on this physical sequence across all chunks for that file.

### Why Ordering Matters

- Keeps `PartIndex`/`PartTotal` stable across indexing runs when the file does not change.
- Reduces reindexing churn when endpoints are added/removed or reordered.
- Mirrors source structure, simplifying navigation and RAG reconstruction.

## 10. Rationale

- A single EndpointDescription chunk per endpoint creates a clean, semantic unit for reasoning, search, and code generation.
- High-level identity plus Handler linkage ties controllers to Manager metadata (IDX-0039) and Models (IDX-0037, IDX-0038).
- Structured request/response metadata supports client generation, testing tools, and LLM-based workflows.
- Authorization and Tenancy metadata enable security analysis and safer automated usage.
- The design is V1: structurally stable, but heuristics and coverage can be tightened as real indexing and generation code is built.

# IDX-0042 – Interface Overview Chunks

**Status:** Accepted  

## 1. Description

Defines the **InterfaceOverview** chunk format for C# interfaces.

Each InterfaceOverview chunk captures a **1:1 semantic description of a single interface type**, including:

- Identity and classification (namespace, name, generic info)
- Relationship to primary entity (when applicable)
- Role (Manager/Repository/Service/Other contract)
- Method surface (name, parameters, return type, async flag)
- Linkage to implementing classes and dependent controllers

This DDR focuses on the *contract-level view* of interfaces and is designed to pair with:

- Managers (IDX-0039)
- Repositories (IDX-0040)
- Controllers / EndpointDescription (IDX-0041)
- Model structure/metadata (IDX-0037 / IDX-0038)

## 2. Scope

- `Kind = "SourceCode"`
- `SubKind = "Interface"`
- `SymbolType = "Interface"`
- `ChunkFlavor = "Overview"`
- Language: C#
- Applies to C# `interface` declarations discovered in the source tree.

### 2.1 Chunk Cardinality

- Exactly **one InterfaceOverview chunk per interface type**.
- No per-method sub-chunks for V1; methods are summarized inside the overview.

### 2.2 Shared Metadata

Each InterfaceOverview chunk includes standard indexing metadata:

- `Kind = "SourceCode"`
- `SubKind = "Interface"`
- `SymbolType = "Interface"`
- `ChunkFlavor = "Overview"`
- `DocId` – same as the interface source file (IDX-001)
- `PartIndex`, `PartTotal` – position within the file’s chunk sequence (IDX-019)
- `LineStart`, `LineEnd` – inclusive 1-based line range for the interface declaration (IDX-020)
- `CharStart`, `CharEnd` – optional 0-based character offsets (IDX-021)

InterfaceOverview chunks **do not** include raw source; they are structured metadata summarizing the contract.

## 3. Interface Identity & Classification

These fields answer: **“Which interface is this and how does it fit into the architecture?”**

```jsonc
{
  "InterfaceName": "IDeviceManager",
  "Namespace": "LagoVista.IoT.DeviceAdmin.Managers",
  "FullName": "LagoVista.IoT.DeviceAdmin.Managers.IDeviceManager",
  "IsGeneric": false,
  "GenericArity": 0,
  "BaseInterfaces": [
    "System.IDisposable"
  ],
  "PrimaryEntity": "Device",
  "Role": "ManagerContract"
}
```

### 3.1 Identity Fields

- **InterfaceName** (string, required)  
  - Simple type name, e.g. `IDeviceManager`.

- **Namespace** (string, required)  
  - Fully qualified namespace, e.g. `LagoVista.IoT.DeviceAdmin.Managers`.

- **FullName** (string, required)  
  - `"Namespace.InterfaceName"`, e.g. `"LagoVista.IoT.DeviceAdmin.Managers.IDeviceManager"`.

- **IsGeneric** (bool, required)  
  - `true` if the interface is generic (e.g. `IRepository<T>`), otherwise `false`.

- **GenericArity** (int, required)  
  - Number of generic type parameters, e.g. `0` for non-generic, `1` for `IRepository<T>`.

- **BaseInterfaces** (string array, optional)  
  - Full names of interfaces this interface extends, e.g. `["System.IDisposable", "LagoVista.Core.Interfaces.IValidateable"]`.
  - Omitted if there are no base interfaces.

### 3.2 PrimaryEntity

- **PrimaryEntity** (string, optional but strongly recommended when applicable)  
  - Simple class name of the primary entity the interface operates on, e.g. `"Device"` for `IDeviceManager`.

Heuristics (aligned with broader system rules):

1. **Naming pattern**: strip common prefixes/suffixes (e.g. `I`, `Manager`, `Repository`, `Service`) and match against known model/entity names.  
2. **Method signatures**: inspect parameters and return types for models (e.g. `Task<InvokeResult<Device>>`, parameters of type `Device`).  
3. **Manager/Repository metadata**: reuse `PrimaryEntity` from Manager/Repository chunks that implement this interface when already known.

If no clear mapping can be inferred, `PrimaryEntity` is omitted.

### 3.3 Role Classification

- **Role** (string, optional)  
  - Coarse classification of the interface’s architectural role. Suggested values:
    - `"ManagerContract"`
    - `"RepositoryContract"`
    - `"ServiceContract"`
    - `"OtherContract"`

Heuristics:

- Interface names ending in `Manager` → `"ManagerContract"`.
- Interface names ending in `Repository` → `"RepositoryContract"`.
- Interface names ending in `Service` → `"ServiceContract"`.
- Otherwise → `"OtherContract"` (or omitted).

Global metadata fields like `Domain`, `Layer`, and `Role` (from other DDRs) still apply at the chunk level; this `Role` is specific to the **interface as a contract**.

## 4. Method Summary

The `Methods` array describes the **surface** of the contract without full AST details.

```jsonc
"Methods": [
  {
    "Name": "CreateDeviceAsync",
    "ReturnType": "Task<InvokeResult<Device>>",
    "IsAsync": true,
    "Parameters": [
      {
        "Name": "device",
        "Type": "Device",
        "IsOptional": false,
        "DefaultValue": null
      }
    ],
    "Summary": "Creates a new device in the current organization."
  },
  {
    "Name": "GetDeviceAsync",
    "ReturnType": "Task<InvokeResult<Device>>",
    "IsAsync": true,
    "Parameters": [
      {
        "Name": "id",
        "Type": "string",
        "IsOptional": false,
        "DefaultValue": null
      }
    ],
    "Summary": "Gets a single device by its identifier."
  }
]
```

### 4.1 Method Fields

- **Name** (string, required)  
  - Method name, e.g. `CreateDeviceAsync`.

- **ReturnType** (string, required)  
  - Raw C# return type string, e.g. `Task<InvokeResult<Device>>`, `Task`, `InvokeResult`, `bool`.

- **IsAsync** (bool, required)  
  - `true` if the return type is `Task` or `Task<T>`, otherwise `false`.

- **Parameters** (array, required; may be empty)  
  - Each parameter:

    - `Name` (string, required)
      - C# parameter name.

    - `Type` (string, required)
      - Type name, e.g. `Device`, `string`, `CancellationToken`.

    - `IsOptional` (bool, required)
      - `true` if the parameter has a default value (`= null`, `= 0`, etc.) or is syntactically optional.
      - `false` otherwise.

    - `DefaultValue` (string, optional)
      - String representation of the default value when present (e.g. `"null"`, `"0"`, `"true"`).
      - Omitted when no default is defined.

- **Summary** (string, optional)  
  - Short description of the method.
  - Preferred source: XML `<summary>` on the interface method.
  - If missing, it may be synthesized by tooling/LLMs later, but the DDR only defines the field.

The method summary is intentionally lightweight: enough for contract navigation and reasoning without duplicating full implementation or UI-related metadata.

## 5. Usage & Linkage

InterfaceOverview connects the contract to its implementations and consumers.

```jsonc
"ImplementedBy": [
  "LagoVista.IoT.DeviceAdmin.Managers.DeviceManager"
],
"UsedByControllers": [
  "DeviceController.CreateDevice",
  "DeviceController.GetDevice"
]
```

### 5.1 ImplementedBy

- **ImplementedBy** (string array, optional)  
  - Full type names of classes implementing this interface, e.g.:
    - `"LagoVista.IoT.DeviceAdmin.Managers.DeviceManager"`.

Source of truth:

- Roslyn analysis where `class X : IDeviceManager` (or via `: IDeviceManager, IOtherInterface`).
- May be omitted if implementation relationships are not yet computed.

### 5.2 UsedByControllers

- **UsedByControllers** (string array, optional)  
  - List of `EndpointKey` values from IDX-0041 representing controller endpoints that depend on this interface via DI, e.g.:
    - `"DeviceController.CreateDevice"`
    - `"DeviceController.GetDevice"`.

Population:

- Controllers (IDX-0041) record a `Handler.Interface` field.
- These values can be “reverse-joined” into InterfaceOverview `UsedByControllers` to show which endpoints rely on this contract.
- Field is optional; omitted when such relationships are not computed or not available.

These linkages enable queries such as:

- “Which controllers use `IDeviceManager`?”  
- “Which classes implement `IUserManager` and what methods do they promise?”

## 6. Chunking & Ordering

For each interface source file (`DocId`):

- There is exactly one InterfaceOverview chunk per interface declaration.
- `LineStart` and `LineEnd` cover the entire interface block including its method signatures.
- `PartIndex` and `PartTotal` are assigned following the global rules (IDX-019):
  - Traverse the file in source order, assigning indexes as chunks are emitted.

No additional `ChunkFlavor` values are defined for interfaces in this DDR. If a raw-interface chunk is ever added, it will be specified in a separate DDR with its own `ChunkFlavor`.

## 7. Rationale

- Interfaces are central to the architecture (Managers, Repositories, Services).
- A single, compact **InterfaceOverview** chunk provides a contract-focused view that complements Manager/Repository/Controller chunks.
- This enables:
  - Contract-level navigation and reasoning.
  - Analysis of which classes implement a given contract.
  - Understanding which endpoints depend on which contracts.
- The design is intentionally lightweight and extensible:
  - Identity + methods + implementation/usage links.
  - UI and field-level metadata remain in model/metadata DDRs (IDX-0037 / IDX-0038).