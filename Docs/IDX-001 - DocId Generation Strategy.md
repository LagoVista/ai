# IDX-001 – DocId Generation Strategy

**Status:** Accepted

## 1. Description
The **DocId** is the stable identifier assigned to a *document* (a source file or other artifact) within the indexing system. A single document can be split into multiple chunks, but **all of those chunks share the same DocId**. The DocId is used for consistent grouping, retrieval, and tracking across ingestion runs.

## 2. Decision
- Generate `DocId` as a **deterministic UUID v5** from a canonical string:
  - Canonical string: `<RepoUrl>|<NormalizedPath>`.
  - Normalize `RepoUrl` by trimming whitespace, lowercasing, and removing trailing slashes.
  - Normalize `Path` by converting `\\` to `/`, collapsing duplicate `/`, and lowercasing.
- Compute: `DocId = UUIDv5(NamespaceCodeFiles, canonicalString)`.
- Store `DocId` in metadata as a **string**:
  - Format: 32 uppercase hexadecimal characters, no braces, no hyphens.
  - Example: `A1B2C3D4E5F67890ABCDEF1234567890`.
- Guarantee **global uniqueness** across all indexed content (all projects/repos/orgs).
- If the canonical string changes (e.g., file moved/renamed), treat this as a **new DocId**; alias handling (if needed) is done outside `DocId`.
- Use a single fixed namespace GUID (`NamespaceCodeFiles`) unless a deliberate v2 migration of canonicalization rules is performed.

## 3. Rationale
- Deterministic UUID v5 provides stable IDs without centralized state or counters.
- The scheme aligns with existing `CodeDocId` behavior.
- String storage is flexible and easy to serialize and interop with other systems.
- Keeping aliasing out of `DocId` keeps the contract simple and deterministic.
- A single global space for DocIds avoids accidental collisions.

## 4. Resolved Questions
1. **Include `OrgId` in canonical string?**  
   No. `DocId` is globally scoped; tenant scoping lives in other fields.
2. **Store as string or Guid?**  
   String, uppercase, no hyphens.
3. **Namespace GUID versioning?**  
   Single fixed namespace; only change in a deliberate, versioned migration.
4. **Uniqueness scope?**  
   Global across all content.
5. **File renamed/moved → new DocId or alias?**  
   New DocId; aliasing (if needed) is external to this contract.
