# IDX-0061 – Definition of IRagPointFactory

**Status:** Accepted  
**Artifact Type:** C# Interface  
**Collection:** RAG Indexing & Vectorization DDRs

---

## 1. Title
**Definition of IRagPointFactory**

---

## 2. Purpose
Defines a minimal interface that guarantees an implementing class can create a fully-formed **`IRagPoint`** based solely on its internally owned state.

The factory:
- Holds all necessary context (normalized text, IDs, metadata, file paths).
- Performs validation rules specific to the artifact type.
- Returns an **`IEnumerable<InvokeResult<IRagPoint>>`** wrapping the created payload.

This pattern ensures consistent, self-contained payload generation across all RAG pipeline components.

---

## 3. Scope
This interface applies to any component responsible for constructing a **`IRagPoint`**, including:
- Normalized chunk builders
- Description builders
- Interface or class overview builders
- Method-level description generators
- Resource/RESX processors
- Higher-level indexing orchestrators

Each implementation is expected to know the rules, required fields, and validations applicable to its specific artifact type.

---

## 4. Requirements
### 4.1 Minimal surface area
- Single method.
- No parameters.
- Implementations must rely on pre-existing state.

### 4.2 No caller-provided context
- All state needed for payload construction must be owned and managed by the implementing class.

### 4.3 Output contract
- Returns a non-null **`IEnumerable<InvokeResult<IRagPoint>>`**.
- Payload must include stable identifying information and normalized text.

### 4.4 Internal validation behavior
- `CreateIRagPoints()` performs validation for the specific artifact type.
- Implementations know what fields are required vs. optional.
- Missing required fields should returned via InvokeResult or domain-appropriate errors.

### 4.5 No side effects
- No file writes, registry updates, or direct DB interactions.

---

## 5. Interface Specification
### Method Signature
```csharp
IEnumerable<InvokeResult<IRagPoint>> CreateIRagPoints();
```

### Inputs
None — factory owns all relevant context.

### Outputs
An `IEnumerable<InvokeResult<IRagPoint>>` representing a fully-formed vector payload.

---

## 6. Rules & Guarantees
1. Must not return null.
2. Must generate a list of structurally complete `IRagPoint`s.
3. Must validate all required inputs before payload creation.
4. Must ensure deterministic identifiers when applicable.
5. Must not perform I/O operations.

---

## 7. Example Implementation Skeleton
```csharp
using System;
using LagoVista.AI.Rag.Contracts;
using LagoVista.AI.Rag.Models;
using LagoVista.Core.Models;

namespace LagoVista.AI.Rag.Services
{
    public class ExampleRagPayloadFactory : IRagPointFactory
    {
        private readonly string _id;
        private readonly string _normalizedText;
        private readonly string _filePath;

        public ExampleRagPayloadFactory(string id, string normalizedText, string filePath)
        {
            _id = id;
            _normalizedText = normalizedText;
            _filePath = filePath;
        }

        public IEnumerable<InvokeResult<IRagPoint>> CreateVectorPayload()
        {
            if (string.IsNullOrWhiteSpace(_id))
                throw new InvalidOperationException("RagVectorPayload Id is required.");

            if (string.IsNullOrWhiteSpace(_normalizedText))
                throw new InvalidOperationException("Normalized text is required.");

            var payload = new RagVectorPayload
            {
                Id = _id,
                FilePath = _filePath,
                NormalizedText = _normalizedText
            };

            // Construct and return an InvokeResult<RagVectorPayload>
            throw new NotImplementedException();
        }
    }
}
```

---

## 8. Extension Notes
- Async variants may be needed if embedding or dynamic metadata resolution is added.
- Additional fields (e.g., token counts, domain/model identifiers) can be integrated.
- Implementations may provide diagnostic info or structured validation results.
