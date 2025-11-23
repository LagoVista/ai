# IDX-024 – SubKind Definitions for Server Code Base

**Status:** Accepted

## 1. Description
Defines the authoritative set of `SubKind` values for classifying server-side C# source artifacts.

## 2. Decision
- Allowed `SubKind` values (PascalCase):
  - `DomainDescription`
  - `Model`
  - `Manager`
  - `Repository`
  - `Controller`
  - `Service`
  - `Interface`
  - `Other` (fallback)

- Detection priority (highest first):
  1. DomainDescription
  2. Model
  3. Manager
  4. Repository
  5. Controller
  6. Service
  7. Interface
  8. Other

- Once matched, lower-priority checks are skipped.
- `SymbolType` conventions:
  - Classes → `Component`
  - Interfaces → `Interface`

- Chunking strategy **will vary** per SubKind.
- Mixed-purpose classes should be **flagged** for manual review.
- Automatic reclassification on source changes is **not** supported.
- SubKind list is currently **fixed** (not extensible or versioned).

## 3. Rationale
- Controlled vocabulary ensures stable classification.
- Priority-based detection reduces ambiguity in legacy codebases.
- Different SubKinds require different chunking profiles (e.g., for models vs controllers).
- Manual review ensures correctness when files contain multiple roles.

## 4. Notes
Future extensibility may introduce more SubKinds once usage patterns stabilize.
