# AGN-018 — Summarizing DDRs for LLMs

**ID:** AGN-018  
**Title:** Summarizing DDRs for LLMs  
**Status:** Approved

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-20

---

*(DDR content omitted here for brevity; all previously approved sections remain unchanged.)*

---

## 11. Tool Usage Instructions (Baseline DDR Ingestion & ModeInstruction Derivation)

The following instructions define the **baseline behavior** for tools that ingest DDRs and derive ModeInstructions.

These rules apply **by default** unless a future DDR explicitly defines alternative ingestion or transformation behavior.

```
Imports an approved DDR from a Markdown document into the DDR store (create-only) and derives ModeInstructions for LLM use.

Use when the user provides a DDR Markdown file and requests that it be imported.

The tool MUST parse identifier, TLA, index, title, status, and approval metadata when present.
If the TLA-index already exists, the tool MUST reject the import and report the existing DDR.
If the parsed identifier does not match the parsed TLA-index, the tool MUST return an error.

Unparseable or missing fields MUST be returned as null/unknown and explicitly confirmed with the user.

The primary responsibility of this tool is to correctly ingest and persist the DDR as an authoritative artifact.
As part of ingestion, the tool MUST also derive ModeInstructions from the approved DDR for LLM execution.

ModeInstructions represent enforceable procedural rules and constraints, not descriptive or contextual summaries.

The tool MUST NOT invent summaries, descriptive text, or inferred intent.
The tool MUST NOT summarize narrative sections, rationale, or explanatory content.
Only operationally relevant DDR content is eligible for ModeInstruction derivation.

Chapters and narrative-only sections are intentionally excluded from ModeInstruction derivation.

When deriving ModeInstructions, the tool MUST apply the following assembly rules:

Summarize the approved DDR into ModeInstructions intended for LLM execution.
Extract only procedural rules, ordered steps, constraints, prohibitions, conditions, and approval gates.
Rewrite all extracted content as imperative instructions using explicit normative keywords (MUST, MUST NOT, SHOULD, MAY).
Preserve ordering and gating semantics explicitly.
Enumerated allowlists, denylists, and supported value sets MUST be emitted verbatim.
Exclude narrative explanation, rationale, historical context, examples, and non-operational commentary.
Do not infer missing rules or invent behavior not explicitly defined in the DDR.

The ingestion and ModeInstruction derivation rules defined here constitute the baseline DDR import behavior.

Unless a future DDR explicitly defines alternative ingestion or transformation rules for a specific DDR type, these rules MUST be applied by default.

DDR-specific ingestion behavior MAY extend or refine this baseline but MUST NOT weaken its constraints unless explicitly approved and documented.
```
