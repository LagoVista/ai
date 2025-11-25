# IDX-038 – Model Metadata & UI Description (Metadata Chunk)

**Status:** Accepted

## 1. Description
Defines the **metadata/UI description** (`ChunkFlavor = 'Metadata'`) for Model entities, including field-level UI/validation semantics and multi-variant layouts.

## 2. Decision
### Top-Level
- `ModelName`
- `Namespace`
- `Domain`
- `ResourceLibrary`
- `Fields[]`
- `Layouts`

### Field Schema
Identity, label/help text, validation, data type, picker semantics, file/media metadata, advanced AI metadata, etc.

### Layouts
Derived from the various `IForm*` descriptor interfaces:
- Form columns, tabs, bottom sections
- Advanced layouts
- Inline fields
- Mobile
- Simple
- Quick-create
- Additional Actions

### FormAdditionalAction
Each action includes Title, Icon, Help, Key, ForCreate, ForEdit.

## 3. Rationale
Captures the complete UI/UX contract for a model, enabling auto-form generation and consistent RAG usage.
