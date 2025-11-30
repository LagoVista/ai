# IDX-066 — Model and Domain Metadata Refinement Process
**Status:** Approved
**Owner:** Kevin Wolf & Aptix

---

## Approval Metadata
- **Approved By:** Kevin Wolf
- **Approval Timestamp:** 2025-11-30 11:15 EST (UTC-05:00)

> NOTE: This version supersedes the prior IDX-066 by adding FormField-based field context to the model refinement pass, while keeping all previous behaviors intact.

---

## 1. Overview
IDX-066 defines the workflow for extracting, refining, validating, and cataloging model and domain metadata across the entire NuvIoT ecosystem. It governs how `ITitleDescriptionReviewService` uses an LLM to improve user-facing metadata (titles, descriptions, and optional help text) while preserving strict semantic fidelity.

Refinement is performed in two ordered passes:
1. **Model Pass** — Discover first-class entities, gather FormField-based field context, and refine model-level metadata.
2. **Domain Pass** — If and only if the model pass has zero failures, refine domain-level metadata using refined model summaries as context.

All results are stored in a persistent global catalog. The catalog is global to NuvIoT; each run is strictly scoped to the set of `DiscoveredFile` instances passed in.

---

## 2. Extraction Points
This phase identifies relevant classes and extracts raw metadata for processing.

### 2.1 Identifying First-Class Models
- A class is a **first-class model** if and only if it has exactly one `[EntityDescription]` attribute.
- Classes with zero such attributes are recorded as **skipped**.
- Classes with more than one `[EntityDescription]` are recorded as **failures**.
- From the attribute, IDX-066 extracts:
  - Title resource key
  - Description resource key
  - Help resource key (may equal description)
- The neutral/invariant RESX dictionaries provided to the run are used to resolve the actual text values for title, description, and help.
- Missing resource keys, missing entries, or illegal placeholders (e.g., `{0}`) are recorded as failures.

### 2.2 Identifying Domains
A class is a **domain descriptor** if:
- It has a `[DomainDescriptor]` attribute, and
- It exposes exactly one static member decorated with `[DomainDescription]` returning a `DomainDescription`.

From this member, IDX-066 extracts:
- Domain key (constant value referenced by the attribute argument)
- Domain name (`DomainDescription.Name`)
- Domain description (`DomainDescription.Description`)

Structural violations or missing/incorrect constants are treated as domain failures.

### 2.3 Skipped Files
Files containing no first-class models and no domain descriptors are recorded as **skipped**. Skipped entries include file path, hash, and indexVersion to avoid re-scanning unchanged non-relevant files.

### 2.4 FormField-Based Field Context (NEW)
Many first-class models additionally mark important properties with `[FormField]` attributes. IDX-066 uses these as **extra context** when refining model metadata.

For each first-class model:
- IDX-066 scans properties for `[FormField]` attributes.
- For each such property, it resolves:
  - The label resource (if provided) to a human-friendly field label.
  - The help resource (if provided) to a short field-level help text.
- It builds a list of **FieldSummary** items:
  - `PropertyName`
  - `Label` (resolved from RESX)
  - `Help` (resolved from RESX, may be null)
- These summaries are **not** written back or modified by IDX-066; they are used only as context when calling `ITitleDescriptionReviewService`.

This field context is used to help the LLM craft better, more grounded model-level descriptions and help text without changing individual field labels.

---

## 3. Model Review Mechanism (Entities Only)

### 3.1 Review Invocation
For each valid first-class model, IDX-066 calls:

```csharp
Task<TitleDescriptionReviewResult> ReviewAsync(
    SummaryObjectKind kind,       // SummaryObjectKind.Model
    string symbolName,            // class name
    string title,                 // model title text
    string description,           // model description text
    string help = null,           // model-level help text (if distinct)
    string context = null,        // FormField-derived context blob
    CancellationToken cancellationToken = default)
```

- `kind` is always `SummaryObjectKind.Model` for the model pass.
- `symbolName` is the C# class name (for traceability and prompt context).
- `title`, `description`, `help` come from model-level RESX entries.
- `context` is a preformatted **field context blob** built from `[FormField]` metadata (see 5.4).

### 3.2 LLM Result Handling
- On success, refined values are captured in the catalog and (in the full implementation) used to update RESX content.
- If `RequiresAttention == true`, original values are preserved and a catalog warning is recorded.
- If the call fails (schema/JSON/transport/guardrail violation), a catalog failure is recorded and the model pass is considered to have failures for gating.

### 3.3 Duplicate and Structural Issues
- Duplicate model names, malformed `[EntityDescription]` usage, unresolved resources, and other structural issues are recorded as **failures**.

### 3.4 Hash & Index Version Integration
- Before processing a model, IDX-066 computes the file hash via `IContentHashService`.
- Combined with `indexVersion`, the hash is used to decide whether to skip, reprocess, or treat an item as a previously-known failure/warning.

---

## 4. Reintegration Points (File & Resource Write-Back)

> NOTE: This DDR still defines the behavior for RESX write-back and catalog updates; the code skeleton generated under IDX-066 may initially implement only the catalog side, with RESX persistence to be fleshed out using your existing resource infrastructure.

### 4.1 Resource Update Rules (Models)
- When a model refinement succeeds and `RequiresAttention == false`, the refined values must be written back to the neutral/invariant RESX entries corresponding to the title, description, and help resource keys.
- Resource keys are never changed; only string values are updated.
- RESX updates should be applied atomically per file where possible.

### 4.2 Catalog Updates
For refined models, IDX-066 records:
- File path, file hash, repoId, symbolName
- Original and refined title/description/help
- Resource keys used
- IndexVersion and timestamp
- Optional notes from the LLM

For warnings and failures, original values are recorded along with notes or failure reasons.

### 4.3 Catalog Write Triggers & Footer
- The catalog is written (and footer regenerated) whenever:
  - A refinement is recorded
  - A warning is recorded
  - A failure is recorded
  - The run completes
- Skipped entries may accumulate in memory and are flushed at the next catalog write or end-of-run.

---

## 5. System Prompt Specification — Models

### 5.1 Goals
The model prompt must:
- Fix grammar, spelling, and clarity.
- Preserve factual meaning.
- Avoid hallucinations or invented behaviors.
- Harmonize title, description, and help at the model level.
- Use field context to better explain what the model does without turning the description into a long field list.
- Return strictly structured JSON.

### 5.2 JSON Schema
The LLM returns:

```json
{
  "refinedTitle": "string",
  "refinedDescription": "string",
  "refinedHelp": "string or null",
  "notes": "string",
  "requiresAttention": false
}
```

### 5.3 Guardrail Behavior
If the LLM is unsure whether changes would alter meaning, or the text appears ambiguous:
- It returns original values unchanged.
- It sets `requiresAttention: true`.
- It explains the concern in `notes`.
- IDX-066 records this as a **warning**, not a failure.

### 5.4 Key Fields Context (NEW)
The **`context`** parameter for models contains a preformatted block derived from `[FormField]` attributes, for example:

```text
KEY FIELDS (CONTEXT ONLY):
- Icon: Icon used to visually represent the agent.
- Vector database collection name: Name of the collection where embeddings are stored.
- Provider: LLM provider backing this agent.

Use these fields only as context to understand the model's purpose.
Do NOT list all fields in the final description or help text.
Do NOT invent new fields or behaviors.
```

The system prompt explicitly instructs the LLM:
- To use this context to better phrase the model description and help text.
- To avoid turning the final description into a detailed field checklist.
- To leave individual field labels/help unchanged; IDX-066 does not rewrite field-level metadata.

---

## 6. Domain Review Strategy & Prompt
(unchanged from prior version, except that domains now also pass their entity summaries via the same `context` parameter, not the `help` parameter.)

- Domains use `SummaryObjectKind.Domain`.
- `title` and `description` are the domain name and description.
- `help` is null; `context` is the domain/entity summary blob.
- The same JSON schema and guardrail behavior applies.
- Domain refinements are catalog-only in IDX-066.

---

## 7. Safety, Catalog, and Performance
All previously-agreed rules still apply:
- Non-halting behavior; all errors become catalog entries.
- Items may be in exactly one of: refined, warnings, failures, skipped.
- Model failures block domain refinement for the current run; warnings do not.
- Catalog is global, but each run only operates on the `DiscoveredFile` list passed in.
- Hash+indexVersion control incremental behavior.
- Concurrency, throttling, and backoff are configuration-driven.

---

_End of DDR — IDX-066 (FormField context revision)_
