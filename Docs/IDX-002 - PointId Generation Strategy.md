# IDX-002 – PointId Generation Strategy

**Status:** Accepted

## 1. Description
The **PointId** is the unique identifier assigned to each **vector point** (chunk embedding) in the vector database. It allows each chunk to be upserted, retrieved, updated, or deleted independently of other chunks, even when they share the same `DocId`.

## 2. Decision
- Generate `PointId` as a **GUID** (UUID v4 or v5) represented as a string.
- Store `PointId` in metadata as a **string** (UUID format).
- Ensure `PointId` values are **globally unique** across all collections, documents, and chunks.
- Do **not** embed document/section details inside `PointId`; it is a pure GUID.
- Use `DocId`, `SectionKey`, and `PartIndex` in metadata for semantic identity; `PointId` is only the technical vector ID.

## 3. Rationale
- A plain GUID is simple to generate, reason about, and validate.
- Cleanly separates vector identity (`PointId`) from document and section identity (`DocId` + `SectionKey`).
- Aligns with vector DB expectations where IDs are typically integer or UUID-like strings.
- Avoids brittle custom encodings or slugs inside IDs.

## 4. Resolved Questions
1. **Should `PointId` equal `DocId`?**  
   No. One `DocId` can map to many `PointId`s.
2. **Is `PointId` globally or collection-scoped?**  
   Treated as globally unique for simplicity, even though some DBs only require collection-level uniqueness.
3. **Should `PointId` encode semantics?**  
   No. Use metadata fields for semantics; keep `PointId` as a plain GUID.
