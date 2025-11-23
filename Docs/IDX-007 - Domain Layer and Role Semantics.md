# IDX-007 – Domain / Layer / Role Semantics

**Status:** Accepted

## 1. Description
`Domain`, `Layer`, and `Role` are orthogonal axes that describe *where* and *how* an artifact fits into the system architecture:
- `Domain`: bounded context or functional area (e.g., `UI`, `Backend`, `Docs`, `Integration`).
- `Layer`: architectural layer or tier (e.g., `Primitives`, `Composites`, `Implementation`, `Infrastructure`).
- `Role`: responsibility or purpose within the domain and layer (e.g., `Component`, `Service`, `Style`, `RagMetadata`).

## 2. Decision
- `Domain`, `Layer`, and `Role` are **optional** for non-code assets, but must be populated for **code-type assets**.
- Naming convention for all three fields is **PascalCase** (e.g., `UI`, `Backend`, `Component`, `Service`).
- Values are **free-form** at this stage; no enforced controlled vocabulary.
- Only a **single** `Role` value per item (no arrays).
- `Layer` is free-form; we do not enforce a strict order or fixed set.
- `Role` values may be reused across different domains.
- Ingestion logic for deriving these values is deferred; no runtime validation yet.

## 3. Rationale
- These fields provide rich architectural context for code assets without bloating non-code metadata.
- PascalCase keeps metadata conventions consistent with other fields.
- Free-form values provide flexibility while we learn how domains, layers, and roles are actually used.
- A single `Role` per item keeps classification straightforward.

## 4. Resolved Questions
1. **Controlled vocabulary now?**  
   No; free-form for now.
2. **Multiple roles per artifact?**  
   No; single role only.
3. **How are values derived?**  
   Derivation logic is deferred; current DDR only defines the fields.
4. **Is `Layer` ordered or ordinal?**  
   No; it is descriptive/free-form.
5. **Are role names globally unique?**  
   No; they may be reused across domains.
