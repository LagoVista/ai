# IDX-065 — Summary Section Scoring Service
**Status:** Approved  
**Scope:** LagoVista / Aptix Indexing & Embedding Pipeline  
**Applies To:** All repositories containing semantic indexing components  
**Protocol:** SYS-001

---

## 1. Approval Metadata
- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-11-30 09:16 EST (UTC-05:00)

---

## 2. Preamble
This DDR defines a new **Summary Section Scoring Service** that evaluates the semantic quality of SummarySections and equivalent semantic snippets before they are embedded and stored in Qdrant. Its purpose is to ensure high-quality, domain-relevant, coherent embeddings and to prevent noisy or low-value vectors from contaminating the semantic search space. All semantic artifacts destined for embedding must pass through this scoring service and a publication gate.

---

## 3. Problem
The indexing pipeline currently embeds SummarySections, normalized chunk explanations, and DDR text without any quantitative measure of semantic clarity. Poorly formed summaries create weak embeddings that degrade retrieval accuracy, produce false positives, and introduce “semantic soup” into vector space. There is no mechanism to detect, suppress, or improve low-quality summaries. The pipeline requires a deterministic, extensible, and pluggable scoring + gating system.

---

## 4. Goals
This DDR establishes:

- A deterministic scoring engine (`SummarySectionScoringService`) that assigns dimension scores and a composite score to each SummarySection-like snippet.
- A publication gate (`SummarySectionScoreHandler`) that decides whether a snippet should be embedded.
- An optional rewrite loop (0 to N cycles) that may improve low-scoring summaries.
- Logging, reporting, and traceability for all low-score summaries.
- Clean separation of concerns: scoring, decision-making, rewriting, embedding.

The primary goal is improved RAG retrieval quality through structured semantic validation.

---

## 5. High-Level Workflow Summary
Per SYS-001, the following elements define the system-level behavior:

1. SummarySection builder creates semantic text.
2. **SummarySectionScoringService** evaluates the semantic text.
3. **SummarySectionScoreHandler** decides publish/suppress and may rewrite/rescore.
4. Only handler-approved text proceeds to embedding.
5. Final vectors in Qdrant include scoring metadata.
6. Reports of low-scoring items are generated per run, grouped by `SubtypeKind`.

---

## 6. Detailed Design

### 6.1 SummarySectionScoringService (Core Engine)

#### 6.1.1 Responsibilities
The `SummarySectionScoringService` is a *pure*, deterministic component that evaluates semantic snippets and produces a **SummarySectionScoreResult**. It must:

- Score based on structural clarity, semantic cohesion, domain anchoring, noise ratio, coverage, and query alignment.
- Produce dimension scores (0–100) and a composite score (0–100).
- Produce classification categories (`Excellent`, `Good`, `Fair`, `Poor`, `Reject`).
- Generate flags and human-readable reasons.
- Operate without modifying text or making publish decisions.

#### 6.1.2 What It Scores
It scores **only** semantic text intended for embedding:

- SummarySections  
- Normalized chunk explanations  
- DDR sections  
- RAG-normalized snippets  
- FreeText semantic entries

**Raw source code is explicitly out of scope and must not be scored.**

---

### 6.2 SummarySectionScoreHandler (Publication Gate)

#### 6.2.1 Responsibilities
The `SummarySectionScoreHandler` MUST:

- Receive the snippet + `SummarySectionScoreResult`.  
- Log/report low-scoring items.  
- Optionally attempt to rewrite the snippet.  
- Optionally rescore after rewrite.  
- Repeat rewrite/rescore up to **N iterations** (configurable).  
- Produce a **SummarySectionScoreHandlingResult** containing:
  - Final snippet text  
  - Final composite score  
  - Publish decision (`ShouldPublish`)  
  - Disposition (`Accepted`, `AcceptedAfterRewrite`, `RejectedLowScore`, etc.)  
  - Rewrite count  
  - Reasons  

#### 6.2.2 Publication Authority
The handler is the **sole authority** for determining whether a snippet may be embedded:

> **Only snippets with `ShouldPublish == true` may be embedded.**

#### 6.2.3 Rewrite Boundaries
Rewriting MUST NOT change the semantic intent of the original snippet. It may only improve clarity, structure, and domain anchoring. Rewriting is optional.

#### 6.2.4 Handler Factory
A pluggable `ISummarySectionScoreHandlerFactory` must produce handlers based on configuration.

Handler types may include:

- LogOnlyHandler (default)  
- RewriteOnceHandler  
- RewriteUpToNHandler  
- StrictRejectHandler (for CI/CD)  

---

### 6.3 Reports
Reports are produced **per-run**, grouped by `SubtypeKind` and include:

- SnippetId  
- Original composite score  
- Flags + reasons  
- Full text of the snippet  
- Final disposition  
- Rewrite count  

No specific sink is mandated (console, JSON file, telemetry, etc.).

---

### 6.4 Integration Points

#### 6.4.1 Upstream
Receives semantic text from SummarySection builder / normalized chunk builder.

#### 6.4.2 Scoring → Handler
Scoring engine produces deterministic result. Handler acts as publication gate.

#### 6.4.3 Embedding Layer
Only the `FinalSnippetText` approved by the handler is embedded. The embedding metadata must include the final score + disposition.

#### 6.4.4 CI/CD
Strict handlers may reject builds based on score thresholds or number of rejected items.

---

## 7. Non-Goals & Boundaries

### 7.1 Does Not Score Raw Source Code
Scoring applies only to semantic summaries, never code.

### 7.2 Does Not Generate SummarySections
Upstream SummarySection builder remains untouched.

### 7.3 Does Not Embed or Write to Qdrant
Those are downstream concerns.

### 7.4 Does Not Rewrite Text (Scoring Engine Only)
Rewrite logic lives entirely in the handler layer.

### 7.5 Not a Learning System
No ML training or dynamic weight learning is included.

### 7.6 Not Responsible for Output Formatting
Handlers own all reporting formatting decisions.

### 7.7 Not Responsible for Semantic Drift Detection
This DDR covers only per-snippet scoring.

---

## 8. DDR Workflow Compliance (SYS-001)
- DDR identifier: **IDX-065** assigned according to TLA+Index rules.  
- High-level summary reviewed and accepted.  
- Deep-dive completed bullet-by-bullet.  
- User approval captured with timestamp.  
- File generated into `./ddrs/` as a standalone artifact.  
- Implementation and tests deferred to subsequent DDRs.

---

**End of DDR IDX-065**
