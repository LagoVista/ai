# IDX-009 – ComponentType & ComponentName Rules

**Status:** Preliminary

## 1. Description
`ComponentType` and `ComponentName` are specialized fields for **component-oriented artifacts**, typically UI or library components.

They complement `SymbolType`:
- `SymbolType` describes the **programming construct** or symbol role (e.g., `File`, `Class`, `Method`, `Component`).
- `ComponentType` / `ComponentName` apply only when the artifact is a *component* and provide semantic UI/library classification inside that subset.

Example for a UI component chunk:
- `SymbolType = Component` (or `ComponentFile`).
- `ComponentType = primitive` or `composite`.
- `ComponentName = SliderComponent`.

If the artifact is not a component (e.g., a service or model), `ComponentType` / `ComponentName` remain unset.

## 2. Decision
- Introduce `ComponentType` (string) with **allowed values**:
  - `primitive` — basic building-block component.
  - `composite` — component built from primitives and/or other composites.
  - `other` — component that does not neatly fit primitive/composite.
- `ComponentType` values are all **lowercase** as shown.
- Introduce `ComponentName` (string) as the **PascalCase symbol name** for the component (e.g., `SliderComponent`, `Button`).
- Populate these fields **only** when `Kind` indicates the artifact is a component (e.g., component-oriented `Kind`/`SubKind`). For non-component artifacts, leave them null/omitted.
- If ingestion cannot reliably determine the type, default `ComponentType` to `"other"`.
- `ComponentName` should match the actual code symbol name as closely as possible for traceability.

## 3. Rationale
- Distinguishing `primitive` vs `composite` components enables richer UI-specific filtering (e.g., library introspection, design system views).
- Aligning `ComponentName` with the real symbol name simplifies linking between code, docs, and design assets.
- Restricting `ComponentType` to three values prevents uncontrolled taxonomy growth while still modeling key distinctions.
- Keeping these fields optional for non-component artifacts avoids cluttering other asset types.

## 4. Resolved Questions
1. **Extend `ComponentType` beyond `primitive`, `composite`, `other`?**  
   Not initially; start with these three.
2. **Composite components containing primitives – what `ComponentType`?**  
   `composite`.
3. **Should `ComponentName` include namespaces or path prefixes?**  
   No; use the symbol name only.
4. **Platform-agnostic components (used in multiple contexts) – where to capture that?**  
   In other metadata fields (e.g., `Domain`, `Layer`), not `ComponentType`.
5. **For non-component `Kind` values (services, models, etc.), should these fields appear?**  
   No; they should be omitted/null.
