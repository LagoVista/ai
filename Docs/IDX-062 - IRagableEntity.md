# IDX-0062 – IRagableEntity Marker Interface

**Status:** Draft  
**Artifact Type:** C# interface

## 1. Title

IRagableEntity – Combined Embeddable plus RAG Payload Contract

## 2. Purpose

Define a single, minimal interface that marks an entity as both embeddable and able to create a RAG payload for a vector store.

IRagableEntity unifies:

- IDX-0060 IEmbeddable
- IDX-0061 IRagPayloadFactory

Any type implementing IRagableEntity is considered RAG ready.

## 3. Relationship To Other DDRs

- **IDX-0060 – Common IEmbeddable Interface**  
  IRagableEntity inherits the embeddability contract from IEmbeddable.

- **IDX-0061 – IRagPayloadFactory**  
  IRagableEntity inherits the payload creation contract from IRagPayloadFactory.

IRagableEntity is a composite marker that brings both contracts together.

## 4. Design Overview

IRagableEntity is intentionally minimal and introduces no new members. It simply composes:

- IEmbeddable (normalized text, token estimate, embedding vector)
- IRagPayloadFactory (method to create a RagVectorPayload)

This keeps the surface area small and encourages reuse of the existing abstractions.

### 4.1 Interface Sketch

The interface is defined in the shared AI interfaces assembly and will inherit both contracts:

```csharp
namespace LagoVista.Core.AI.Interfaces
{
    /// <summary>
    /// IDX-0062 – IRagableEntity marker interface.
    ///
    /// Combines:
    /// - IDX-0060 IEmbeddable (normalized text, token estimate, embedding vector)
    /// - IDX-0061 IRagPayloadFactory (CreateRagPayload method)
    ///
    /// Any entity implementing this interface is considered RAG ready:
    /// it can be embedded and can produce a RagVectorPayload for indexing.
    /// </summary>
    public interface IRagableEntity : IEmbeddable, IRagPayloadFactory
    {
    }
}
```

> Note: The exact signatures of IEmbeddable and IRagPayloadFactory are defined in IDX-0060 and IDX-0061. IRagableEntity only composes them.

## 5. Usage Patterns

### 5.1 Indexing Pipeline

Indexing code can accept IRagableEntity as a single abstraction:

- Use IEmbeddable.NormalizedText for embedding generation when needed.
- Use IEmbeddable.EmbeddingVector to store or reuse embeddings.
- Use IRagPayloadFactory.CreateRagPayload to obtain the RagVectorPayload for the vector database.

### 5.2 Domain Models

Domain entities that participate in RAG can implement IRagableEntity directly so they are both embeddable and able to produce their own payloads.

## 6. Non Goals

- Does not define where or how embeddings are stored.
- Does not define serialization or transport details of RagVectorPayload.
- Does not impose persistence or lifecycle semantics.

## 7. Future Extensions

If additional RAG related behaviors are needed later, they should be added as separate interfaces and composed as needed rather than extending IRagableEntity. This keeps IRagableEntity small and widely usable.
