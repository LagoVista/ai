# IDX-Structured Summary Propagation – DDR

## Overview
This decision record defines how Domain and Model semantic definitions are propagated into all SummarySection normalized text for downstream embedding, search, and LLM context.

The goal is to strengthen semantic recall across related artifacts (Models, Managers, Repositories, Services, Controllers) without polluting embeddings with routing-only metadata.

---

## Problem Statement

Embeddings only "see" the normalized text of each chunk. If a Manager, Repository, or Controller does not explicitly contain domain or model context in its text, then queries referencing the domain or model may fail to retrieve the correct chunk.

At the same time, duplicating full domain or model descriptions into every section would:

- Waste token budget
- Create semantic noise
- Reduce signal from local details

We need a balanced propagation pattern.

---

## Key Decisions

### Decision 1 — Domain and Model must be textually present in all related sections

Every SummarySection belonging to a SubKind must include:

- DomainName plus a short DomainTagline
- ModelDisplayName plus a short ModelTagline (if applicable)

These are used to seed the semantic embedding.

Example (intro lines in normalized text):

"Device Manager (Domain: Device Management)\nThis component operates on the Device model, which represents an IoT node within the device management domain."

---

### Decision 2 — Use taglines, not full descriptions

We split content into:

- Domain Overview: rich multi-paragraph explanation (stored once)
- Domain Tagline: 1–2 sentence summary (reused)
- Model Overview: rich description (stored in model section)
- Model Tagline: 1–2 sentence summary (reused)

Only taglines are reused and propagated into related sections.

---

### Decision 3 — Keep routing metadata out of normalized text

The following MUST NOT appear in SectionNormalizedText:

- Repo path
- File path
- Commit SHA
- Internal IDs

These fields exist only in RagVectorPayload:

- Repo
- Path
- Symbol
- SymbolType
- SectionKey
- Domain

This keeps embeddings clean and semantically meaningful.

---

## SummarySection structure

```csharp
public sealed class SummarySection
{
    public string SectionKey { get; set; }      // e.g. "model-overview", "model-properties"
    public string SectionType { get; set; }     // e.g. "Overview", "Properties", "Relationships"
    public string Symbol { get; set; }          // e.g. "Device"
    public string SymbolType { get; set; }      // e.g. "Model", "Manager", "Repository"
    public string Title { get; set; }           // e.g. "Device Model – Properties"
    public string SectionNormalizedText { get; set; }
}
```

- Symbol and SymbolType come from SubKind detection.
- Title will include the Domain plus Model where relevant.
- SectionNormalizedText begins with domain plus model tagline.

---

## Application to SubKinds

- Domain: includes full domain description and tagline.
- Model: includes domain tagline plus full model description and tagline.
- Manager: includes domain tagline and model tagline.
- Repository: includes domain tagline and model tagline.
- Controller: includes domain tagline and model tagline.
- Service: includes domain tagline; model tagline is optional if the service is tightly scoped to a single model.

---

## Example normalized text (Manager)

```text
Device Manager (Domain: Device Management)

The Device model represents an IoT node in the field. This manager is responsible for initiating configuration, processing telemetry events, and enforcing lifecycle rules.

Key operations:
- RegisterDevice
- AssignCloudHub
- FlagOffline
```

This text is then passed into the chunking and embedding pipeline.

---

## Benefits

- Better semantic recall
- Consistent cross-artifact understanding
- Reduced token pollution
- Clean integration into existing RagChunk / RagVectorPayload flow
- Stronger LLM reasoning context

---

## Status

Accepted – ready for implementation in ChunkFlavor builders.
