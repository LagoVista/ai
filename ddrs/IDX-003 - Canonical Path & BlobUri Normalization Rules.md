# IDX-003 – Canonical Path & BlobUri Normalization Rules

**Status:** Accepted

## 1. Description
This DDR defines how we normalize file paths (`Path`) and storage references (`BlobUri`) so that the same logical file is always represented consistently. Normalization underpins deterministic `DocId` generation and reliable deduplication.

## 2. Decision
- The first segment of `Path` is the unique **project identifier** provided at repo setup (e.g., `nuvos`, `ai.machinelearning`, `co.core`).
- Path normalization:
  - Use forward slashes (`/`) only; convert all `\\` to `/`.
  - Lowercase the entire path string.
  - Collapse repeated slashes (`//` → `/`).
  - Trim leading/trailing slashes, then ensure `Path` begins with **one** leading `/`.
  - Example: `/projectId/libs/primitives/src/lib/button/button.component.ts`.
- `BlobUri` mirrors the normalized `Path` relative to the storage root, using the same separator and casing rules.
- Do **not** enforce reserved prefix folders (such as `/libs`, `/apps`, `/docs`); classification is done via metadata (`Kind`, `Domain`, `Layer`, `Role`).
- When a file is moved or renamed, treat the new location as a **new canonical path** (leading to a new `DocId`).
- For now, do **not** enforce an explicit maximum path length, relying on underlying storage limits (e.g., Azure Blob up to 1,024 characters).

## 3. Rationale
- Normalizing paths prevents minor differences (case, slashes, extra segments) from producing multiple identities for the same file.
- Making the project identifier the top path segment keeps paths portable and self-describing across repos/storage.
- Not enforcing structural prefixes keeps the system adaptable to varied repo layouts.
- Deferring explicit path length limits simplifies the contract while remaining within practical storage constraints.

## 4. Resolved Questions
1. **Strip file extensions for titles/component names?**  
   No. Keep extensions where needed; this is separate from `Path` normalization.
2. **Enforce reserved path prefixes?**  
   No.
3. **Enforce maximum path length now?**  
   No; rely on storage limits until tighter constraints are needed.
4. **Preserve old path segments when files move?**  
   No. A moved/renamed file is treated as a new canonical path.
