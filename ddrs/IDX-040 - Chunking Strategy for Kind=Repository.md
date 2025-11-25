# IDX-040 – Chunking Strategy for Kind=Repository

**Status:** Accepted

## 1. Description
This DDR defines the deterministic, semantics-aware chunking strategy for **Repository** classes. It standardizes how we cut repository source into chunks, how we tag those chunks with metadata, and how we determine the primary entity a repository is responsible for.

Repositories in this system own persistence semantics – how entities are stored, queried, and deleted.

## 2. Scope

- `Kind = "SourceCode"`
- `SubKind = "Repository"`
- Language: C#
- Applies to classes detected as repositories using the existing heuristics, for example:
  - Inheriting a known repository base type (`DocumentDBRepoBase<T>`, `TableStorageRepoBase<T>`, etc.)
  - Implementing interfaces like `I*Repository`
  - Residing in namespaces/folders that clearly indicate repositories

Shared constraints for all chunks from the same repository file:

- Same `DocId` (see IDX-001)
- Per-chunk `ContentHash` (IDX-016)
- `PartIndex` / `PartTotal` (IDX-019)
- `LineStart` / `LineEnd` (IDX-020)
- Optional `CharStart` / `CharEnd` (IDX-021)

SymbolType conventions:

- File-level chunk (if present): `File`
- Repository overview: `Class`
- Repository methods: `Method`

## 3. ChunkFlavors

Repository code is represented using three logical chunk flavors:

- `RepositoryOverview` – a single class-level overview
- `RepositoryMethod` – one chunk per significant method
- `RepositoryMethodOverflow` – additional chunks for oversized methods

We may store these as a `ChunkFlavor` field or equivalent, but each flavor MUST be distinguishable via metadata.

## 4. RepositoryOverview Chunk

Exactly one **RepositoryOverview** chunk is created for each repository class.

### 4.1 Content
In source order, the overview chunk contains:

- `using` directives (optional)
- `namespace` declaration
- XML doc comments for the repository class
- Class-level attributes
- Class signature (including base type and implemented interfaces)
- Private field declarations (signatures only)
- Public/protected properties (signatures only)
- A method index: the signatures of public methods (no bodies)

### 4.2 Required Metadata

- `Kind = "SourceCode"`
- `SubKind = "Repository"`
- `SymbolType = "Class"`
- `ChunkFlavor = "RepositoryOverview"`
- `PrimaryEntity` – simple name of the entity type this repository persists (Section 7)

### 4.3 Optional StorageProfile

Where it can be determined safely, an optional `StorageProfile` object may be included:

- `StorageKind` – e.g. `DocumentDb`, `TableStorage`, `Sql`, `InMemory`, `Other`
- `EntityType` – typically the same as `PrimaryEntity`
- `CollectionOrTable` – collection or table name if clearly discoverable
- `PartitionKeyField` – partition key field when it is obvious from attributes or base types

If this data is not straightforward to infer, `StorageProfile` can be omitted or populated with null fields.

## 5. RepositoryMethod Chunks

Repository method chunks capture individual methods that carry persistence behavior.

### 5.1 Content
Each `RepositoryMethod` chunk is a contiguous slice of text in source order containing:

- XML doc comments for the method (if present)
- Method-level attributes
- Full method signature (including async, generics, modifiers)
- Full method body

### 5.2 Which Methods Are Included

Create a `RepositoryMethod` chunk for:

- All public methods
- Protected or internal methods that contain meaningful persistence, query, or update logic
- Private methods **when** they encapsulate non-trivial persistence or query behavior

Typical examples:

- Methods like `GetByIdAsync`, `GetDeviceAsync`, `GetListAsync`
- Query helpers: `GetDevicesForOrgAsync`, `GetDevicesForUserAsync`
- Persistence operations: `Add*`, `Insert*`, `Upsert*`, `Save*`, `Delete*`, `Remove*`

Very small utility helpers that do not directly impact persistence may be skipped.

### 5.3 Metadata

Each `RepositoryMethod` chunk MUST include:

- `Kind = "SourceCode"`
- `SubKind = "Repository"`
- `SymbolType = "Method"`
- `ChunkFlavor = "RepositoryMethod"`
- `PrimaryEntity` – same value as in the overview for that repository

Optional method-level classification:

- `MethodKind` – inferred label such as:
  - `Query`
  - `GetById`
  - `Insert`
  - `Update`
  - `Delete`
  - `Upsert`
  - `Other`

## 6. RepositoryMethodOverflow Chunks

Methods that exceed chunk/token limits can be broken into multiple pieces.

### 6.1 Rules

- A chunk corresponds to exactly one method – **never** split across method boundaries.
- If a single method’s body is too large:
  - Split it into a leading `RepositoryMethod` chunk plus one or more `RepositoryMethodOverflow` chunks.
  - Splits should occur at safe boundaries such as blank lines, region markers, or natural comment breaks.

### 6.2 Metadata

Each overflow chunk MUST include:

- `Kind = "SourceCode"`
- `SubKind = "Repository"`
- `SymbolType = "Method"`
- `ChunkFlavor = "RepositoryMethodOverflow"`
- `OverflowOf` – the method name this overflow chunk belongs to
- `PrimaryEntity` – same as the repository overview

## 7. PrimaryEntity Detection

Every repository chunk (overview and any method-related chunks) includes a `PrimaryEntity` field:

```jsonc
"PrimaryEntity": "Device"
```

The value is the **simple type name** of the primary entity persisted by the repository.

### 7.1 Heuristic Order
Apply the following in order until a clear answer is found:

1. **Base Class Generic Argument (strongest signal)**
   - For generic base classes such as `DocumentDBRepoBase<T>` or `TableStorageRepoBase<T>`, use:
     - `PrimaryEntity = typeof(T).Name`

2. **Class Name Pattern**
   - If the repository name matches:
     - `<EntityName>Repository`
     - `<EntityName>Repo`
   - Then `PrimaryEntity = <EntityName>`.

3. **Create/Add/Insert/Upsert/Save First Parameter**
   - Look for methods starting with `Add`, `Insert`, `Upsert`, or `Save`.
   - If the first parameter type in those methods corresponds to a known entity class, then:
     - `PrimaryEntity = <FirstParameterTypeName>`.

4. **Method Signature Dominance**
   - Count entity types appearing in method parameters and return types.
   - The most frequently occurring entity type becomes `PrimaryEntity`.

5. **Field/Property Clues**
   - If fields/properties reference a single entity in their names (e.g., `_deviceCollection`, `_deviceContainer`), use that as a final fallback.

### 7.2 Ambiguity

If none of the rules produce a clear winner, set:

```jsonc
"PrimaryEntity": null
```

This situation should be rare; most repositories are strongly associated with a single entity.

## 8. Ordering and PartIndex

For each repository class (single `DocId`), chunks are emitted in **source order**:

1. The `RepositoryOverview` chunk
2. All `RepositoryMethod` chunks in the order their methods appear
3. `RepositoryMethodOverflow` chunks immediately following the corresponding method chunk

`PartIndex` and `PartTotal` (IDX-019) are assigned based on this physical order across **all** chunks for that file.

### 8.1 Why Ordering Matters

- Keeps `PartIndex` and `PartTotal` stable when files remain unchanged.
- Reduces unnecessary reembedding for trivial edits.
- Preserves adjacency of multi-chunk methods so the full method can be reconstructed.
- Mirrors the natural structure of the source file, which simplifies tooling and RAG flows.

## 9. Rationale

- Repository classes encode the persistence story for models; we need their structure in a form that is both machine- and LLM-friendly.
- Per-method chunks support fine-grained reasoning and editing over individual queries and persistence behaviors.
- A single overview chunk anchors that method-level detail to the repository’s broader role and storage configuration.
- `PrimaryEntity` creates a bridge between repositories and model/metadata descriptions (IDX-037 and IDX-038).
- Stable ordering and consistent chunk flavors reduce churn in the index and raise the quality of retrieved context.
