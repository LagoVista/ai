# IDX-037 – Model Structure Description (Structured Chunk)

**Status:** Accepted

## 1. Description
Defines the **structured** (`ChunkFlavor = 'Structured'`) representation for Models, capturing identity, relationships, operational affordances, and high-level structure.

## 2. Decision
### Identity
- `ModelName`
- `Namespace`
- `QualifiedName`
- `Domain`

### Human Text
- `Title`
- `Help`
- `Description`

### Operational Affordances
- `Cloneable`
- `CanImport`
- `CanExport`

### UI URLs
- `ListUIUrl`, `EditUIUrl`, `CreateUIUrl`, `HelpUrl`

### API URLs
- CRUD endpoints (`InsertUrl`, `SaveUrl`, `GetUrl`, etc.)

### Structural Components
- `Properties[]`
- `EntityHeaderRefs[]`
- `ChildObjects[]`
- `Relationships[]`

### Construction
Derived from:
- `[EntityDescription]`
- `[FormField]`, `[LabelResource]`, `[HelpResource]`, `[FKeyProperty]`
- Resource dictionaries
- Reflection/semantic analysis

## 3. Rationale
Provides LLMs with a complete semantic overview of a model suitable for reasoning, documentation, and code generation.
