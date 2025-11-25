# DDR — Chunker Service Pattern (Contract-First + Static Helpers)

## Status
Approved — for immediate use in `LagoVista.AI.Rag.Chunkers`

## Context
We are building a large-scale source-code indexing and RAG pipeline. The system must be:
- Highly testable
- Deterministic
- Modular and evolvable
- Easy for an LLM ("AptIx") to collaborate on via contract-first patterns

To achieve this, we established a consistent pattern for new functionality in the **chunker project**.

---

## Design Decision
All new functionality should follow a **three-layer pattern**:

1. **Define Models**
   - Create strongly-typed DTOs / value objects / enums for inputs and outputs
   - These types define the "world" the service operates in
   - Examples:
     - `DomainSummaryInfo`
     - `ModelMetadataDescription`
     - `ModelStructureDescription`
     - `MethodSummaryContext`
     - `SummaryObjectKind`

2. **Create Specialized Static Classes**
   - One responsibility only
   - Pure / deterministic where possible
   - Easy to unit-test in isolation
   - Examples:
     - `DomainDescriptorParser` → Extracts domains from source
     - `ModelMetadataBuilder` → Builds metadata for models
     - `MethodSummaryBuilder` → Generates method summaries
     - `TitleDescriptionNormalizer` → Cleans titles/descriptions

3. **Expose via Interface (Service Layer)**
   - Top-level abstraction that higher layers call
   - Internally delegates to the static helpers
   - Enables mocking, swapping, orchestration
   - Examples:
     - `IChunkerServices`
     - `ITitleDescriptionReviewService`

Concrete services (e.g. `ChunkerServices`) simply orchestrate static helpers.

---

## Why Use Interfaces?
Static methods are preferred for low-level deterministic logic, but interfaces are required for:

- Mocking in unit and integration tests
- Swapping implementations (`Stub` vs `OpenAI`, local vs cloud)
- Orchestration by higher-level agents
- Future pipeline chaining

**Rule:** If it may be mocked or swapped → use an interface.
If it is deterministic and internal → static is preferred.

---

## Example Interface Contract

```csharp
using LagoVista.AI.Rag.Chunkers.Models;
using System.Collections.Generic;

namespace LagoVista.AI.Rag.Chunkers.Services
{
    public interface IChunkerServices
    {
        ModelMetadataDescription BuildMetadataDescriptionForModel(
            string sourceText,
            string relativePath,
            IReadOnlyDictionary<string, string> resources);

        ModelStructureDescription BuildStructuredDescriptionForModel(
            string sourceText,
            string relativePath,
            IReadOnlyDictionary<string, string> resources);

        string BuildSummaryForMethod(MethodSummaryContext ctx);

        IReadOnlyList<DomainSummaryInfo> ExtractDomains(
            string source,
            string filePath);
    }
}
```

This interface defines the full expected surface area without exposing internal mechanics.

---

## Class Placement Conventions

| Type | Folder |
|------|------|
| Models / DTOs | `/LagoVista.AI.Rag.Chunkers/Models/` |
| Interfaces | `/LagoVista.AI.Rag.Chunkers/Interfaces/` |
| Concrete services | `/LagoVista.AI.Rag.Chunkers/Services/` |
| Static helper classes | `/LagoVista.AI.Rag.Chunkers/Services/Internals/` (or subfolders) |
| Design / DDR docs | `/docs/` |

---

## Serialization Standard
All C# code must use **Newtonsoft.Json (Json.NET)** going forward for:

- OpenAI calls
- Payload handling
- Exporting structured RAG metadata

This is now a **global project rule**.

---

## Benefits of This Pattern

1. Clear contracts for LLM + humans
2. Highly testable individual logic
3. Minimal coupling between layers
4. Swappable implementations
5. Future-proof for orchestration + agents
6. Super clean for code generation workflows

This pattern is now considered **standard for all chunker-related development**.

---

## Version
**DDR ID:** CHUNKER-001
**Created:** 2025-11-21
**Status:** Active / Adopted

---

"Static where possible. Abstract where necessary. Contract-first always."