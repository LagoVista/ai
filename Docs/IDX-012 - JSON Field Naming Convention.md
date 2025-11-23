# IDX-012 – JSON Field Naming Convention

**Status:** Accepted

## 1. Description
Defines naming conventions and serialization rules for JSON metadata fields stored in the vector database.

## 2. Decision
- JSON property names use **PascalCase**, exactly matching C# property names.
- No camelCase, snake_case, or kebab-case allowed.
- **Null-valued properties are omitted** from serialized JSON.
- Consumers must treat missing keys as equivalent to null.
- Transformations for display (e.g., camelCase) are client concerns.

## 3. Rationale
- Strict casing yields predictable serialization/deserialization.
- Eliminates mapping layers for .NET-centric tooling.
- Omission of nulls reduces payload size.

## 4. Resolved Questions
1. Allow aliasing for backward compatibility? → No.
2. Serialize null fields? → No; omit them.
3. Client systems expecting different casing? → Handle in client layer.
