# IDX-010 – LabelSlugs & LabelIds Semantics

**Status:** Accepted

## 1. Description
`LabelSlugs` and `LabelIds` provide a flexible, tag-based metadata system for classification, faceted filtering, and search enrichment. These fields allow human-friendly labels alongside stable internal identifiers.

- **LabelSlugs** – user-friendly lowercase hyphenated tags.
- **LabelIds** – stable internal codes mapped 1-to-1 with the slugs.

Both lists are required but may be empty.

## 2. Decision
- `LabelSlugs` and `LabelIds` **must be present** (non-null), but may be empty lists.
- Tag format:
  - `LabelSlugs`: lowercase, hyphen-separated (e.g., `ui-component`, `backend-service`).
  - `LabelIds`: uppercase alphanumeric IDs with prefix (e.g., `LBL001`).
- Lists must remain **aligned by index** (slug ↔ ID mapping).
- Order is preserved (though queries treat them as sets).
- Values are **free-form** for now; no controlled vocabulary yet.
- No duplicates allowed within each list.
- Do not allow hierarchical slugs (`ui/component/button`). Flat only.
- No maximum label count at this stage.
- `LabelIds` are scoped per project/org, **not globally unique**.

## 3. Rationale
- Tagging enables rich search dimensions beyond structural metadata.
- Hyphenated lowercase slugs avoid stylistic drift.
- Stable internal IDs support future taxonomy tooling.
- Requiring both fields ensures consistent ingestion behavior.
- Flat tags reduce ontological complexity.

## 4. Resolved Questions
1. Restrict slugs to a controlled vocabulary? → No.
2. Maximum label count? → No.
3. Global uniqueness for `LabelIds`? → No.
4. Allow hierarchical tags? → No.
5. Track label timestamps or versions? → No.
