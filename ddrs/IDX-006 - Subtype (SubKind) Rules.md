# IDX-006 – Subtype (SubKind) Rules

**Status:** Accepted

## 1. Description
This DDR refines the rules for the **`SubKind`** field introduced in IDX-004. `SubKind` provides a second-level classification beneath `Kind`, describing more specific structural or behavioral roles (e.g., `Manager`, `Repository`, `Model`, `PrimitiveComponent`).

## 2. Decision
- `SubKind` is a **required** metadata field for every item (alongside `Kind`).
- Use **PascalCase** for all `SubKind` values (e.g., `PrimitiveComponent`, `DomainObject`).
- Treat `SubKind` values as **free-form strings** initially; governance and a registry come later.
- Exactly **one** `SubKind` per item (no arrays). If needed, use a value like `None` rather than leaving it empty.
- `SubKind` values are **not required to be globally unique**; the same value may appear under different `Kind`s.
- Disallow version indicators or variant suffixes inside `SubKind` (no `ManagerV2`).
- Do **not** enforce runtime validation of allowed `Kind` + `SubKind` combinations at this stage.

## 3. Rationale
- Requiring `SubKind` prevents overly coarse metadata that only specifies `Kind`.
- PascalCase keeps the taxonomy aligned with .NET naming style and makes patterns easier to see.
- Free-form values allow rapid evolution; a formal glossary can be layered on later.
- Avoiding embedded versioning in `SubKind` keeps names stable and easier to search.

## 4. Resolved Questions
1. **Glossary/formal registry now or later?**  
   Deferred; free-form for now.
2. **Versioning in `SubKind`?**  
   Not allowed (no `V2` suffixes).
3. **Is `SubKind` optional?**  
   No; must be populated (with `None` if necessary).
4. **Global uniqueness of `SubKind` values?**  
   No; reuse is allowed.
5. **Runtime enforcement of valid `Kind` + `SubKind` combos?**  
   Not enforced in this phase.
