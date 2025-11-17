# Vector Chunk Metadata Contract

This contract describes the metadata payload stored alongside each embedded chunk in the vector database.

All properties are **required at the JSON level**, but many are allowed to be `null`. This gives a stable, predictable shape for ingestion and querying.

## Overview

Each record represents a *chunk* of a source document, scoped to a project and organization, with enough metadata to:

- Reconstruct source context (file path, line range, repo, commit, etc.)
- Classify the content (kind, domain, layer, role, component metadata, etc.)
- Track versioning and indexing details (index version, embedding model, timestamps, hashes)
- Support filtering and ranking (priority, labels, type flags like `IsDemo`, `IsRagMetadata`)

## Fields

| Name             | Type                    | Nullable | Description |
|------------------|-------------------------|----------|-------------|
| `OrgId`          | string                  | no       | Organization identifier (typically a GUID or similar). |
| `ProjectId`      | string                  | no       | Project identifier within the organization. |
| `DocId`          | string                  | no       | Logical document identifier (e.g., a stable ID per source file). |
| `Kind`           | string                  | no       | Classification of the chunk/file (e.g. `primitiveComponent`, `primitiveStyle`, `guidance`, etc.). |
| `ContentTypeId`  | string                  | no       | Internal ID for the content type (e.g. maps to a content type registry). |
| `ContentType`    | string                  | no       | Human-readable content type (e.g. `typescript`, `html`, `sass`, `markdown`). |
| `Subtype`        | string                  | yes      | Optional subtype for more granular classification (e.g. `demo`, `rag`, `style`). |
| `SectionKey`     | string                  | no       | Logical section name within the document. Currently fixed to `"file"`. |
| `PartIndex`      | integer                 | no       | 0-based index of this chunk within the document. |
| `PartTotal`      | integer                 | no       | Total number of chunks produced for this document. |
| `Title`          | string                  | no       | Title or display name of the document or symbol (often the component name). |
| `Language`       | string                  | yes      | Language or locale (e.g. `en`, `en-US`), if applicable. |
| `Priority`       | integer                 | no       | Relative importance for retrieval/ranking (higher = more important). Currently default `3`. |
| `Audience`       | string                  | yes      | Intended audience segment, if used (e.g. `developer`, `designer`). |
| `Persona`        | string                  | yes      | Persona label for more specific targeting (e.g. `frontend-engineer`, `qa-analyst`). |
| `Stage`          | string                  | yes      | Lifecycle stage (e.g. `draft`, `beta`, `stable`) when applicable. |
| `LabelSlugs`     | string[]                | no       | Human-readable label slugs for filtering/faceting (e.g. `["primitive","button"]`). |
| `LabelIds`       | string[]                | no       | Internal label IDs corresponding to `LabelSlugs`. |
| `BlobUri`        | string                  | no       | URI to the stored source blob (usually a path within storage or repo). |
| `BlobVersionId`  | string                  | yes      | Optional version ID for the blob (e.g. storage version or ETag). |
| `SourceSha256`   | string                  | no       | SHA-256 hash of the original source content used for this chunk. |
| `LineStart`      | integer                 | no       | 1-based start line number of the chunk in the source file. |
| `LineEnd`        | integer                 | no       | 1-based end line number of the chunk in the source file. |
| `CharStart`      | integer                 | yes      | Optional 0-based character offset start within the source, if tracked. |
| `CharEnd`        | integer                 | yes      | Optional 0-based character offset end within the source, if tracked. |
| `Symbol`         | string                  | no       | Primary symbol name represented by this chunk (often the component name). |
| `SymbolType`     | string                  | no       | Symbol classification, e.g. `"component"` or `"component-file"`. |
| `HtmlAnchor`     | string                  | yes      | Optional HTML anchor (fragment) for deep linking into rendered docs. |
| `PdfPages`       | integer[]               | yes      | Optional list of PDF page numbers this chunk maps to, if applicable. |
| `IndexVersion`   | integer                 | no       | Version of the indexing pipeline/contract (e.g. `2`). |
| `EmbeddingModel` | string                  | no       | Name of the embedding model used (e.g. `text-embedding-3-large`). |
| `ContentHash`    | string                  | no       | Hash of the chunk text content (post-chunking) for idempotency. |
| `ChunkSizeTokens`| integer                 | yes      | Approximate token length of the chunk when embedded. |
| `OverlapTokens`  | integer                 | yes      | Number of overlapping tokens with adjacent chunks (if using sliding windows). |
| `ContentLenChars`| integer                 | no       | Length of `chunk.text` in characters. |
| `IndexedUtc`     | string (ISO-8601)       | no       | Timestamp (UTC) when this chunk was indexed. |
| `UpdatedUtc`     | string (ISO-8601)       | yes      | Timestamp (UTC) when underlying source was last updated after indexing, if known. |
| `SourceSystem`   | string                  | no       | Identifier for the originating system (e.g. `nuvos-repo-indexer`). |
| `SourceObjectId` | string                  | no       | System-specific ID for the source object (often identical to `DocId`). |
| `Repo`           | string                  | no       | Repository URL or identifier. |
| `RepoBranch`     | string                  | no       | Branch name from which the content was indexed. |
| `CommitSha`      | string                  | yes      | Commit SHA at which the content was captured. |
| `Path`           | string                  | no       | Normalized path to the source file, starting with `/`. |
| `StartLine`      | integer                 | no       | Alias of `LineStart` (redundant for convenience). |
| `EndLine`        | integer                 | no       | Alias of `LineEnd` (redundant for convenience). |
| `Domain`         | string                  | no       | High-level domain or bounded context (e.g. `ui`, `backend`, `docs`). |
| `Layer`          | string                  | no       | Architectural or design layer (e.g. `primitives`, `composites`, `playground`). |
| `Role`           | string                  | no       | The role/purpose of the file within the system (e.g. `component`, `style`, `rag-metadata`). |
| `ComponentType`  | string                  | no       | Component classification: `"primitive"`, `"composite"`, or `"other"`. |
| `ComponentName`  | string                  | yes      | Name of the component associated with this chunk, if any. |
| `IsDemo`         | boolean                 | no       | `true` if the chunk originates from demo/playground files. |
| `IsRagMetadata`  | boolean                 | no       | `true` if the chunk contains RAG metadata (e.g. `*.rag.json`). |

### SymbolType Rules

- If `Kind` is a primitive or composite kind, `SymbolType = "component"`.
- Otherwise, `SymbolType = "component-file"`.

### ComponentType Rules

- If `Kind` is a primitive kind ⇒ `ComponentType = "primitive"`.
- If `Kind` is a composite kind ⇒ `ComponentType = "composite"`.
- Otherwise ⇒ `ComponentType = "other"`.
