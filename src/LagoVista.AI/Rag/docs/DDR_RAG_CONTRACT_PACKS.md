# DDR — RAG Contract Packs Refactor (Phase 1)

## Status
Active — Foundation contracts created, implementations to be migrated incrementally.

## Context
The `LagoVista.AI.Rag` project grew organically while we were experimenting with
indexing flows, Qdrant integration, and Roslyn-based chunking. Several areas of
concern became tightly coupled:

- File discovery and ingestion planning
- Local index persistence
- Indexing orchestration
- Chunking, embedding, and vector upsert
- Registry updates and inline manifest tracking

We now want to adopt a **Contract Pack** architecture that matches the design
we are using in the Chunkers project:

1. Define clear, small **models** and **interfaces** (contracts).
2. Implement behavior in specialized, testable services (static or instance).
3. Wire things together through a small number of orchestrator interfaces.

We are early enough in adoption that we are comfortable refactoring and even
breaking backward compatibility on a separate branch.

---

## Goals

1. Introduce clear contract boundaries for:
   - Orchestration (runs, pipelines, file contexts)
   - Ingestion (discovery + planning)
   - Local index access
   - Hashing
   - Quality checks (title/description review)

2. Decouple top-level orchestrators (like the legacy `IngestorService`) from:
   - Chunking and embedding
   - Vector database implementations (Qdrant)
   - Local index and registry policies

3. Prepare the codebase for future agent-driven control and Contract Pack
   driven prompts, where the AI only needs interfaces + models, not
   implementations.

---

## New Contract Packs (Phase 1)

This phase introduces interfaces and context models. Implementations are still
in their legacy locations and will be migrated in later phases.

### 1. Orchestration Contract Pack

**Namespace root:** `LagoVista.AI.Rag.ContractPacks.Orchestration`

**Interfaces:**
- `IIndexingPipeline`
  - Indexes a single file (`IndexFileContext`).
  - Owns chunking, embedding, and vector upsert.
  - Must not perform file discovery or planning.

- `IIndexRunOrchestrator`
  - Executes a full indexing run for a repo.
  - Uses `IFileDiscoveryService` + `IFileIngestionPlanner` + `ILocalIndexStore`.
  - Invokes `IIndexingPipeline` for each file.

**Models:**
- `IndexFileContext`
  - Carries OrgId, ProjectId, RepoId, FullPath, RelativePath, Language,
    DocumentIdentity, and a metadata dictionary.

- `MissingFileContext`
  - Carries OrgId, ProjectId, RepoId, RelativePath, and DocumentIdentity to
    represent deletions.

These models are the boundary between orchestration and concrete pipelines.

---

### 2. Ingestion Contract Pack

**Namespace root:** `LagoVista.AI.Rag.ContractPacks.Ingestion`

**Interfaces:**
- `IFileDiscoveryService`
  - Given a repo id, discover files and basic properties.
  - Returns `DiscoveredFile` objects.

- `IFileIngestionPlanner`
  - Given repo id, discovered file paths, and a `LocalIndexStore`, computes a
    `FileIngestionPlan` (which files to index/delete).

**Models:**
- `DiscoveredFile`
  - RepoId, FullPath, RelativePath, SizeBytes, IsBinary.

Existing classes like `FileWalker`, `FileDiscoveryService`, and
`FileIngestionPlanner` will be adapted to these interfaces.

---

### 3. Index Store Contract Pack

**Namespace root:** `LagoVista.AI.Rag.Models` (interface for now)

**Interfaces:**
- `ILocalIndexStore`
  - `LoadAsync(repoId)` → `LocalIndexStore`
  - `SaveAsync(repoId, LocalIndexStore)`
  - `GetAll(LocalIndexStore)` → `IReadOnlyList<LocalIndexRecord>`

Existing `LocalIndexStore` and `LocalIndexRecord` models will be unified and
wired through this interface.

---

### 4. Hashing Contract Pack

**Namespace root:** `LagoVista.AI.Rag.ContractPacks.Hashing`

**Interfaces:**
- `IContentHashService`
  - `ComputeFileHashAsync(fullPath)`
  - `ComputeTextHash(content)`

Existing helpers `ContentHashHelper` and `ContentHashUtil` will be consolidated
behind this interface, using the canonical DDR-defined hashing behavior.

---

### 5. Quality Contract Pack

**Namespace root:** `LagoVista.AI.Rag.ContractPacks.Quality`

**Interfaces:**
- `ITitleDescriptionReviewService`
  - `ReviewAsync(kind, symbolName, title, description)`
  - Returns `TitleDescriptionReviewResult` (defined in the Chunkers project).

This aligns with the static OpenAI-based review helper and allows the RAG
pipeline to depend only on the interface.

---

## Phase 1 vs Future Phases

**Phase 1 (this refactor):**
- Introduce interfaces and context models.
- Keep existing implementations (IngestorService, LocalIndex*, planners,
  QdrantIndexingPipeline, etc.) as-is.
- New code can begin targeting the contract pack interfaces.

**Phase 2 (next):**
- Migrate implementations to live under matching Contract Pack folders.
- Unify duplicate models and helpers (LocalIndex*, hashing helpers, planners).
- Convert all serialization to use Newtonsoft.Json, per project standard.

**Phase 3 (later):**
- Remove legacy entry points (like the original IngestorService) once the new
  orchestrator + pipeline contracts are fully wired.
- Stabilize the Contract Packs as the source of truth for agents and tools.

---

## Summary

This DDR formalizes the first step in refactoring LagoVista.AI.Rag into a
contract-first architecture. We introduce clear interfaces for orchestration,
ingestion, local index access, hashing, and quality review, without yet moving
existing implementations. Future phases will:

- Unify duplicate functionality
- Relocate implementations under the Contract Pack structure
- Remove legacy, tightly coupled orchestration

This document should be indexed into the vector database so future work and
agents can rely on the Contract Pack architecture as the canonical design.
