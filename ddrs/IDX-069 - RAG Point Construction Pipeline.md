# IDX-069 — RAG Point Construction Pipeline

**ID:** IDX-069  
**Title:** RAG Point Construction Pipeline  
**Status:** Approved  
**Owner:** Kevin Wolf & Aptix  
**Scope:** Defines the standard pipeline for turning source artifacts into RAG points, and the contracts for Description Builders, descriptions, SummarySections, and RAG construction.

---

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-02 08:20:00 EST (UTC-05:00)

---

## 1. Purpose & Scope

IDX-069 defines the canonical pattern for turning any indexable asset (code symbols, DDRs, specs, text files, etc.) into one or more RAG points. It standardizes:

1. Inputs and file context (IndexFileContext).  
2. Symbol segmentation for code.  
3. SubtypeKind routing.  
4. Description Builders as DI services.  
5. Description contracts (IRagDescription).  
6. SummarySection construction as the atomic indexing unit.  
7. Invariant RAG point construction via SummaryFacts (or equivalent).  
8. The Description Builder Registry.

---

## 2. Inputs & File Context

- The pipeline starts from an `IReadOnlyList<DiscoveredFile>`.  
- Each file is normalized into an `IndexFileContext` that contains, at minimum:
  - `FullPath` (absolute path)
  - `RelativePath` (repo-relative path)
  - `Contents` (full file text)
  - `GitRepoInfo` (remote URL, branch, commit SHA)
  - `DocumentIdentity` (DocId, Org, Project, etc.)
  - Optional `LanguageHint` and `BlobUri`
- All downstream logic must rely on `IndexFileContext` and must not re-read files from disk or external storage.

Everything prior to `IndexFileContext` is out of scope for this DDR. Everything after is governed by IDX-069.

---

## 3. Symbol Segmentation for Code

- Symbol segmentation applies only to code files (e.g., C#).  
- Each file is split into one segment per top-level symbol:
  - `class`
  - `interface`
  - `enum`
  - `struct`
  - `record`
- Nested types are included in the segment for their containing symbol.
- The output is a collection of segment descriptors that include:
  - The symbol text
  - Symbol name
  - Symbol kind
  - Location metadata (line/offset ranges)
- Segmentation must be deterministic and side-effect free.

Text-based documents (DDRs, markdown, specs, etc.) bypass symbol splitting and are treated as single segments.

---

## 4. SubtypeKind Routing

- SubtypeKind detection (mapping segments to a `SubtypeKind` enum) is specified in other DDRs and is considered pre-baked here.  
- For each segment, the system provides a `SubtypeKind` (e.g., Model, Manager, Enum, DDRDocument, etc.).  
- Description Builders do not perform SubtypeKind detection; they are invoked only for kinds they are registered for.  
- Flavor (e.g., Command vs Query) is determined inside the builder, not at the routing layer.

---

## 5. Description Builders

### 5.1 Purpose

Each Description Builder converts a single segment plus its context into a fully enriched `IRagDescription` that can generate SummarySections and RAG points.

### 5.2 Builder Contract

All Description Builders implement:

```csharp
public interface IDescriptionBuilder
{
    // Identification for diagnostics, tooling, and introspection
    string Name { get; }
    string Description { get; }

    // Core build pipeline
    Task<InvokeResult<IRagDescription>> BuildAsync(
        IndexFileContext fileContext,
        string symbolText,
        IDomainCatalogService domainCatalogService,
        IResourceDictionary resourceDictionary);
}
```

Rules:

- `Name` is a short, human-readable identifier used in logs, UIs, and agent reasoning.  
- `Description` briefly explains what the builder handles and how it interprets the symbol.  
- `BuildAsync` returns `InvokeResult<IRagDescription>`; it must not throw for expected conditions.  
- A builder may signal "not applicable" via a non-success `InvokeResult` or a success with a null result, depending on the chosen policy.

### 5.3 Builders as DI Services

- Builders are DI-managed services, not static methods.  
- They may depend on other services, including:
  - Logging
  - Domain catalogs
  - Resource dictionary providers
  - Utility helpers
  - `IStructuredTextLlmService` for text refinement

### 5.4 LLM Usage (IStructuredTextLlmService)

Builders are encouraged to use `IStructuredTextLlmService` to refine natural-language text within descriptions (e.g., property summaries, overall narrative, clarifications).

Rules:

- LLM calls must occur only inside the builder layer.  
- Descriptions, SummarySections, and RAG construction logic must remain pure and deterministic (no DI, no I/O, no LLM calls).  
- LLM calls must follow `InvokeResult<T>` conventions and must not throw for expected failure states.  
- Builders should avoid unbounded loops that cause excessive LLM calls.

### 5.5 Flavor Determination & Responsibilities

- Multiple builders can be registered for the same `SubtypeKind`.  
- Each builder decides:
  - Whether it applies to a given symbol for that `SubtypeKind`.  
  - What `Subtype` and `SubtypeFlavor` to set on the resulting description.
- Each builder must:
  - Parse the symbol or document segment.  
  - Construct and populate a concrete `IRagDescription`.  
  - If inheriting from `SummaryFacts`, call `SetCommonProperties(IndexFileContext)` to populate shared metadata.  
  - Use domain and RESX data for enrichment as needed.
- Builders must not:
  - Perform symbol splitting.  
  - Build SummarySections directly.  
  - Create IRagPoints or call embedding services.  
  - Re-read files from disk or remote storage.

---

## 6. IRagDescription & SummarySections

### 6.1 IRagDescription Contract

Every description returned by a builder must implement:

```csharp
public interface IRagDescription
{
    IReadOnlyList<SummarySection> BuildSummarySections();
    IReadOnlyList<IRagPoint>      BuildRagPoints();
}
```

- `BuildSummarySections()` is subtype-specific and describes how structured facts are turned into SummarySections.  
- `BuildRagPoints()` is invariant and should delegate to the SummaryFacts implementation (or an equivalent that follows the same rules).

### 6.2 Recommended Base: SummaryFacts

- Descriptions are strongly encouraged (but not strictly required) to inherit from `SummaryFacts` and implement `IRagDescription`.  
- When using `SummaryFacts`, builders must call `SetCommonProperties(IndexFileContext)` to populate shared fields such as DocId, Org, Repo, Project, Branch, Commit, Path, BlobUri, and RepoId.

### 6.3 SummarySection Rules

SummarySection is the atomic indexing unit:

- One SummarySection produces exactly one RAG point.  
- Each SummarySection must provide:
  - FinderSnippet (embedded text).  
  - BackingArtifact (context sent to the LLM when retrieved).  
  - A file locator plus section metadata (SectionKey, PartIndex, PartTotal, symbol/type information, domain key, embedding model, etc.).
- `BuildSummarySections()` and `BuildRagPoints()` must be deterministic and must not:
  - Call external services.  
  - Perform I/O.  
  - Invoke LLMs.

---

## 7. RAG Point Construction via SummaryFacts

### 7.1 SummaryFacts Role

`SummaryFacts` is an abstract base class that:

- Implements `IRagableEntity`.  
- Holds shared identity and repository fields.  
- Orchestrates embedding creation across its `_summarySections`.  
- Implements invariant RAG point construction via `CreateIRagPoints()`.

Descriptions that inherit from `SummaryFacts` automatically gain:

- Shared metadata handling via `SetCommonProperties(IndexFileContext)`.  
- Section embedding orchestration via `CreateEmbeddingsAsync(IEmbedder)`.  
- Standardized RAG point creation via `CreateIRagPoints()` plus an override point for additional payload fields.

Descriptions that do not inherit from `SummaryFacts` must implement equivalent semantics for RAG point creation.

### 7.2 Common Context Population

Builders using SummaryFacts must call:

```csharp
SetCommonProperties(IndexFileContext ctx)
```

This populates fields such as:

- `DocId`, `OrgId`, `OrgNamespace`, `ProjectId`  
- `Repo`, `Branch`, `CommitSha`, `RepoId`  
- `Path`, `BlobUri`

These values are used later when constructing `RagVectorPayload` and RAG points.

### 7.3 Embedding Creation

Embedding creation is coordinated by:

```csharp
Task<InvokeResult> CreateEmbeddingsAsync(IEmbedder embeddingService)
```

- Iterates all `_summarySections`.  
- Calls `section.CreateEmbeddingsAsync(embeddingService)` for each section.  
- Aggregates all errors and warnings into a single `InvokeResult`.

Descriptions and builders do not talk to `IEmbedder` directly; all embedding logic flows through SummarySection and SummaryFacts.

### 7.4 IRagPoint Creation (CreateIRagPoints)

Core invariant behavior:

```csharp
IEnumerable<InvokeResult<IRagPoint>> CreateIRagPoints()
```

For each `SummarySection` in `_summarySections`:

1. A `RagVectorPayload` is created and populated with:
   - Identity and organization:
     - `DocId`, `OrgNamespace`, `OrgId`, `ProjectId`, `RepoId`
   - Repository context:
     - `Repo`, `RepoBranch` (Branch), `CommitSha`, `Path`
   - Classification:
     - `ContentTypeId` (virtual; defaults to SourceCode)
     - `Subtype` (abstract)
     - `SubtypeFlavor` (virtual)
   - Section info:
     - `SectionKey`, `EmbeddingModel`, `BusinessDomainKey`, `Language` (currently `"en-US"`)
2. A `Title` is generated in the format:
   - `"{SymbolType}: {Symbol} - {SectionKey} (Chunk {PartIndex} of {PartTotal})"`
3. A `SemanticId` is generated as:
   - `"{OrgNamespace}:{ProjectId}:{RepoId}:{SymbolType}:{Symbol}:{SectionKey}:{PartIndex}"` in lowercase.
   - The implementation validates that there are no `"::"` sequences (no missing segments).
4. `section.PopulateRagPayload(payload)` is called to add section-specific metadata.  
5. Blob URIs are assigned:
   - `FullDocumentBlobUri = BlobUri`  
   - `SnippetBlobUri = $"{BlobUri}.{SectionKey}.{PartIndex}"`, lowercased and with spaces replaced by `_`.
6. `PopulateAdditionalRagProperties(payload)` is called as the only extension point for subtype-specific RAG payload enrichment.  
7. A `RagPoint` is created:
   - `PointId = Guid.NewGuid().ToString()`  
   - `Payload = payload`  
   - `Vector = section.Vectors`  
   - `Contents = UTF8(section.SectionNormalizedText)`
8. The `RagPoint` is wrapped in `InvokeResult<IRagPoint>.Create(point)` and collected.

After processing all sections, the implementation verifies that all `SnippetBlobUri` values are unique; if not, it throws, as duplicate snippet blob URIs are considered a programming error.

RAG point creation does not call external services and does not perform I/O.

---

## 8. Description Builder Registry

### 8.1 Purpose

The Description Builder Registry provides a single lookup mechanism for retrieving all description builders associated with a given `SubtypeKind` enum value. This allows:

- Multiple builders per SubtypeKind (e.g., different flavors such as Command and Query).  
- Zero builders for kinds that are not indexable.  
- Extension without changes to `SourceFileProcessor`.

### 8.2 Registry Contract

```csharp
public interface IDescriptionBuilderRegistry
{
    IReadOnlyList<IDescriptionBuilder> GetBuilders(SubtypeKind subtypeKind);

    void Register<TBuilder>(SubtypeKind subtypeKind)
        where TBuilder : IDescriptionBuilder;
}
```

Rules:

- `SubtypeKind` is the only key at the registry level.  
- `GetBuilders(SubtypeKind)` returns zero or more builders for that kind, in a deterministic order.  
- The registry does not inspect symbol text, flavors, domains, or RESX data; it only groups builders by kind.

### 8.3 Registration Requirements

At startup:

```csharp
registry.Register<ModelCommandDescriptionBuilder>(SubtypeKind.Model);
registry.Register<ModelQueryDescriptionBuilder>(SubtypeKind.Model);
registry.Register<ManagerDescriptionBuilder>(SubtypeKind.Manager);
registry.Register<EnumDescriptionBuilder>(SubtypeKind.Enum);
// ...
```

Rules:

1. A `SubtypeKind` can have multiple different builders associated with it.  
2. The registry must prevent exact duplicates of the pair `(builder type, SubtypeKind)` (the same builder type registered to the same kind more than once).  
3. A builder type may be registered under multiple different `SubtypeKind` values if appropriate.  
4. The registry stores builder types; instances are resolved from DI when `GetBuilders` is called.

### 8.4 Runtime Usage

When processing a symbol:

1. SubtypeKind detection produces a `SubtypeKind kind`.  
2. `SourceFileProcessor` calls:

```csharp
var builders = registry.GetBuilders(kind);
```

3. For each builder:

```csharp
foreach (var builder in builders)
{
    var result = await builder.BuildAsync(
        ctx,
        symbolText,
        domainCatalogService,
        resourceDictionary);

    if (result.Successful && result.Result != null)
    {
        var description = result.Result;

        // downstream:
        var sections = description.BuildSummarySections();
        var ragPoints = description.BuildRagPoints();
        // ragPoints are passed into the vector upload pipeline
    }
}
```

- Builders decide whether they apply to the given symbol and what `Subtype` and `SubtypeFlavor` to set on the description.  
- `SourceFileProcessor` does not contain flavor-specific branching logic; it only works with `SubtypeKind` and the resulting `IRagDescription` instances.

### 8.5 Registry Responsibilities

The registry **does**:

- Maintain the mapping `SubtypeKind -> List<IDescriptionBuilder>` by type.  
- Resolve builder instances from DI.  
- Provide deterministic, side-effect-free lookups.

The registry **does not**:

- Determine applicability or flavor.  
- Inspect symbol text, domain catalog, or RESX content.  
- Invoke builders, LLMs, or embedding services.

All applicability and flavor logic lives within individual builders, and all RAG point construction follows the invariant behavior defined via SummaryFacts and IRagDescription in this DDR.
