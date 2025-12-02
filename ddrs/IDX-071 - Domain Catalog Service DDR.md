# IDX-071 — Domain Catalog Service DDR

**ID:** IDX-071  
**Title:** Domain Catalog Service  
**Status:** Approved  
**Owner:** Kevin Wolf & Aptix  
**Scope:** Defines the first-class Domain Catalog Service responsible for discovering, persisting, and exposing the global list of domains and interesting model classes across the LagoVista ecosystem.

---

## 1. Purpose & Scope

The Domain Catalog Service provides the single authoritative registry of all domains and all *interesting* model classes across the entire code base. It is a structural map, not a semantic interpreter.

An interesting class is any class decorated with the `[EntityDescription]` attribute. These classes form the primary backbone of the LagoVista ecosystem and represent the majority of meaningful models used in metadata-driven UI, data persistence, orchestration, and RAG workflows. We will refine and expand the notion of "interesting" over time.

The catalog:

- Includes all domains discovered in the repository, using the declared name and description from the resource library.  
- Includes all interesting classes belonging to each domain, capturing only their declared name and human-readable metadata.  
- Performs no refinement of text or semantics.  
- Exists as a single, global catalog instance per repository.

---

## 2. Discovery Input & Scan Behavior

The Domain Catalog Service receives a list of C# files and performs a deterministic scan to identify domain definitions and interesting model classes.

Discovery rules:

- Only files ending in `.cs` are considered.  
- Files under **test roots** (`tests/...`) must be excluded entirely, even if they contain domain-like classes or `[EntityDescription]` attributes. Only the `src/...` hierarchy contributes to the catalog.  
- Each C# file may define a domain, one or more interesting classes, or irrelevant content that is ignored.
- No inference, transformation, or refinement is performed.
- Any file-level or parsing-level failure is treated as a fatal error via `InvokeResult`.

The result is a raw structural mapping of **domain → classes** with no semantic interpretation applied.

---

## 3. Catalog Structure (Data Contracts & Immutability)

The Domain Catalog exposes concrete, immutable C#-shaped data contracts other DDRs may depend on.

### 3.1 DomainCatalog Root

```csharp
public sealed class DomainCatalog
{
    public IReadOnlyList<DomainEntry> Domains { get; }
    public IReadOnlyList<ModelClassEntry> Classes { get; }
}
```

### 3.2 DomainEntry

```csharp
public sealed class DomainEntry
{
    public string DomainKey { get; }
    public string Title { get; }
    public string Description { get; }
    public IReadOnlyList<ModelClassEntry> Classes { get; }
}
```

### 3.3 ModelClassEntry

```csharp
public sealed class ModelClassEntry
{
    public string DomainKey { get; }
    public string ClassName { get; }
    public string QualifiedClassName { get; }
    public string Title { get; }
    public string Description { get; }
    public string HelpText { get; }
    public string RelativePath { get; }
}
```

### 3.4 Immutability

All catalog objects are constructed as immutable; updates require building a new catalog instance.

---

## 4. Persistence Requirements

The catalog is stored at:

```
[SourceRoot]/rag-common/domain-master-catalog.json
```

### 4.1 Canonical Filename
`domain-master-catalog.json`

### 4.2 Save Operation
- Full overwrite on success.

### 4.3 Load Operation
- Fatal error if missing, unreadable, or invalid.

### 4.4 Deterministic Snapshot
- Exactly one snapshot per repository.

### 4.5 No Refinement
- Raw values only.

---

## 5. Refresh Capability

A refresh performs a deterministic full rebuild:

1. Discards previous catalog.  
2. Scans all provided `.cs` files (excluding tests).  
3. Constructs a new immutable catalog.  
4. Persists on success.

No partial updates allowed.

---

## 6. Constructor & DI Requirements

```csharp
public DomainCatalogService(IAdminLogger adminLogger, IngestionConfig ingestionConfig)
```

### Required Dependencies
- `IAdminLogger` for diagnostics.  
- `IngestionConfig` for resolving `SourceRoot` and catalog location.

### Discovery Inputs
- File lists are provided per build/refresh call.

---

## 7. Output and Error Behavior

All operations return `InvokeResult`.

### 7.1 Success
- In-memory and on-disk catalogs match.

### 7.2 Fatal Errors
- Any missing/invalid data, IO errors, serialization failures, or malformed classes.

### 7.3 No Partial Success
- Any invalid domain or model entry invalidates the entire operation.

### 7.4 Data Completeness
- All properties of `DomainEntry` and `ModelClassEntry` are mandatory.

---

## 8. Public Query Methods

```csharp
IReadOnlyList<DomainEntry> GetAllDomains();
IReadOnlyList<ModelClassEntry> GetClassesForDomain(string domainKey);
InvokeResult<DomainEntry> FindDomainForClass(string className);
```

### 8.1 GetAllDomains
- Returns all domains; never null.

### 8.2 GetClassesForDomain
- Case-insensitive domain key lookup; empty list if missing.

### 8.3 FindDomainForClass
- Accepts simple or fully-qualified class name.
- Returns `InvokeResult<DomainEntry>`; lookup miss is non-fatal.

---

## 9. No Refinement Requirements

- No cleanup, rewriting, transformation, or summarization.
- Catalog values must remain raw.
- Supports deterministic behavior across RAG and indexing flows.
