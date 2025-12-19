# Item 7 – Normalized Chunk Building

This step is the heart of the indexing pipeline. Its goal is to take a **single source file** and convert it into one or more **NormalizedChunk** objects that are ready to be sent to the embedder.

Everything in this checklist is implemented (or will be implemented) inside:

`DefaultNormalizedChunkBuilderService`

---

## 7.0 – Preconditions

- ✅ An `IndexFileContext` exists for the file
  - `OrgId`
  - `ProjectId`
  - `RepoId`
  - `FullPath`
  - `RelativePath`
- ✅ A `DomainModelCatalog` has been built upstream and injected into the service
- ✅ The file is eligible for indexing (filtered by previous steps)

---

## 7.1 – Load Source Text

**Goal:** Read the full contents of the file from disk

**Method:**  
`LoadSourceTextAsync(IndexFileContext)`

**Output:**
- `string sourceText`

---

## 7.2 – Split into Symbol Fragments

**Goal:** Break the file into logical C# components (types)

**Method:**  
`SymbolSplitter.Split(sourceText, relativePath)`

Each fragment contains:
- Fully isolated C# syntax (usings + namespace + single type)
- `SourceText`
- `RelativePath`
- `PrimaryTypeName`

**Output:**
- `IReadOnlyList<SymbolSplitResult>`

> ⚠️ Important rule: Each result should contain **only one type**. If not, it’s a bug.

---

## 7.3 – Analyze Each Symbol

**Goal:** Determine what the symbol **is**

**Method:**  
`SourceKindAnalyzer.AnalyzeFile(fragment.SourceText, fragment.RelativePath)`

This produces:
- `CodeSubKind` (Model, Manager, Service, Repository, Controller, etc)
- `PrimaryTypeName`
- `Evidence`
- `Reason`

**Output:**
- `SymbolProcessingContext`
  - Fragment
  - SourceKindResult

---

## 7.4 – Enrich with Domain / Model Catalog

**Goal:** Inject knowledge about Domains and Models

**Source:** `DomainModelCatalog`

May add:
- `DomainName`
- `DomainTagline`
- `ModelName`
- `ModelTagline`
- Model field metadata (via `ModelMetadataDescription`)

**Output:**
- Enriched `SymbolProcessingContext`

_Currently implemented as a placeholder and ready for expansion._

---

## 7.5 – Build Roslyn RagChunks

**Goal:** Chunk the symbol into token-constrained parts

**Method:**  
`IChunkerServices.ChunkCSharpWithRoslyn()` → `RoslynCSharpChunker`

Each chunk includes:
- `TextNormalized`
- `Symbol`
- `SymbolType`
- `LineStart / LineEnd`
- `EstimatedTokens`

**Output:**
- `IReadOnlyList<RagChunk>`

---

## 7.6 – Build NormalizedChunks

**Goal:** Convert raw chunks into embedding-ready units

Each `NormalizedChunk` contains:
- `DocumentIdentity`
  - OrgId
  - ProjectId
  - RepoId
  - RelativePath
  - Computed `DocId`
- `NormalizedText`
- `Symbol`
- `SymbolType`
- `EstimatedTokens`
- (Future) `Summary`

**Method:**  
`BuildNormalizedChunks(ragChunks, symbols, context)`

**Output:**
- `IReadOnlyList<NormalizedChunk>` → sent to the embedder

---

## Future Enhancements (Already Planned)

These will be layered *into* Step 7 without changing the overall sequence:

- 🔹 Prepending header comments to each chunk (org, repo, domain, model, etc)
- 🔹 Injecting `MethodSummaryBuilder` output for methods
- 🔹 Injecting `ModelMetadataSummaryBuilder` output for model types
- 🔹 Adding Facets via `IFacetAccumulator`
- 🔹 Supporting non-C# source types using the same pattern

---

## Mental Model (TL;DR)

```
File ➜ Split ➜ Analyze ➜ Enrich ➜ Chunk ➜ Normalize ➜ Embed
```

Everything for Step 7 happens inside **DefaultNormalizedChunkBuilderService**.

---

✅ You now have:
- A clean architectural plan
- A single, central orchestration point
- Explicit extension hooks
- Deterministic output

This is a rock-solid foundation for the next 30 years 😉
