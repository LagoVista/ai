# IDX-033 – Meta Data Registry: Facet Upload Contract

**Status:** Accepted

## 1. Description
Defines how the indexer reports **unique facet combinations** (Kind, SubKind, ChunkFlavor, etc.) to a Meta Data Registry service.
The registry stores these facet paths for filtering and analytics.

## 2. Decision
### Entry Semantics
Each entry represents one **unique facet path** discovered during the run.

### Required Fields
- `OrgId`
- `ProjectId`
- `ComponentName` (e.g., `ServerCodeIndexer`)
- `Facets[]` — ordered list of `{ Key, Value }` pairs

Examples:
```
Kind → SourceCode
Kind + SubKind → SourceCode/Model
Kind + SubKind + ChunkFlavor → SourceCode/Model/Raw
```

### Uniqueness Rules
- Unique per-run by tuple:
  `(OrgId, ProjectId, ComponentName, Facets[])`
- Indexer de-duplicates **within the run**
- Server handles cross-run deduping

### Scope
- Indexer only reports facet combinations; purposes (filters, UI, analytics) are out-of-scope.

### ComponentName
- Differentiates which subsystem produced an entry.
- Has no behavioral effect.

## 3. Rationale
- Enables dynamic facet exploration.
- Keeps the client simple—no need to query registry state.

## 4. Notes
Facet paths imply parent/child semantics via list ordering.
