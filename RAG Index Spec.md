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
