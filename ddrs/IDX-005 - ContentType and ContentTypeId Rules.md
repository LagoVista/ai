# IDX-005 – ContentType / ContentTypeId Rules

**Status:** Accepted

## 1. Description
`ContentType` and `ContentTypeId` classify each document or chunk at a **technical** level, describing the nature of the content (e.g., source code vs domain docs). They sit alongside `Kind`/`SubKind` and are used for coarse-grained filtering.

## 2. Decision
- Define `ContentTypeId` as an **integer enum** (`RagContentType`).
- Define `ContentType` as the **string name** of that enum value, using the same label.
- Both `ContentTypeId` and `ContentType` are **required** for all items.
- These fields represent higher-level, semantic categories (e.g., `SourceCode`, `DomainDocument`) rather than extension-level distinctions.
- `ContentType` uses **PascalCase**.
- Exactly **one** content type per item (no multi-valued lists).
- Do **not** include version modifiers or variants in the `ContentType` string (no `SourceCodeV2`).
- Maintain the existing set of enum values (e.g., `Unknown = 0`, `DomainDocument = 1`, `SourceCode = 2`, …); once a value is published, treat it as stable.

## 3. Rationale
- Pairing an integer enum with a descriptive string balances efficient querying with readability.
- Semantic categories avoid an explosion of types mapped one-to-one to file extensions.
- PascalCase naming is consistent with C# and metadata conventions.
- Limiting each item to one content type removes ambiguity during querying.

## 4. Resolved Questions
1. **Initial value set?**  
   Defined centrally via the `RagContentType` enum.
2. **Should content types reflect semantics or raw extensions?**  
   Semantics (e.g., `SourceCode`) rather than per-extension labels.
3. **Multiple content types per item?**  
   No.
4. **Include versions/variants in `ContentType`?**  
   No.
5. **Naming convention for `ContentType`?**  
   PascalCase.
