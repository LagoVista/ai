# IDX-023 – ExternalId Field Strategy

**Status:** Accepted

## 1. Description
`ExternalId` provides an optional mapping between an indexed artifact and an external system, URL, or debugging reference.

## 2. Decision
- `ExternalId` is **optional** and may be null.
- No specific format or pattern is required.
- Uniqueness is not required.
- Downstream clients may use it for deep-linking but must not rely on it for core indexing logic.
- Does not participate in versioning, hashing, or change-detection logic.

## 3. Rationale
- Maintains flexibility for external integrations without burdening core ingestion.
- Keeps contract simple by making field optional and non-semantic.

## 4. Notes
Future requirements may extend this field to support required deep-linking or standardized locator formats.
