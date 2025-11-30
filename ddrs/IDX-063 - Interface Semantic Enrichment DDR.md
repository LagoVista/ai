# IDX-063 — Interface Semantic Enrichment DDR

**TLA:** IDX  
**Index:** 063  
**Title:** Interface Semantic Enrichment  
**Status:** Approved  
**Owner:** Aptix / Kevin Wolf  

## Approval Metadata
- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-11-29 17:50 EST (UTC-05:00)

---

# 1. Purpose & Goals

IDX-063 defines how InterfaceOverview chunks (IDX-042) are enriched with **short, machine-targeted semantic fields** that improve vector search accuracy and support LLM contract reasoning.

The enriched fields are embedded as **structured JSON** in the chunk (per IDX-064) and transformed into **deterministic plain text** during embedding.

### 1.1 Primary Goals
1. Improve interface discoverability in the vector index.
2. Expose key contract semantics not present in signatures.
3. Enable LLMs to reason architecturally about contracts.
4. Maximize semantic density per token for embeddings.

### 1.2 Non-Goals
- Not intended for human documentation.
- Not a replacement for manager/repository/controller DDRs.
- Not describing implementation behavior.

### 1.3 Success Criteria
- Vector queries reliably retrieve interface contracts.
- Enriched fields remain short, factual, deterministic.
- Enriched chunks remain consistent with IDS-042, 037–041.

---

# 2. Scope & Trigger

### 2.1 Applies to
InterfaceOverview chunks with:
- `Kind = "SourceCode"`
- `SubKind = "Interface"`
- `SymbolType = "Interface"`
- `ChunkFlavor = "Overview"`

### 2.2 Trigger
Enrichment occurs **after** IDX-042 InterfaceOverview construction and **before** embedding per IDX-064.

### 2.3 Pipeline Position
1. Roslyn analysis
2. Build InterfaceDescription
3. Build InterfaceOverview (structural)
4. **Apply IDX-063 enrichment**
5. Write enriched chunk JSON to content repo
6. Flatten for embedding (IDX-064)
7. Write vectors to Qdrant

### 2.4 Out of Scope
- Classes, enums, structs, abstract classes
- Raw text chunks
- Any symbol not a C# `interface`

### 2.5 Preconditions
- Structural InterfaceOverview exists
- Identity fields fully populated
- Method signatures known
- Linkage fields filled where possible

---

# 3. New / Enriched Fields

### 3.1 OverviewSummary (string)
Compact 1–3 sentence summary (≤ 320 chars). Factual, role- and domain-aware.

### 3.2 Responsibilities (string[])
3–7 short statements (≤ 120 chars each) describing responsibilities.

### 3.3 UsageNotes (string[])
0–5 integration hints (≤ 120 chars each).

### 3.4 LinkageSummary (string)
≤ 320 chars summarizing implementations and consumers.

### 3.5 Method Summary Refinement
Each method’s `SemanticSummary` (≤ 120 chars, one sentence) is normalized.

### 3.6 Fields Not Modified
Structural identity, signatures, linkage arrays, line positions.

---

# 4. Style & Guardrails

### 4.1 General Rules
- Neutral tone
- Factual only
- No speculation
- Compact language
- Deterministic phrasing

### 4.2 Hard Length Limits
- OverviewSummary: 320 chars
- Responsibilities: 120 chars per entry
- UsageNotes: 120 chars per entry
- LinkageSummary: 320 chars
- Method summaries: 120 chars, one sentence

### 4.3 Formatting
- Plain text (no Markdown, HTML, special syntax)
- No prefixes ("Summary:")
- Sentence casing

### 4.4 Safety Guardrails
- No invented domain behavior
- No invented validation, persistence, I/O
- Must not infer PrimaryEntity beyond IDX-042

### 4.5 Idempotency
Same input → same enrichment; no drift.

### 4.6 Embedding Optimization
Dense noun/verb vocabulary; no adjectives; consistent domain terminology.

---

# 5. Execution in Pipeline

### 5.1 Ordering
1. Roslyn extraction
2. InterfaceDescription built
3. InterfaceOverview (structural)
4. **IDX-063 enrichment**
5. Write to content repo
6. Flatten for embedding (IDX-064)
7. Vector write

### 5.2 Trigger Rules
Runs automatically when structural InterfaceOverview exists.

### 5.3 Inputs
- `InterfaceDescription`
- Optional interface source text
- Enrichment rules

### 5.4 Outputs
- Enriched InterfaceDescription fields
- Projected InterfaceOverview chunk

### 5.5 Error Handling
- Must not hallucinate
- Must respect constraints
- May return warnings or fail via InvokeResult

### 5.6 Idempotency Guarantee
Reruns on same input produce identical enriched results.

### 5.7 Integration with Future TUL DDRs
Enrichment logic must be encapsulated in a tool implementing the contract in Section 8.

### 5.8 Content Repository Write (per IDX-064)
- Enriched chunk JSON written to content repo before embedding.
- Only flattened text is embedded.
- Full structured JSON used for reasoning.

---

# 6. RAG / Retrieval Rationale

### 6.1 Purpose
Interfaces encode contracts; enriched signals make them retrievable.

### 6.2 Improved Vector Matching
Stable vocabulary: ManagerContract, RepositoryContract, DomainModelCatalog, etc.

### 6.3 LLM Uses Structured JSON
Vector search → chunk lookup → LLM receives enriched JSON (not flattened text).

### 6.4 Multi-Hop Reasoning
Controller → Interface → Manager → Repository → Model chains are fully discoverable.

### 6.5 Noise Avoidance
Enriched fields minimize token noise while maximizing semantic clarity.

### 6.6 Reliance on IDX-064
Structured JSON is stored; flattened text is embedded.

### 6.7 Retrieval Success Criteria
Interfaces cluster naturally and queries surface relevant contracts.

---

# 7. Rationale

### 7.1 Interfaces as Architectural Nodes
Interfaces are the primary contract boundaries.

### 7.2 Signatures Alone Are Insufficient
Semantic enrichment supplies missing intent.

### 7.3 Multi-Hop Reasoning
Enables correct traversal across layers.

### 7.4 Vocabulary Cohesion
Consistent architectural terminology improves clustering.

### 7.5 Contract Enforcement Foundations
Enriched JSON supports future automated analysis.

### 7.6 Works With IDX-064
Separation of storage vs. embedding is essential.

### 7.7 Summary
Dense, factual semantic data → better retrieval → better reasoning.

---

# 8. Implementation & Tooling Considerations

### 8.1 Source Model: InterfaceDescription
Enrichment modifies only:
- OverviewSummary
- Responsibilities
- UsageNotes
- LinkageSummary
- Method SemanticSummary

All structural fields remain unchanged.

### 8.2 Enrichment Tool Contract

```csharp
public interface IInterfaceSemanticEnricher
{
    Task<InvokeResult<InterfaceDescription>> EnrichAsync(
        InterfaceDescription description,
        string interfaceSource = null);
}
```

### 8.3 Chunk Builder Integration
`InterfaceDescription` → `InterfaceOverview` chunk → content repo → embedding.

### 8.4 LLM Usage
LLM receives `InterfaceDescription` + enrichment rules and produces enriched fields.

### 8.5 Idempotency & Testing

- Same input → same output.
- Tests validate length, style, determinism.

### 8.6 Relationship to IDX-064
Structured JSON stored in content repo; flattening handled separately.

---

# End of IDX-063
