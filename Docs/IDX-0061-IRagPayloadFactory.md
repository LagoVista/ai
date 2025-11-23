# IDX-0061 – Definition of IRagPayloadFactory

**Status:** Accepted  
**Artifact Type:** C# Interface  
**Collection:** RAG Indexing & Vectorization DDRs

---

## 1. Title
**Definition of IRagPayloadFactory**

---

## 2. Purpose
Defines a minimal interface that guarantees an implementing class can create a fully-formed **`RagVectorPayload`** based solely on the state it already holds. No parameters are passed into the `Create()` method; instead, the implementer is responsible for possessing the necessary normalized text, metadata, and identifiers.

This ensures:
- Consistent payload creation
- Implementation-specific flexibility
- Strong decoupling between parsing, normalization, and vectorization

---

## 3. Scope
This interface applies to any component responsible for constructing a **`RagVectorPayload`**, including:
- Normalized chunk builders
- Description builders
- Artifact-specific processors
- Higher-level indexing orchestrators

---

## 4. Requirements
### 4.1 Minimal surface area
- Interface contains **one** method.
- No parameters.
- The implementation must rely on data it already holds.

### 4.2 No external context
- Callers do not supply input at creation time.
- Factories must fully own the data required to produce the payload.

### 4.3 Output contract
- Returns a non-null **`RagVectorPayload`**.
- Payload must contain stable identifying information and normalized text.

### 4.4 No side effects
- Factory creation should not write to disk or mutate external state.

---

## 5. Interface Specification
### Name
`IRagPayloadFactory`

### Namespace (recommended)
`LagoVista.AI.Rag.Contracts`

### Method
```csharp
RagVectorPayload Create();
```

### Inputs
None — the factory must own all required context.

### Outputs
A fully-formed **`RagVectorPayload`** ready for embedding or vector DB storage.

---

## 6. Rules & Guarantees
1. `Create()` must never return null.
2. Payload must be structurally complete.
3. Implementations must guarantee deterministic identifiers.
4. No I/O or side effects.

---

## 7. Example Implementation Skeleton
```csharp
using LagoVista.AI.Rag.Contracts;
using LagoVista.AI.Rag.Models;

namespace LagoVista.AI.Rag.Services
{
    public class DefaultRagPayloadFactory : IRagPayloadFactory
    {
        private readonly string _id;
        private readonly string _normalizedText;
        private readonly string _filePath;
        private readonly Dictionary<string, object> _metadata;

        public DefaultRagPayloadFactory(string id, string normalizedText, string filePath,
                                        Dictionary<string, object> metadata = null)
        {
            _id = id;
            _normalizedText = normalizedText;
            _filePath = filePath;
            _metadata = metadata ?? new Dictionary<string, object>();
        }

        public RagVectorPayload Create()
        {
            return new RagVectorPayload
            {
                Id = _id,
                FilePath = _filePath,
                NormalizedText = _normalizedText,
                Metadata = _metadata
            };
        }
    }
}
```

---

## 8. Notes for Future Extensions
- Async variants may be added if embedding logic moves inside the payload factory.
- Additional metadata (token count, domain, model identifiers) may be incorporated.
- Implementers may enforce validation rules before payload construction.
