# RAG Indexing Pipeline – Master Checklist

This document captures the agreed high-level steps required to take a directory of C# assets (and resource dictionaries) and index them into Qdrant for RAG.

It is intended to be a living, implementation-ready reference for the current architecture.

---

## 1. Load configuration
- Read `IngestionConfig` (OrgId, SourceRoot, repos, Qdrant, Embeddings, etc.)
- Decide which repository / repositories are being indexed

---

## 2. Discover files on disk
- Walk the repo under `SourceRoot`
- Include C# source + resource dictionaries
- Apply include / exclude rules
- Produce a flat list of discovered files with relative paths

---

## 3. Pre-scan: Build in-memory Domain / Model database
- Identify domain descriptor assets (e.g. classes marked with `[DomainDescriptor]`)
- Identify primary models associated with domains
- Extract:
  - Domain titles and taglines / descriptions
  - Model titles and taglines / descriptions
- Store as an in-memory catalog keyed by:
  - Domain
  - Model

This catalog is made available to the chunking and normalization stage.

---

## 4. Load the local index
- Read the repository-specific `LocalIndexStore` (JSON)
- Get the previous view of:
  - Indexed files
  - Stored hashes
  - Facets / metadata

---

## 5. Build an ingestion plan
Compare discovered files vs local index:
- New files → Full index
- Changed files → Re-index
- Missing files → Delete

Produce a `FileIngestionPlan` containing:
- `FilesToIndex`
- `DocsToDelete`

---

## 6. For each file to be indexed: build context + identity
- Create `IndexFileContext`:
  - OrgId
  - ProjectId
  - RepoId
  - FullPath
  - RelativePath
  - Metadata
- Create `DocumentIdentity`
- Compute and assign `DocId`

---

## 7. Chunk + summarize the file (using in-memory domain/model DB)
- Parse the C# / resource file into logical chunks
- For each chunk, build `NormalizedText` that includes:
  - Summary of what the chunk does
  - Domain title + tagline
  - Model title + tagline
- This gives each vector rich semantic context

---

## 8. Generate embeddings
For each normalized chunk:
- Call `IEmbeddingService` (OpenAI)
- Retrieve float vector for the chunk

---

## 9. Write vectors + payloads to Qdrant
For each chunk:
- Call `IQdrantVectorStore.IndexChunksAsync`
- Provide:
  - `DocumentIdentity`
  - Embedding vector
  - Normalized text
  - Facets (`Kind`, `SubKind`, `Domain`, `Model`, `Repo`, etc.)

Qdrant now contains searchable, semantically-rich vectors for this asset.

---

## 10. Update and persist local index PER FILE (crash-safe)
Immediately after each asset is successfully indexed:
- Update `LocalIndexRecord` with:
  - ContentHash
  - Reindex flag
  - Facets
  - `LastIndexedUtc`
- Save `LocalIndexStore` back to disk

This allows partial progress and safe resume if indexing is interrupted.

---

## 11. Handle deletions
For each item in `DocsToDelete`:
- Delete vectors in Qdrant using `DocId`
- Remove from:
  - LocalIndexStore
  - Registry (if used)
- Persist local index again

---

## End State
At the end of a run:
- Qdrant is synchronized with the current repo state
- LocalIndex reflects latest indexing state
- Unchanged files are skipped on the next run
- System is prepared for efficient RAG queries by your agents