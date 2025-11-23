# IDX-004 – Kind & SubKind Classification Rules

**Status:** Accepted

## 1. Description
The `Kind` and `SubKind` fields provide a two-level classification for each document/chunk:
- `Kind`: broad category (e.g., `Code`, `Specs`, `DomainObjects`, `Documentation`, `Metadata`).
- `SubKind`: more specific classification beneath the `Kind` (e.g., `PrimitiveComponent`, `CompositeComponent`, `Service`, `Model`, `Repository`).

These fields are required for all processed items and are used heavily for filtering and retrieval.

## 2. Decision
- Introduce **required** metadata fields `Kind` and `SubKind` for every item.
- Both fields must use **PascalCase** (e.g., `PrimitiveComponent`).
- For now, values are **free-form strings**; a controlled vocabulary / glossary will be defined later.
- Exactly **one** `Kind` and **one** `SubKind` per item (no arrays).
- Ingest pipelines are responsible for populating both fields based on file type/role.

## 3. Rationale
- A two-level classification (Kind + SubKind) offers finer granularity without exploding the number of top-level types.
- `Kind` and `SubKind` strongly enhance querying and filtering (broad vs specific views).
- PascalCase matches .NET property/type naming conventions and keeps metadata visually consistent.
- Starting with free-form values preserves agility; later governance can lock down the vocabulary.

## 4. Resolved Questions
1. **Do we want both `Kind` and `SubKind`?**  
   Yes.
2. **Is `SubKind` required?**  
   Yes; it must be populated (a value like `None` is acceptable if truly needed).
3. **Naming convention?**  
   PascalCase for both.
4. **Controlled vocabulary now?**  
   No; glossary to follow once patterns stabilize.
