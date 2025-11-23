# IDX-008 – SymbolType Rules

**Status:** Accepted

## 1. Description
`SymbolType` describes **what kind of symbol** a chunk represents (e.g., file, class, method, component). It is especially important for code assets and is complementary to `Kind`/`SubKind`.

## 2. Decision
- `SymbolType` is **optional** for general assets but **required for source-code assets** (`Kind = Code`).
- Use **PascalCase** for `SymbolType` values (e.g., `File`, `Component`, `Method`, `Class`).
- For source-code assets, the top-level (file-level) chunk should always use `SymbolType = File`.
- Exactly **one** `SymbolType` per item (no arrays).
- `SymbolType` values are **free-form** for now; we may standardize them later.
- We do **not** require global uniqueness or enforce specific `Kind` + `SymbolType` combinations at runtime.
- Any internal chunker-specific symbol kinds should be mapped or aligned with this field as lightly as possible.

## 3. Rationale
- `SymbolType` enables structural queries like "show me all methods" or "show me all components".
- Making it required for code assets ensures consistent structural metadata.
- PascalCase keeps it visually aligned with other taxonomy fields.
- A special `File` value for file-level symbols provides a clear root for each document.

## 4. Resolved Questions
1. **Treat file-level chunks specially?**  
   Yes: `SymbolType = File`.
2. **Allow multiple symbol types per chunk?**  
   No.
3. **Global uniqueness of symbol type values?**  
   Deferred; not enforced now.
4. **Map internal chunker symbol labels to `SymbolType`?**  
   Yes, but keep mapping minimal and aligned.
5. **Pre-define `SymbolType` for non-code assets now?**  
   Deferred.
