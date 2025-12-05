# IDX-072 — Domain Description DDR

**ID:** IDX-072  
**Title:** Domain Description  
**Status:** Approved  
**Owner:** Kevin Wolf & Aptix  
**Scope:** Defines the canonical Domain Description pipeline for RAG, including builder, description, SummarySection, and RAG point behavior for SubtypeKind.DomainDescription.

---

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-03 14:30:00 EST (UTC-05:00)

---

## 1. Purpose

IDX-072 defines how a Domain Description document is converted into RAG-ready artifacts in a deterministic, non-LLM-dependent way.

It establishes:

1. Domain as a first-class, indexable concept with:
   - One Finder Snippet (sparse, canonical, used for embeddings)
   - One Backing Artifact (rich narrative + catalog-derived class list, used post-retrieval)
2. A single canonical path from domain document → DomainDescriptionRag → SummarySection → RAG point.
3. Integration with:
   - IDX-068 for Finder Snippet shape and canonical fields
   - IDX-069 for SummarySection and RAG point construction
4. A strict guarantee that all content is taken from:
   - The domain description document
   - The Domain Catalog Service
   with no LLM refinement or synthetic text.
5. Domains as a convenient “hub” to see both narrative scope and the concrete model classes that participate in that domain.

---

## 2. Scope

IDX-072 governs only the Domain Description pipeline:

- What it defines:
  - DomainDescriptionBuilder as the canonical builder for SubtypeKind.DomainDescription
  - DomainDescriptionRag as the concrete IRagDescription implementation
  - Rules for constructing:
    - Exactly one Finder Snippet (per IDX-068)
    - Exactly one Backing Artifact (narrative + class list)
    - Exactly one SummarySection
    - Exactly one RAG point
  - How domain text and domain catalog entries are combined.

- What it does not define:
  - Domain catalog build/update logic (BuildCatalogAsync, LoadCatalogAsync)
  - Behavior for other SubtypeKinds (Model, Manager, Service, etc.)
  - Any LLM usage or refinement
  - Domain hierarchy, cross-domain relationships, or composite domains.

The lifecycle starts when a file has been classified as SubtypeKind.DomainDescription and ends after the RAG point has been constructed. Embedding and vector upload are handled elsewhere.

---

## 3. Inputs

The Domain Description pipeline consumes:

1. **IndexFileContext**
   - FullPath, RelativePath, and Contents for the domain document.
   - Document identity fields (Org, RepoId, ProjectId, CommitSha, BlobUri, etc.).
   - The builder must not re-read the file from disk; IndexFileContext is authoritative.

2. **Raw Symbol Text**
   - For DomainDescription, this is the entire document content as a string.
   - The builder extracts:
     - DomainName
     - DomainSummary
     - DomainNarrative
   deterministically.

3. **Domain Catalog Service (IDomainCatalogService)**
   - IDX-072 assumes the catalog is already built and loaded.
   - The builder uses:
     - GetClassesForDomain(domainKey) → IReadOnlyList<ModelClassEntry>
   - Results are used only to enrich the Backing Artifact.
   - The builder must not call BuildCatalogAsync or LoadCatalogAsync.

4. **Resource Dictionary (IResourceDictionary, optional)**
   - Passed for consistency with other builders.
   - DomainDescription typically does not require RESX lookups.
   - Any usage must be deterministic.

5. **No LLM Services**
   - DomainDescriptionBuilder does not call any LLM or refinement service.
   - All text is taken as-is from the document or catalog.

6. **No Additional External Inputs**
   - No web requests, no filesystem reads beyond IndexFileContext, no cross-file scanning beyond what the Domain Catalog Service already exposes.

---

## 4. SubtypeKind Classification

1. All domain description documents are classified as:

   ```csharp
   SubtypeKind.DomainDescription
   ```

   This classification occurs before the builder runs and is out of scope for IDX-072.

2. IDX-072 defines a single canonical builder:

   - DomainDescriptionBuilder

3. DomainDescription documents are treated as single segments:

   - One file → one segment → one description → one SummarySection.

4. Routing behavior:

   - Subtype detection marks the file as SubtypeKind.DomainDescription.
   - DescriptionBuilderRegistry returns exactly one builder:
     - DomainDescriptionBuilder
   - DomainDescriptionBuilder.BuildAsync is invoked with the full document text.

5. A domain description file is mutually exclusive with other SubtypeKinds; it cannot also be treated as Model, Manager, Service, Controller, etc.

---

## 5. Builder Responsibilities (DomainDescriptionBuilder)

The DomainDescriptionBuilder deterministically transforms a SubtypeKind.DomainDescription document into a DomainDescriptionRag. It performs no LLM refinement and relies on document text plus the Domain Catalog.

### 5.1 Parse the Domain Document

The builder must extract:

- DomainName
  - Derived from a clear marker (header, metadata block, or file naming convention).
  - Normalized into a domain key for catalog lookups (case-insensitive; hyphen/underscore normalization allowed).

- DomainSummary
  - The first meaningful sentence or dedicated summary section.
  - Used as-is with no rewriting.

- DomainNarrative
  - The remaining descriptive body of the document.
  - Treated as the authoritative narrative.

Parsing must be deterministic for all valid domain files.

### 5.2 Query Domain Catalog Service

The builder must retrieve model classes associated with this domain:

```csharp
var classes = domainCatalogService.GetClassesForDomain(domainKey);
```

Rules:

- All returned ModelClassEntry instances are copied into the description.
- An empty list is valid and does not constitute an error.
- The builder must not call BuildCatalogAsync or LoadCatalogAsync.

### 5.3 Construct the Description Object

The builder must instantiate DomainDescriptionRag and populate:

- DomainName
- DomainSummary
- DomainNarrative
- Classes (from catalog)
- Shared identity metadata via:

```csharp
description.SetCommonProperties(fileContext);
```

No other fields may be inferred or computed outside these rules.

### 5.4 Validation and Error Handling

Non-success (using InvokeResult) is required when:

- The domain document is empty or missing required structure.
- DomainName cannot be determined.

Allowed (must not be treated as errors):

- Missing or empty DomainSummary.
- Missing or empty DomainNarrative.
- Empty class list from GetClassesForDomain.

Errors must be reported via InvokeResult<IRagDescription>; the builder must not throw exceptions for normal validation failures.

### 5.5 Prohibited Actions

The builder must not:

- Call any LLM or refinement service.
- Re-read files from disk.
- Perform any network I/O.
- Attempt embedding work.
- Generate more than one description per file.
- Produce more than one SummarySection.

### 5.6 Builder Result

On success, the builder returns:

```csharp
InvokeResult<IRagDescription>
```

Where the IRagDescription instance is a fully initialized DomainDescriptionRag compatible with SummarySection and RAG point construction.

### 5.7 Examples (Required Output Patterns)

#### Example A — Finder Snippet (Canonical Format)

Given:

- DomainName = "Devices"
- DomainSummary = "Handles all device provisioning, registration, and lifecycle operations."

Finder Snippet:

```text
Domain: Devices
DomainSummary: Handles all device provisioning, registration, and lifecycle operations.

Kind: Domain

Artifact: Devices

Purpose: Describes the scope and responsibilities of the Devices domain.
```

Notes:

- This format is required for all domain finder snippets.
- Purpose always follows the pattern:
  - "Describes the scope and responsibilities of the {DomainName} domain."

#### Example B — Backing Artifact (Condensed Example)

Given:

- DomainNarrative already written in the document.
- Two catalog classes:

  - Device: Registered Device, "Represents a device in the system"
  - DeviceGroup: Device Group, "Logical grouping of devices"

Backing Artifact:

```text
# Domain: Devices

## Summary
Handles all device provisioning, registration, and lifecycle operations.

## Narrative
(All text captured exactly from DomainNarrative; no LLM refinement.)

## Model Classes in This Domain

### Device
- Qualified Name: LagoVista.Devices.Models.Device
- Title: Registered Device
- Description: Represents a device in the system
- Help: (helpText from catalog)
- Path: src/devices/models/Device.cs

### DeviceGroup
- Qualified Name: LagoVista.Devices.Models.DeviceGroup
- Title: Device Group
- Description: Logical grouping of devices
- Help: (helpText from catalog)
- Path: src/devices/models/DeviceGroup.cs
```

Formatting must remain stable and deterministic.

---

## 6. IRagDescription Responsibilities (DomainDescriptionRag)

DomainDescriptionRag is the concrete IRagDescription implementation for domain descriptions:

```csharp
public sealed class DomainDescriptionRag : SummaryFacts, IRagDescription
```

### 6.1 Single SummarySection

DomainDescriptionRag must produce exactly one SummarySection:

- SectionKey:
  - domain-{normalizedDomainName}
  - Lowercase, spaces removed, hyphens/underscores normalized to hyphens.
- FinderSnippet:
  - The canonical domain snippet described in Section 5.7.
- BackingArtifact:
  - The structured artifact (narrative + catalog-derived class list).

No additional sections or chunking are allowed.

### 6.2 RAG Point Creation

DomainDescriptionRag must not override SummaryFacts RAG point creation.

- BuildRagPoints() is inherited from SummaryFacts.
- Since there is only one SummarySection:
  - Exactly one RAG point is produced.

SummaryFacts is responsible for:

- Creating RagVectorPayload.
- Generating semantic IDs.
- Assigning blob URIs.
- Using section vectors to fill the RAG point.

### 6.3 Identity and Metadata

The builder must call:

```csharp
description.SetCommonProperties(fileContext);
```

This populates:

- DocId, OrgId, OrgNamespace, ProjectId
- RepoId, Repo, RepoBranch, CommitSha
- Path, BlobUri

These values are used when RAG points are created.

### 6.4 Required Fields

DomainDescriptionRag exposes read-only properties:

- string DomainName
- string DomainSummary
- string DomainNarrative
- IReadOnlyList<ModelClassEntry> Classes

Values originate strictly from parsed domain text and catalog results.

### 6.5 Determinism

DomainDescriptionRag must not:

- Depend on external services.
- Perform I/O.
- Call any LLM.
- Vary its output based on runtime state beyond supplied inputs.

Given the same inputs, it must always produce identical SummarySections and RAG points.

---

## 7. SummarySection Rules

DomainDescriptionRag produces exactly one SummarySection, which is the atomic indexing unit per IDX-069.

### 7.1 Section Count

- Exactly one SummarySection per domain.
- No token-based splitting or multi-part sections.

### 7.2 SectionKey Format

The SectionKey must be:

```text
domain-{normalizedDomainName}
```

Normalization rules:

- Lowercase.
- Spaces removed or converted to hyphens.
- Hyphens and underscores normalized to hyphens.
- No trailing punctuation.

Examples:

- "Business" → "domain-business"
- "AI Services" → "domain-ai-services"
- "Device_Provisioning" → "domain-device-provisioning"

### 7.3 FinderSnippet Content

FinderSnippet must exactly follow:

```text
Domain: {DomainName}
DomainSummary: {DomainSummary}

Kind: Domain

Artifact: {DomainName}

Purpose: Describes the scope and responsibilities of the {DomainName} domain.
```

- No additional lines or fields.
- No class list included here.

### 7.4 BackingArtifact Content

BackingArtifact must contain:

1. Domain summary:
   - Direct copy of DomainSummary.

2. Full domain narrative:
   - Direct copy of DomainNarrative.
   - No rewriting.

3. Catalog-based class list:
   - One subsection per ModelClassEntry.
   - Required fields:
     - ClassName
     - QualifiedClassName
     - Title
     - Description
     - HelpText
     - RelativePath

4. Deterministic, markdown-like layout matching the example in Section 5.7.

### 7.5 Section Metadata

The SummarySection must set:

- SectionKey (as above)
- PartIndex = 1
- PartTotal = 1
- SymbolType = "Domain"
- Symbol = DomainName
- BusinessDomainKey = a normalized form of DomainName
- EmbeddingModel (provided externally)
- Language (usually "en-US")

### 7.6 Purity Requirements

Section construction must not:

- Consult the Domain Catalog Service.
- Re-parse the domain file.
- Perform any I/O.
- Call any LLM.

All data must come from values already stored on DomainDescriptionRag.

### 7.7 Allowed Normalization

Only deterministic text normalization is allowed, such as:

- Normalizing newlines.
- Trimming leading/trailing blank lines.
- Stable indentation adjustments.

No rewriting or reordering of content is allowed.

---

## 8. RAG Point Construction

DomainDescriptionRag produces exactly one RAG point via SummaryFacts, in line with IDX-069.

### 8.1 One Section → One Point

- Single SummarySection → single IRagPoint in BuildRagPoints().

### 8.2 SummaryFacts Ownership

RAG point construction is owned by SummaryFacts:

- DomainDescriptionRag must not override this behavior.

### 8.3 Payload Fields

SummaryFacts populates:

- Identity and repository:
  - DocId, OrgId, OrgNamespace, ProjectId, RepoId, Repo, RepoBranch, CommitSha, Path, BlobUri.
- Section metadata:
  - SectionKey, PartIndex, PartTotal, BusinessDomainKey, EmbeddingModel, Language.
- Classification:
  - ContentTypeId (defaults to source-like content; may be extended in future DDRs).
  - Subtype and SubtypeFlavor (see below).

DomainDescriptionRag must set:

- Subtype = "Domain"
- SubtypeFlavor = "Canonical"

### 8.4 Title Convention

RAG point Title must be:

```text
Domain: {DomainName} - {SectionKey} (Chunk 1 of 1)
```

### 8.5 SemanticId Rules

SemanticId is generated per IDX-069:

```text
{OrgNamespace}:{ProjectId}:{RepoId}:domain:{domainname}:{sectionkey}:1
```

- Entire ID lowercased.
- No empty segments or "double colons".
- DomainName is normalized only as part of lowercasing, not rewritten.

### 8.6 Blob URI Assignment

Blob URIs must follow IDX-069 conventions:

- FullDocumentBlobUri = BlobUri
- SnippetBlobUri = "${BlobUri}.{SectionKey}.1"
  - Lowercased.
  - Spaces replaced with underscores.

### 8.7 Determinism and Purity

RAG point construction must not:

- Perform I/O.
- Consult external services.
- Call any LLM.
- Modify textual content.

### 8.8 Error Handling

- Errors in embedding or section state are reported via InvokeResult<IRagPoint>.
- Programming errors such as duplicate SnippetBlobUri may throw, consistent with IDX-069.
- DomainDescriptionRag must not introduce new error paths beyond those defined by SummaryFacts.

---

## 9. Registry Participation

DomainDescriptionBuilder must participate in the global Description Builder Registry defined in IDX-069.

### 9.1 Required Registration

At startup:

```csharp
registry.Register<DomainDescriptionBuilder>(SubtypeKind.DomainDescription);
```

### 9.2 Single Builder Guarantee

For IDX-072:

- Exactly one builder is registered for SubtypeKind.DomainDescription.
- Duplicate registrations for (DomainDescriptionBuilder, SubtypeKind.DomainDescription) are rejected.

### 9.3 Deterministic Resolution

Registry behavior:

- GetBuilders(SubtypeKind.DomainDescription) returns:
  - A list with exactly one builder: DomainDescriptionBuilder.
- No ordering ambiguity exists since there is only one builder type.

### 9.4 Registry Responsibilities

The registry:

- Resolves builders via DI.
- Returns builders by SubtypeKind only.
- Does not:
  - Inspect content.
  - Decide applicability.
  - Invoke builders.
  - Call LLMs or external services.

---

## 10. Output Contracts

This DDR defines the output contracts required for interoperability.

### 10.1 Builder Output

On success, DomainDescriptionBuilder returns:

```csharp
InvokeResult<IRagDescription>
```

- InvokeResult.Successful == true
- Result is a DomainDescriptionRag instance.

On failure:

- InvokeResult.Successful == false
- Result == null
- Errors contain diagnostic messages.

### 10.2 DomainDescriptionRag Contract

DomainDescriptionRag:

- Inherits from SummaryFacts.
- Implements IRagDescription.

Required properties:

- string DomainName { get; }
- string DomainSummary { get; }
- string DomainNarrative { get; }
- IReadOnlyList<ModelClassEntry> Classes { get; }

Required methods:

- IReadOnlyList<SummarySection> BuildSummarySections()
  - Returns exactly one section.
- IReadOnlyList<IRagPoint> BuildRagPoints()
  - Uses SummaryFacts implementation (must not be overridden).

### 10.3 SummarySection Contract

The single SummarySection must have:

- FinderSnippet: canonical snippet from Section 5.7.
- BackingArtifact: narrative + class list, structured as in the example.
- SectionKey: domain-{normalizedDomainName}.
- PartIndex = 1
- PartTotal = 1

### 10.4 RAG Point Contract

The resulting RAG point must have:

- Payload populated per SummaryFacts and IDX-069.
- Subtype = "Domain"
- SubtypeFlavor = "Canonical"
- Title following:
  - "Domain: {DomainName} - {SectionKey} (Chunk 1 of 1)"
- SemanticId following the pattern:
  - "{OrgNamespace}:{ProjectId}:{RepoId}:domain:{domainname}:{sectionkey}:1"
- Valid, unique SnippetBlobUri.

### 10.5 Error Output

On error:

- Builder returns a non-success InvokeResult<IRagDescription> with Result == null.
- RAG point creation returns failed InvokeResult<IRagPoint> instances.
- Exceptions are reserved for programming errors such as duplicate SnippetBlobUri.

---

## 11. Error Handling

DomainDescriptionBuilder and DomainDescriptionRag must be resilient to malformed documents and missing catalog data, while preserving deterministic behavior.

### 11.1 Builder-Level Failures

The builder must return non-success InvokeResult for:

- Empty or unreadable domain documents.
- Inability to determine DomainName.
- Structural issues that prevent parsing the domain content.

### 11.2 Tolerated Conditions

The following are allowed and must not cause failure:

- Missing or empty DomainSummary.
- Missing or empty DomainNarrative.
- Empty class list from GetClassesForDomain.
- Missing or empty helpText in ModelClassEntry.
- Minor formatting irregularities in the document.

### 11.3 Programming Errors

Exceptions are allowed only for programming errors, such as:

- Invalid state in SummaryFacts (e.g., missing identity fields).
- Duplicate SnippetBlobUri detection.

These are treated as critical issues and must be detected during development/testing.

### 11.4 RAG Construction Errors

During RAG point construction (SummaryFacts):

- Embedding failures are surfaced via InvokeResult<IRagPoint>.
- Semantic ID construction errors due to missing fields are considered programming errors.
- DomainDescriptionRag must not add additional RAG-level error behaviors.

### 11.5 Logging

Builder-level errors may be logged through injected logging services. Logging format is not dictated by this DDR; it follows existing logging conventions.

---

## 12. Non-Goals

IDX-072 explicitly limits its responsibilities to the canonical Domain Description pipeline. The following are out of scope:

1. Domain catalog construction and maintenance:
   - Scanning C# files and RESX for models.
   - Persisting or loading catalogs.
   - Scheduling catalog rebuilds.
   These are covered by the Domain Catalog DDR.

2. Any LLM interaction:
   - No refinement of DomainSummary.
   - No rewriting of narrative content.
   - No AI-generated text or inferred entities.

3. Other SubtypeKinds:
   - Models, Managers, Services, Controllers, Repositories, Interfaces, Tests, DDRs, etc.
   Each requires its own DDR.

4. Complex domain structures:
   - Composite domains.
   - Parent/child domain relationships.
   - Cross-domain graphs or dependency modeling.
   IDX-072 deals only with single, flat domain descriptions.

5. Multi-section or multi-point outputs:
   - Domains always produce exactly one SummarySection and one RAG point.

6. Embedding configuration:
   - Embedding models and invocation are owned by higher-level orchestration and SummarySection logic, not by DomainDescriptionRag.

7. Automatic field generation:
   - No synthesized domain metadata.
   - No synthetic summaries or class entries.
   - All fields originate from the domain document and domain catalog.
