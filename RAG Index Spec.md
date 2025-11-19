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
