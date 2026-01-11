# Metadata
ID: IDX-000073
Title: RAG Indexing Pipeline
Type: Referential
Summary: Defines the RAG indexing pipeline steps and contracts for processing a single source file into one or more vector points, focusing on when fields are populated rather than implementation details.
Status: Draft
Creator: Kevin Wolf (SLSYS)
References: none
Creation Date: 1/11/2026, 12:40:43 PM
Last Updated Date: 1/11/2026, 12:40:43 PM
Last Updated By: Kevin Wolf (SLSYS)
Needs Human Confirmation: No
Needs Human Confirmation Reason: -

# Body
## 1 - Overview
Summary: Establishes the purpose, scope, and non-goals of the RAG indexing pipeline DDR.
Purpose
- Provide a clear, shared understanding of the required indexing pipeline steps.
- Define the data structures passed through the pipeline and identify at what stage which fields are populated.

Scope
- Defines the pipeline steps and the contracts between them (context + payload).
- Defines point schema “must-haves” at the contract level (e.g., required URL fields, parent linkage), without prescribing vendor-specific index/storage configuration.

Non-goals
- Detailed implementation of vector database operations, embedding provider selection, storage engine configuration, or chunking/embedding algorithms.

## 2 - IndexingPipelineContext + Step Interface Shape
Summary: Defines the high-level context object passed between steps and the standard async step interface used by the indexing pipeline.
Core context shape (initial)
- IndexingPipelineContext contains:
  - WorkItem (mutable): the evolving “thing being indexed”.
  - Resources (read-only): lookup/reference data (no services).
- The pipeline processes exactly one source file per run; the file content is text (source code) and is provided to the pipeline.
- Shared file-level fields are promoted to the context top level:
  - FullSource
  - FullSourceUrl

WorkItem (initial fields)
- PointId: generated identifier for the vector DB point (GUID; no semantic meaning).
- Vector: embedded float array (populated by Embed).
- RagPayload: full payload (populated progressively at step level).
- Lenses: EntityIndexLenses containing:
  - EmbedSnippet
  - ModelSummary
  - UserDetail
  - CleanupGuidance (optional)
  - FullSourceFile
  - SymbolText

Iteration and expansion model
- Work items are exposed read-only externally (e.g., IReadOnlyList), with internal mutation controlled by the pipeline.
- Some steps expand work items (e.g., segmentation) by cloning an existing work item (deep copy) and adding a child work item.
- Child work items link to their parent via RagPayload.ParentPointId.

Step interface shape (conceptual)
- All steps are asynchronous.
- All steps return InvokeResult.
- Steps mutate the context/work items in place.
- Steps may use pre/post validation, logging, and cancellation patterns consistent with the provided pipeline base class approach.
- Services are not passed through the context; steps resolve services via DI.

## 3 - Step: PersistSourceFile
Summary: Persists the full source file content to durable storage and records a stable URL on the context for downstream steps.
Preconditions / Inputs
- Context.FullSource is populated (file text is provided to the pipeline).
- Resources.PlannedFile is populated (one file).

Responsibilities
- Persist/upload the full file content.
- Populate Context.FullSourceUrl.

Outputs / Mutations
- Context.FullSourceUrl is populated (non-empty).

Non-goals
- No hashing/SHA computation (explicitly deferred).
- No symbol extraction, categorization, segmentation, embedding, or upsert.

## 4 - Step: ExtractSymbols
Summary: Parses the full source file and produces one work item per extracted symbol, each with symbol-scoped text and a generated PointId.
Preconditions / Inputs
- Context.FullSource and Context.FullSourceUrl are populated.
- Resources.PlannedFile is populated.

Responsibilities
- Extract symbols from the file (e.g., classes, enums; potentially multiple per file).
- Create one work item per symbol.

Outputs / Mutations
For each symbol work item:
- WorkItem.PointId is generated (GUID).
- WorkItem.Lenses.SymbolText is populated (required).
- Optional symbol metadata may be set if available (e.g., symbol name/type and line range).
- DocId is not populated in this step.

## 5 - Step: CategorizeContent
Summary: Determines classification for each work item using symbol text and file path, setting both ContentTypeId and Subtype.
Preconditions / Inputs
- Work items exist with Lenses.SymbolText.
- File path is available via planning/source metadata.

Responsibilities
- Set baseline content classification for source code.
- Derive Subtype from symbol text + file path.

Outputs / Mutations
For each work item:
- RagPayload.Meta.ContentTypeId is set to SourceCode.
- RagPayload.Meta.Subtype is set (derived).
- RagPayload.Meta.ContentType may be set to the string label (optional).

## 6 - Step: SegmentContent
Summary: Adds segment work items by cloning existing work items (deep copy) and linking children to parents via ParentPointId, without replacing the original item.
Responsibilities
- Optionally split a symbol into smaller segments suitable for embedding.
- Never replace the original work item; only augment and/or add child work items.

Outputs / Mutations
- Segmenter may add child work items by cloning a parent work item (deep copy) and adding it.
- For each child work item:
  - RagPayload.ParentPointId is set to the parent PointId.
  - Lenses.EmbedSnippet is populated for the segment (required).
  - Meta.SectionKey, Meta.PartIndex, Meta.PartTotal, and Meta.SemanticId are set as determined by the segmenter (segment-type dependent).

## 7 - Step: BuildDescription
Summary: Produces the lens outputs for each work item, including the single embedding input text (EmbedSnippet), and populates additional payload fields as needed.
Responsibilities
For each work item:
- Populate lens fields:
  - Lenses.EmbedSnippet (required; the only embedding text)
  - Lenses.ModelSummary (required)
  - Lenses.UserDetail (required)
  - Lenses.CleanupGuidance (optional; only when cleanup is necessary)
- Populate additional RagPayload fields as necessary for downstream steps (details evolve).

## 8 - Step: UploadContent
Summary: Uploads/persists per-work-item artifacts and populates required URL fields on the payload for model, human, and symbol retrieval.
Required URL fields (per work item)
- RagPayload.Extra.ModelContentUrl (required)
- RagPayload.Extra.HumanContentUrl (required)
- RagPayload.Extra.SymbolContentUrl (required; persisted SymbolText)

Issues (conditional)
- If issues exist:
  - set RagPayload.Extra.IssuesContentUrl
  - set RagPayload.Meta.HasIssues = true

## 9 - Step: Embed
Summary: Generates the embedding vector for each work item from EmbedSnippet and records the embedding model used.
Outputs / Mutations (per work item)
- WorkItem.Vector is populated (required).
- RagPayload.Meta.EmbeddingModel is populated (required).

## 10 - Step: StoreUpsertPoint
Summary: Upserts each work item into the vector database as a point containing PointId, Vector, and RagPayload.
Preconditions / Inputs (per work item)
- WorkItem.PointId populated.
- WorkItem.Vector populated.
- Required URLs populated on payload:
  - Extra.ModelContentUrl
  - Extra.HumanContentUrl
  - Extra.SymbolContentUrl
- If derived/segmented: RagPayload.ParentPointId populated.

Responsibilities
- Upsert point:
  - Id = WorkItem.PointId
  - Vector = WorkItem.Vector
  - Payload = RagPayload

Optional (implementation note, still vendor-agnostic)
- The vector DB integration may remove previous points by DocId (stable per symbol/work item) before upserting.

## 11 - C# Artifacts Produced
Summary: Defines the C# contracts produced alongside this DDR to standardize the indexing pipeline.
Artifacts (C# 8.0, .NET Standard 2.1)
- IndexingPipelineContext, IndexingWorkItem, IndexingResources
- IndexingPipelineSteps enum
- EntityIndexLenses
- IIndexingPipelineStep
- Supporting helper on context to clone-and-add child work items via deep copy (used by segmentation)

# Approval
Approver:
Approval Timestamp:
