# IDX-031 – SubKind Heuristics & Detection Rules

**Status:** Accepted

## 1. Description
Defines the prioritized detection algorithm for classifying server-side C# files into SubKinds (`DomainDescription`, `Model`, `Manager`, `Repository`, `Controller`, `Service`, `Interface`, `Other`).

## 2. Decision
### Detection Priority (Top → Bottom)
1. **DomainDescription**
2. **Model**
3. **Manager**
4. **Repository**
5. **Controller**
6. **Service**
7. **Interface**
8. **Other**

### Detection Criteria
Each SubKind uses:
- Attributes
- Base classes
- Interface suffixes
- Namespace/folder patterns
- Roslyn semantic analysis (attributes, BaseType, interfaces, namespace, file path)

### Conflicts
If multiple heuristics match at the same priority:
- File is **flagged for review**
- Highest-priority SubKind wins

### SymbolType Mapping
- Classes → `"component"`
- Interfaces → `"interface"`

### Chunking Strategy Variation
- SubKind influences how code is chunked:
  - Models: class-level or per-property
  - Manager/Repository/Service: per-method
  - Controller: per-HTTP action
  - Interface: per signature or entire interface
  - DomainDescription: per static description
  - Other: whole-file chunk

## 3. Rationale
- Stabilizes classification in a diverse legacy codebase.
- Enables SubKind-aware chunking and filtering.
- Flagging ambiguous cases supports iterative refinement.

## 4. Notes
Heuristics evolve as codebase patterns become clearer.
