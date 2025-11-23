# IDX-039 – Chunking Strategy for Kind=Manager

**Status:** Accepted

## 1. Description
Defines the deterministic chunking strategy for Manager classes, including overview/method chunks, overflow handling, interface metadata, and `PrimaryEntity` mapping.

## 2. Decision
### ChunkFlavors
- `ManagerOverview`
- `ManagerMethod`
- `ManagerMethodOverflow`

### ManagerOverview
Contains:
- Usings, namespace, XML docs
- Class signature, attributes
- Field/property signatures
- Method index list
- `ImplementedInterfaces[]`
- `PrimaryInterface`
- `PrimaryEntity`

### ManagerMethod
- Full method signature
- Full method body
- `MethodKind` (optional)
- `PrimaryEntity`

### Overflow Chunks
Used when method exceeds size limit.
- `ChunkFlavor = ManagerMethodOverflow`
- `OverflowOf = <MethodName>`

### Ordering
1. Overview
2. Methods (source order)
3. Overflow chunks (immediately after parent method)

### PrimaryEntity Detection
Heuristics:
1. ClassName → `<Entity>Manager`
2. Create/Add first-parameter type
3. Parameter/return-type frequency
4. Repository/service field names
If unresolved → null.

## 3. Rationale
- Preserves semantic context
- Enables controller→manager linking
- Stabilizes ordering for minimal re-indexing
