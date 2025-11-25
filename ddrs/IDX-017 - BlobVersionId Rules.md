# IDX-017 – BlobVersionId Rules

**Status:** Accepted

## 1. Description
`BlobVersionId` was intended to track underlying storage versions. Under the current indexing model, it provides no functional value but may be stored for informational use.

## 2. Decision
- `BlobVersionId` is **optional**.
- Not used for change detection.
- If storage provides a version ID, it may be recorded but is ignored by ingestion logic.
- No dependency on blob storage versioning systems.

## 3. Rationale
- Simplifies ingestion.
- Avoids coupling indexing to storage-specific version semantics.

## 4. Resolved Questions
1. Required when storage supports versioning? → No.
2. Track timestamps? → No.
3. Useful for DocId/PointId? → No.
4. Blob replaced without versioning? → Handled by `ContentHash`.
