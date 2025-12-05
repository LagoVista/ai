# IDX-025 – RagVectorPayload Metadata Contract

**Status:** Accepted  
**Owner:** AI / RAG Infrastructure  
**Namespace:** `LagoVista.Core.Utils.Types.Nuviot.RagIndexing`  
**Artifact Types:**
- `enum RagContentType`  
- `sealed class RagVectorPayload`

---

## 1. Purpose & Scope

This DDR defines the **canonical metadata payload** for all vector embeddings in the system.

`RagVectorPayload` is used across:
- The **indexing pipeline** (writing to Qdrant)
- The **retrieval layer** (querying and ranking)
- The **domain/model catalog**
- The **AI reasoning and routing components**

It provides:

- Tenant & project identity  
- Deterministic semantic identity (`SemanticId`)  
- BusinessDomain classification (`BusinessDomainKey`, `BusinessDomainArea`)  
- Content type / subtype / subtype flavor  
- Chunking & section structure  
- Raw pointers to source content  
- Index metadata (model, version, timestamps)  
- Source-code metadata (repo, SHA, path, lines)  
- Validation (`InvokeResult`)  
- Payload conversion for Qdrant (`ToDictionary()`)

---

## 2. Types Overview

### 2.1 `RagContentType` – Content Classification Enum

| Name            | Value | Description                                      |
|-----------------|:-----:|--------------------------------------------------|
| Unknown         | 0     | Unclassified content (invalid for indexing)     |
| DomainDocument  | 1     | Domain narratives, guides, architecture docs    |
| SourceCode      | 2     | C#, TS, config-as-code, raw code artifacts      |
| Policy          | 3     | Policies, governance documents                  |
| Procedure       | 4     | Procedures, runbooks, operational steps         |
| Reference       | 5     | Reference sheets, APIs, cheat sheets            |
| Configuration   | 6     | Configuration files (JSON, YAML, XML)           |
| Infrastructure  | 7     | IaC, infra descriptors, topologies              |
| Schema          | 8     | Data schemas (DB schemas, JSON schema, etc.)    |
| ApiContract     | 9     | OpenAPI, protobuf, gRPC, REST service contracts |
| Spec            | 10    | DDRs, system specs, requirement sets            |

Persisted as:
- `ContentTypeId` (required)
- `ContentType` (optional string label)

---

## 3. Field Reference

### 3.1 Identity & Tenant Isolation

| Field        | Required | Description |
|--------------|----------|-------------|
| `OrgId`      | ✔        | Tenant / organization identifier |
| `ProjectId`  | ✔        | Project, product, or repo grouping |
| `DocId`      | ✔        | Logical document identifier |

These form the **isolation boundary** for all chunk metadata.

---

### 3.2 BusinessDomain Classification

These fields identify the **business domain** and optional **sub-area** the content belongs to.

| Field        | Required     | Description |
|--------------|--------------|-------------|
| `BusinessDomainKey`  | Recommended  | Primary business domain — e.g., `billing`, `customers`, `iot`, `hr`, `alerts` |
| `BusinessDomainArea` | Optional     | Sub-area within a domain — e.g., `payments`, `onboarding`, `invoicing` |

`BusinessDomainKey` is strongly recommended for all indexed content and may be enforced as required by individual pipelines. 

`ValidateForIndex()` will emit a **warning** (not an error) if `BusinessDomainKey` is not set.

---

### 3.3 Semantic Identity

`SemanticId` is the natural domain key for a chunk.

- Deterministic
- Human-readable
- Derived from: `DocId + SectionKey + PartIndex`
- Used for idempotent indexing
- Independent from Qdrant’s internal ID (UUID or numeric)

Format:

```text
{DocId}:sec:{slug(SectionKey)}#p{PartIndex}
```

Example:

```text
billing.models:sec:customer-entity#p2
```

`SemanticId` is always included in the Qdrant payload.

---

### 3.4 Content Classification

| Field           | Required | Description |
|-----------------|----------|-------------|
| `ContentTypeId` | ✔        | Major classification enum |
| `ContentType`   | Optional | Human-readable label |
| `Subtype`       | Optional | Type refinement (e.g., `Model`, `Runbook`, `Controller`, `Resx`) |
| `SubtypeFlavor` | Optional | Granular view of `Subtype` (e.g. `ModelStructured`, `ModelMetadata`) |

Examples:

- `ContentTypeId = SourceCode`, `Subtype = "Model"`, `SubtypeFlavor = "ModelStructured"`
- `ContentTypeId = Spec`, `Subtype = "SpecSection"`, `SubtypeFlavor = "AcceptanceCriteria"`

Use:
- `ContentTypeId` -> coarse bucket
- `Subtype`       -> mid-level group
- `SubtypeFlavor` -> specific analytical/presentational view

---

### 3.5 Section & Chunking

| Field         | Required | Description |
|---------------|----------|-------------|
| `SectionKey`  | ✔        | Logical section name within the document |
| `PartIndex`   | ✔        | 1-based chunk index within the section |
| `PartTotal`   | ✔        | Total chunks in this section |

These fields enable ordered reconstruction of original content and deterministic semantic IDs.

---

### 3.6 Core Metadata

| Field        | Description |
|--------------|-------------|
| `Title`      | Human-readable title/heading |
| `Language`   | Language tag, e.g. `en`, `en-US` |
| `Priority`   | Ranking hint (default: 3; 1 = highest) |
| `Audience`   | Intended audience (`Developer`, `Ops`, etc.) |
| `Persona`    | Specific persona (`BackendEngineer`, `CSROperator`, etc.) |
| `Stage`      | Lifecycle stage (`Discovery`, `Implementation`, `Runbook`, `Incident`, etc.) |
| `LabelSlugs` | Free-form slug tags (`"ai-indexer"`, `"ddr"`, etc.) |
| `LabelIds`   | System label IDs (GUIDs, numeric IDs, etc.) |

These are optional at the core contract level but may be required by specific DDRs.

---

### 3.7 Raw Source Pointers

Pointers back to the original asset:
- `FullDocumentBlobUri` – URI to backing blob
- `SourceSliceBlobUri` – The exact source code that was used for this indexed entity
- `DescriptionBlobUri` – A textual/human readable description.
- `BlobVersionId` – version/ETag identifier
- `SourceSha256` – SHA-256 of source text

Location metadata:

- `LineStart`, `LineEnd` – 1-based line range
- `CharStart`, `CharEnd` – 0-based character offsets

Symbol information:

- `Symbol` – symbol name (class, method, interface, etc.)
- `SymbolType` – symbol kind (`"class"`, `"method"`, etc.)

Other locators:

- `HtmlAnchor` – HTML fragment/anchor
- `PdfPages` – list of relevant PDF pages (1-based)

---

### 3.8 Indexing & Embedding Metadata

- `IndexVersion` (int)
  - Required. Schema/pipeline version. Defaults to `1` and normalized if <= 0.

- `EmbeddingModel` (string)
  - Required. Embedding model name. Defaults to `"text-embedding-3-large"` and normalized if empty.

- `ContentHash` (string)
  - Hash of normalized content text for idempotent re-indexing and duplicate detection.

- `ChunkSizeTokens` (int?)
  - Size of the chunk in tokens.

- `OverlapTokens` (int?)
  - Token overlap with previous chunk (for sliding windows).

- `ContentLenChars` (int?)
  - Length of normalized content in characters.

- `IndexedUtc` (DateTime)
  - Required. UTC timestamp for when this chunk was indexed.
  - Normalized to `DateTime.UtcNow` if default.

- `UpdatedUtc` (DateTime?)
  - Optional. Last update time, if different from initial indexing.

- `SourceSystem` (string)
  - Logical origin (`GitHub`, `NuviotDocs`, `AzureDevOps`, etc.).

- `SourceObjectId` (string)
  - System-specific ID (issue ID, page ID, DB key, etc.).

---

### 3.9 Source-Code-Specific Fields

For `ContentTypeId = SourceCode` (and related subtypes):

- `Repo` – repository identifier (URL or logical name)
- `RepoBranch` – branch name
- `CommitSha` – commit SHA for this snapshot
- `Path` – repository-relative file path
- `StartLine`, `EndLine` – 1-based source line range

These enable queries that join with VCS (e.g., "what changed in billing/login last 30 days").

---

## 4. Validation – `ValidateForIndex()`

```csharp
public InvokeResult ValidateForIndex()
```

Validation responsibilities:

1. **Validate required fields** and record errors via `AddUserError()`.
2. **Normalize** section/chunk fields.
3. **Apply defaults** for index metadata.
4. **Ensure `SemanticId`** is set or report an error.
5. **Advise on `BusinessDomainKey`** via a warning if missing.

### 4.1 Errors (Block Indexing)

- `OrgId` missing/empty
- `ProjectId` missing/empty
- `DocId` missing/empty
- `ContentTypeId == Unknown`
- `SemanticId` is missing **and** `DocId` is not available to generate one

If any errors are present, the payload is **not** safe to index and the caller must abort.

### 4.2 Warnings & Normalizations

- `SectionKey` empty → set to `"body"`
- `PartIndex < 1` → set to `1`
- `PartTotal < PartIndex` → set to `PartIndex`
- `IndexVersion <= 0` → set to `1`
- `EmbeddingModel` empty → set to `"text-embedding-3-large"`
- `IndexedUtc` default → set to current UTC
- `SemanticId` empty but `DocId` present → generated via `BuildSemanticId()`
- `BusinessDomainKey` empty → warning: domain classification is strongly recommended

Warnings indicate non-fatal issues that were auto-corrected; errors must be resolved before indexing.

---

## 5. Payload Conversion – `ToDictionary()`

```csharp
public Dictionary<string, object> ToDictionary()
```

Behavior:

- Uses **PascalCase** keys.
- Skips null values and empty strings.
- For enumerables, collects non-null elements and omits empty collections.
- Serializes `IndexedUtc` and `UpdatedUtc` using `ToString("o")` (ISO 8601).

Example fragment:

```json
{
  "OrgId": "org-123",
  "ProjectId": "proj-abc",
  "DocId": "IDX-025",
  "SemanticId": "IDX-025:sec:index-embedding-metadata#p1",
  "BusinessDomainKey": "billing",
  "BusinessDomainArea": "payments",
  "ContentTypeId": 10,
  "ContentType": "Spec",
  "Subtype": "Model",
  "SubtypeFlavor": "ModelStructured",
  "SectionKey": "index-embedding-metadata",
  "PartIndex": 1,
  "PartTotal": 1,
  "IndexedUtc": "2025-11-23T18:30:00.0000000Z"
}
```

---

## 6. Qdrant Point Construction – `ToQdrantPoint()`

```csharp
public QdrantPoint ToQdrantPoint(string pointId, float[] embedding)
```

Ensures:

- `pointId` is non-empty
- `embedding` is non-null and non-empty

Returns a `QdrantPoint` with:

- `Id = pointId`      // Qdrant primary key (UUID or numeric)
- `Vector = embedding`
- `Payload = ToDictionary()`

> Qdrant's primary key is **separate** from `SemanticId`. `SemanticId` remains the domain's natural key.

---

## 7. Identity Helpers

### 7.1 `BuildSemanticId()` – Natural Key

```csharp
public static string BuildSemanticId(string docId, string sectionKey, int partIndex)
```

Builds deterministic semantic IDs:

- Requires non-empty `docId`
- Normalizes `sectionKey` to `"body"` if missing
- Normalizes `partIndex` to `1` if < 1

Format:

```text
{docId}:sec:{slug(sectionKey)}#p{partIndex}
```

### 7.2 `BuildPointId()` – Legacy String Helper

```csharp
public static string BuildPointId(string docId, string sectionKey, int partIndex)
```

Retains the same format as `BuildSemanticId()` and may be used where a deterministic string ID is needed outside Qdrant constraints.

### 7.3 `Slug()` – Section Key Normalization

Private helper that:

- Lowercases input
- Keeps `a-z0-9`
- Maps whitespace and `- _ . /` to `-` (collapsing repeats)
- Trims leading/trailing `-`
- Returns `"body"` if the result is empty

---

## 8. Minimum Requirements for Indexing

### Outside `RagVectorPayload`

- `float[] embedding` – required, non-empty
- `pointId` – required, must comply with Qdrant (UUID or numeric)

### Inside `RagVectorPayload`

Minimum set the pipeline relies on:

- `OrgId`
- `ProjectId`
- `DocId`
- `ContentTypeId` (not `Unknown`)
- `SectionKey` (defaults to `"body"` if missing)
- `PartIndex` (>= 1)
- `PartTotal` (>= `PartIndex`)
- `IndexVersion` (> 0, defaults to 1)
- `EmbeddingModel` (non-empty, defaults to `"text-embedding-3-large"`)
- `IndexedUtc` (non-default)
- `SemanticId` (or enough info to generate it)

Strongly recommended (but not hard-blocking at the core level):

- `BusinessDomainKey`
- `ContentHash`
- `Subtype` / `SubtypeFlavor`

Specific DDRs may promote these from "recommended" to "required".

---

## 9. Usage Summary

Typical indexing flow:

1. Construct a `RagVectorPayload` with:
   - Identity (Org/Project/Doc)
   - Domain info (BusinessDomainKey/BusinessDomainArea)
   - Classification (ContentTypeId, Subtype, SubtypeFlavor)
   - Section & chunk info (SectionKey, PartIndex, PartTotal)
   - Optional extra metadata
2. Call `ValidateForIndex()` and inspect `InvokeResult`:
   - If `HasErrors` → do not index; log/report
   - If no errors → proceed (warnings are advisory)
3. Generate a valid Qdrant `pointId` (UUID or numeric)
4. Generate `embedding` from normalized chunk text
5. Build a `QdrantPoint` via `ToQdrantPoint(pointId, embedding)`
6. Persist it to Qdrant

IDX-025 serves as the **single source of truth** for how chunk metadata must be structured and validated before entering the vector database.
