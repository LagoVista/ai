# IDX-0060 – Common `IEmbeddable` Interface

**Status:** Accepted  
**Artifact Type:** Interface  
**Domain:** RAG Indexing / Embedding Pipeline  
**Owner:** AI Indexer Subsystem  

---

## 1. Purpose

Define a minimal common interface implemented by any artifact that can be embedded into a vector store.

Assets that implement this interface must expose:

- `NormalizedText` — canonical embedding text  
- `EstimatedTokens` — approximate token count of the normalized text  
- `EmbeddingVectors` — an array of floats containing embeddings produced during the pipeline

The interface is intentionally minimal so it can be applied broadly across many DDR-defined artifact types.

---

## 2. Scope

This DDR covers:

- The `IEmbeddable` interface shape  
- Semantics of each property  
- How it is used by the embedding and indexing pipeline

Identity, source location, model metadata, etc. remain the responsibility of owning types and/or container models, not `IEmbeddable`.

---

## 3. Requirements

### 3.1 MUST

| Requirement        | Description |
|--------------------|-------------|
| NormalizedText     | Required, non-null, canonical text to embed. Getter-only. |
| EstimatedTokens    | Required, non-negative integer estimate of token count. Getter-only. |
| EmbeddingVectors   | Optional `float[]` populated by the embedding step. Getter + setter. |
| Serialization-safe | Types implementing `IEmbeddable` must serialize cleanly via Newtonsoft.Json and System.Text.Json. |

### 3.2 MUST NOT

- `IEmbeddable` MUST NOT define any identity property (no `Id`, `EmbeddingId`, `QualifiedName`, etc.).
- `IEmbeddable` MUST NOT include pipeline-specific metadata (model name, version, etc.).
- `IEmbeddable` MUST NOT contain behavior (methods); it is a pure data contract.

---

## 4. Interface Definition

The `IEmbeddable` interface is defined in `LagoVista.AI.Rag.Models` and exposes three properties:

- `string NormalizedText { get; }`
- `int EstimatedTokens { get; }`
- `float[]? EmbeddingVectors { get; set; }`

`NormalizedText` and `EstimatedTokens` are immutable from the perspective of the embedding pipeline; `EmbeddingVectors` is the only mutable property.

---

## 5. Pipeline Integration

Typical flow:

1. DDR builders (ModelMetadataDescription, InterfaceOverview, etc.) produce artifacts implementing `IEmbeddable`.  
2. The embedding service iterates over `IEmbeddable` instances and populates `EmbeddingVectors` based on `NormalizedText`.  
3. The index writer (e.g., Qdrant writer) reads `NormalizedText`, `EstimatedTokens`, and `EmbeddingVectors`, plus any owning-type-specific metadata, and persists them.

This keeps the embedding pipeline generic while allowing each DDR artifact to carry its own richer metadata outside the interface.
