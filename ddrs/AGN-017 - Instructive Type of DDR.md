# AGN-018 - Instructive type of DDR

**ID:** AGN-018  
**Title:** Instructive type of DDR 
**Status:** Approved

## Approval Metadata
- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-20

---

## 1. Introduction

### 1.1 Problem Statement
Detailed Design Reviews (DDRs) are authored for human understanding, traceability, and long-term architectural reference. As a result, they often contain narrative explanation, rationale, historical context, and approval mechanics that are counterproductive when passed directly to an LLM.

LLMs perform best when given concise, imperative, and unambiguous instructions. Feeding full DDRs into an LLM risks:
- Diluting critical procedural rules with explanatory text
- Losing ordering and gating semantics
- Encouraging interpretation instead of execution
- Increasing token cost while decreasing determinism

### 1.2 Intent of AGN-018
AGN-018 defines a formal approach for deriving a compact ModeInstruction from a DDR at ingestion time, intended to be delivered to an LLM by an internal agent tool.

The purpose is not to summarize for human readers, but to:
- Preserve the operational intent of a DDR
- Encode mandatory steps, constraints, and sequencing
- Remove narrative and archival material
- Produce an instruction set optimized for LLM compliance

### 1.3 Scope
This DDR applies to:
- Approved DDRs entering the agent ingestion pipeline
- Agent tools responsible for preparing LLM-facing instructions
- Scenarios where the LLM must follow a DDR-defined process without access to the full document

Out of scope:
- Human-facing summaries
- RAG indexing strategies
- Full DDR replacement or simplification for documentation purposes

### 1.4 Relationship to SYS-001
AGN-018 operates within the SYS-001 workflow:
- SYS-001 governs how DDRs are authored, reviewed, and approved
- AGN-018 governs how an approved DDR is transformed into an LLM-consumable ModeInstruction

This distinction ensures archival rigor is maintained while enabling reliable LLM execution.

---

## 2. Design Principles for LLM-Facing Instructions

This section defines the foundational principles that constrain all extraction and transformation behavior.

### 2.1 Fidelity Over Brevity
The primary objective of a ModeInstruction is behavioral fidelity, not maximum compression.

A shorter instruction that omits mandatory steps, approval gates, ordering constraints, prohibitions, or invariants is considered incorrect, even if it is concise.

Brevity is a secondary goal and is only applied after fidelity is preserved.

### 2.2 Determinism and Order Preservation
LLMs must receive instructions that:
- Are explicitly ordered where order matters
- Do not rely on implied sequencing
- Avoid ambiguous transitions unless the condition is formally stated

If a DDR defines a multi-step process, the ModeInstruction must encode:
- Explicit step boundaries
- Clear progression rules
- Any branching or gating logic

### 2.3 Imperative, Non-Narrative Language
ModeInstructions must be written in imperative form ("Do X", "Validate Y", "Do not proceed if Z") and declarative constraints ("X must always...", "Y must never...").

Narrative elements are explicitly excluded, including:
- Historical context
- Rationale and justification
- Examples intended for explanation rather than execution

### 2.4 Explicit Constraints and Prohibitions
Negative rules are as important as positive ones.

The transformation process must:
- Preserve all prohibitions and conditional restrictions
- Avoid softening constraints during paraphrasing
- Prefer explicit prohibition over implied limitation

### 2.5 Minimal Cognitive Load
Instructions should be structured to reduce interpretation:
- Prefer flat lists over deeply nested prose
- Avoid compound instructions when they can be split
- Eliminate synonyms that introduce ambiguity

This does not mean removing complexity, but rather making complexity explicit.

### 2.6 No Hidden Inference
The ModeInstruction must not require the LLM to:
- Infer missing steps
- Guess intent
- Reconstruct logic from partial signals

If a rule or dependency exists in the DDR, it must appear in the ModeInstruction in explicit form, or be intentionally excluded as non-operational.

### 2.7 Normative Language and Instruction Strength
All ModeInstruction content MUST be expressed using explicit, standardized normative qualifiers to convey instruction strength and eliminate ambiguity.

Each bullet or discrete instruction MUST include exactly one normative keyword from the approved set defined below. Instructions lacking a qualifier are considered invalid and MUST NOT be emitted into the final ModeInstruction.

#### 2.7.1 Approved Normative Keywords
The following normative keywords are permitted, listed in descending order of strictness:
- MUST: absolute requirement
- MUST NOT: absolute prohibition
- SHOULD: strong recommendation unless an explicit exception exists
- MAY: optional behavior

No other qualifiers (for example: "can", "might", "typically", "where appropriate") are permitted.

#### 2.7.2 One-Qualifier Rule
Each instruction:
- MUST contain exactly one normative keyword
- MUST NOT mix multiple strengths in a single bullet
- MUST apply the qualifier to the entire instruction

If a DDR statement contains multiple strengths, it MUST be decomposed into multiple instructions before emission.

#### 2.7.3 Default Strength Resolution
If a DDR rule does not explicitly state instruction strength:
- The ingestion tool MUST infer the weakest strength that preserves correctness
- The inferred strength MUST be explicit in the emitted ModeInstruction
- The inference rule applied SHOULD be consistent across all DDRs

Silent omission or implicit strength is not permitted.

#### 2.7.4 Capitalization and Formatting
Normative keywords:
- MUST be uppercase
- MUST appear at the beginning of the instruction
- MUST NOT be embedded mid-sentence

---

## 3. The ModeInstruction Concept

### 3.1 Definition
A ModeInstruction is a compact, imperative instruction set derived from an approved DDR at ingestion time and passed to an LLM by an internal agent tool.

It represents the operational essence of a DDR:
- What must be done
- In what order
- Under what constraints
- With what prohibitions

A ModeInstruction is not a summary, explanation, or abstraction. It is an executable instruction contract.

### 3.2 Intended Role in the Agent to Tool to LLM Chain
The ModeInstruction occupies a specific and narrow position in the execution pipeline:
- DDR: Human-authored, archival, explanatory, approval-gated
- Ingestion Tool: Extracts and normalizes operational intent
- ModeInstruction: LLM-facing instruction payload
- LLM: Executes or reasons strictly within the provided instruction set

The LLM must not be expected to interpret DDR prose, infer missing rules, or reconstruct workflow intent.

### 3.3 Behavioral Contract
A ModeInstruction establishes a behavioral contract between the agent and the LLM:
- The agent guarantees the instruction set is complete, ordered, and explicit
- The LLM is expected to comply without reinterpretation or augmentation
- Deviations are considered execution failures, not creative latitude

### 3.4 Boundaries and Non-Goals
A ModeInstruction is not intended to:
- Replace the DDR as an archival artifact
- Serve as documentation for humans
- Capture design rationale or tradeoffs
- Encode speculative or future-facing guidance

It must remain minimal, deterministic, and focused solely on execution-relevant content.

Any information not required for correct behavior must be excluded, even if it is useful context for a human reader.

### 3.5 Relationship to Instruction Modes
A ModeInstruction may be delivered to the LLM as a system instruction, developer instruction, or a dedicated mode payload. The delivery mechanism is intentionally flexible. AGN-018 defines instruction content, not transport.

---

## 4. DDR Ingestion-Time Transformation

### 4.1 Transformation Timing
The DDR ingestion-time transformation occurs only after a DDR has:
- Completed the SYS-001 workflow
- Received formal human approval
- Been designated for agent execution

Draft, unapproved, or in-review DDRs MUST NOT produce ModeInstructions.

### 4.2 Inputs to the Transformation
The ingestion tool MUST operate on the approved DDR as a structured source, including:
- Final DDR content
- Section structure and ordering
- Explicit constraints, gates, and prohibitions
- Approval metadata (for validation, not emission)

Narrative sections intended solely for human understanding MAY be read but MUST NOT be emitted.

### 4.3 Output of the Transformation
The transformation produces a single ModeInstruction payload consisting of:
1. A mandatory injected validation header (tool-owned) applied once prior to the instruction set
2. A normalized list of imperative instructions derived from the DDR

The ModeInstruction is treated as an atomic unit and MUST NOT be partially emitted.

### 4.4 Injected Validation Header (Mandatory, Single Injection)
A single enforcement directive MUST be injected once, prior to all DDR-derived ModeInstructions, by the ingestion tool at runtime.

This header MUST:
- Precede the complete set of ModeInstructions
- Apply collectively to all instructions that follow
- Be injected by the tool, not repeated per instruction

### 4.5 Ambiguity as a Hard Failure
Ambiguity or contradiction within a ModeInstruction is treated as a hard failure condition at runtime.

In such cases:
- The LLM MUST stop processing the instruction set
- The LLM MUST clearly describe the ambiguity or contradiction
- No attempt may be made to best-guess or infer intent

### 4.6 No Silent Recovery
The ingestion tool MUST NOT attempt to automatically resolve conflicting instructions, missing sequencing, or unclear constraint strength. Such issues MUST be surfaced for human review and correction at the DDR level.

---

## 5. Extraction Rules and Heuristics

### 5.1 Extraction Scope
Only DDR content that directly affects LLM-executed behavior is eligible for extraction.

Eligible content includes:
- Procedural steps and ordered workflows
- Approval gates and progression rules
- Explicit constraints and prohibitions
- Preconditions and termination conditions

Content that does not affect execution MUST NOT be extracted.

### 5.2 Section Eligibility
The ingestion tool MUST evaluate DDR sections for eligibility based on intent, not title.

Typical eligible sections:
- Workflow definitions
- Step-by-step processes
- Validation and approval requirements
- Failure and exception handling

Typical ineligible sections:
- Purpose, background, or narrative context
- Rationale and design tradeoffs
- Historical notes or examples for humans

Section titles MAY guide extraction but MUST NOT be relied upon exclusively.

### 5.3 Procedural Step Identification
Procedural steps are identified by explicit numbering or ordering, imperative language, and gating language.

When ordering is implied but not explicit:
- The ingestion tool MUST make ordering explicit, or
- Fail extraction and surface ambiguity per Section 4

### 5.4 Constraint and Prohibition Detection
The ingestion tool MUST detect and extract absolute requirements, prohibitions, and conditional constraints.

Constraints MUST be emitted as standalone ModeInstructions, not embedded inside procedural steps.

### 5.5 Approval Gates and Human Intervention Points
Any DDR rule requiring explicit human approval, manual verification, or external validation MUST be extracted as a blocking instruction that prevents progression and cannot be auto-satisfied by the LLM.

### 5.6 Conditional Logic Handling
Conditional logic in DDRs MUST be preserved explicitly. Conditions MUST be stated explicitly and dependent instructions MUST not be emitted without their conditions. Implicit conditions MUST NOT be inferred.

### 5.7 Duplication and Redundancy
If a rule appears multiple times within a DDR:
- The ingestion tool MUST de-duplicate identical instructions
- The strongest applicable normative strength MUST be preserved

Duplicate rules DO NOT constitute a failure condition.

However:
- The ingestion tool SHOULD surface a warning indicating duplication
- The warning MUST NOT block ModeInstruction generation

If duplicated rules are conflicting in meaning or strength, this MUST be treated as an ambiguity failure per Section 4.

---

## 6. Normalization and Compression Strategy

### 6.1 Normalization Before Compression
All extracted content MUST be normalized before any attempt at compression.

Normalization includes:
- Converting descriptive language into imperative form
- Isolating single responsibilities per instruction
- Making implicit ordering explicit
- Resolving cross-references into standalone statements

Compression MUST NOT occur on unnormalized content.

### 6.2 Imperative Rewrite Rules
Each instruction MUST:
- Begin with its normative keyword (per Section 2.7)
- Use direct, unambiguous verbs
- Avoid pronouns, references, or context-dependent phrasing

### 6.3 Instruction Decomposition
Compound instructions MUST be decomposed. If a sentence contains multiple actions or mixes validation and execution, it MUST be split into multiple instructions with explicit ordering or conditional structure.

### 6.4 Ordering and Grouping
The ingestion tool MUST preserve logical ordering by:
- Assigning explicit sequence numbers or ordered grouping when needed
- Keeping gating instructions before dependent actions
- Ensuring termination conditions appear before dependent execution steps where applicable

Reordering for stylistic reasons is prohibited.

### 6.5 Compression Boundaries
Compression is permitted only when:
- Two instructions are semantically identical
- One instruction is strictly subsumed by another
- Redundant phrasing can be removed without altering meaning

Compression MUST NOT change normative strength, remove conditional logic, or merge instructions of differing intent.

### 6.6 Vocabulary Control
The ModeInstruction vocabulary SHOULD be intentionally limited:
- Prefer consistent verbs for repeated actions
- Avoid synonyms that introduce interpretive variance
- Normalize domain-specific terms to their DDR-defined meanings

If vocabulary normalization would obscure meaning, fidelity takes precedence.

### 6.7 Explicitness Over Elegance
ModeInstructions favor explicit clarity over linguistic elegance. Awkward or repetitive phrasing is acceptable if it reduces ambiguity and improves deterministic execution.

### 6.8 Multi-Phase Validation Strategy
Validation of ModeInstructions occurs at two distinct phases.

#### 6.8.1 Ingestion-Time Validation
During DDR ingestion and ModeInstruction generation, the tool MUST perform checks for ambiguity, contradictory instructions, and missing constraints or ordering.

Failures detected at this phase:
- MUST NOT block DDR ingestion
- MUST be surfaced to the human as warnings

#### 6.8.2 Injection-Time Validation
At runtime, when ModeInstructions are injected into the LLM, validation is enforced via the injected header defined in Section 4.

Failures detected at this phase:
- MUST halt execution
- MUST be explicitly reported by the LLM
- MUST NOT be automatically corrected or inferred around

#### 6.8.3 Determinism Expectations
This DDR does not require ingestion-time and injection-time validation behavior be perfectly deterministic or identical.

However:
- Implementations MAY share validation logic or wording
- Behavioral convergence is expected in practice
- Correctness and safety take precedence over strict parity

---

## 7. Failure Modes and Safeguards

### 7.1 Failure Classification
Failures encountered during ModeInstruction processing fall into three categories:
1. Ingestion-time warnings
2. Injection-time hard failures
3. Specification integrity failures

### 7.2 Ingestion-Time Warning Conditions
The ingestion tool MUST surface warnings when it detects:
- Ambiguous language
- Redundant or overlapping rules
- Implicit ordering or missing clarity
- Inferred normative strength

Warnings MUST NOT block ingestion and MUST be visible to the human.

### 7.3 Injection-Time Hard Failure Conditions
Injection-time failures MUST halt execution immediately.

Triggers include:
- Ambiguity detected by the LLM
- Contradictory instructions
- Missing required constraints
- Unresolvable ordering dependencies

In such cases, the LLM MUST stop processing, MUST explicitly describe the issue, and MUST NOT attempt correction or continuation.

### 7.4 No Silent Degradation
The system MUST NOT ignore validation failures, degrade into best-effort behavior, or substitute inferred intent for explicit instruction.

### 7.5 Human-in-the-Loop Safeguard
All failures are ultimately resolved through human intervention. The system MUST surface failures clearly and provide sufficient detail for diagnosis.

### 7.6 Observability and Feedback Loop
Failure and warning data SHOULD be captured to improve DDR authoring practices, refine ingestion heuristics, and identify recurring specification defects.

---

## 8. Worked Example (Conceptual)

### 8.1 Purpose of the Example
This example illustrates how DDR operational intent becomes ModeInstructions. It is illustrative only and avoids code and implementation detail.

### 8.2 Source DDR Intent (Conceptual)
Assume a DDR defines an ordered workflow with a mandatory human approval gate, a prohibition against proceeding under certain conditions, and narrative explanation.

### 8.3 Extracted Operational Rules
The ingestion tool identifies initialization, validation prior to execution, human approval requirements, and prohibitions against continuing if validation fails. Narrative rationale is ignored.

### 8.4 Normalized ModeInstructions (Conceptual)
- MUST perform initialization before any other step.
- MUST validate all required inputs prior to execution.
- MUST NOT proceed if validation fails.
- MUST halt and await explicit human approval before execution.

### 8.5 Injected Validation Header (Conceptual)
Prior to the instruction list, the tool injects a validation header that applies to the instruction set as a whole.

### 8.6 Failure Illustration
If the instruction set contains conflicting rules or unclear ordering dependencies, the LLM halts and reports the issue, requiring human remediation.

### 8.7 Key Takeaways
ModeInstructions are executable contracts, narrative is discarded, explicitness and determinism are prioritized, and validation occurs at both creation and execution time.

---

## 9. Operational Considerations

### 9.1 Instructional vs Referential DDRs
This DDR focuses on ModeInstructions derived from procedural DDR content. Not all DDR content is eligible for ModeInstruction derivation.

### 9.2 Context-Specific ModeInstruction Injection
ModeInstructions are context-scoped, not global.

At runtime:
- Only ModeInstructions relevant to the active mode MUST be injected
- Irrelevant instructions MUST NOT be included

This prevents instruction overload and misapplication.

### 9.3 Coexistence with Referential Knowledge
ModeInstructions constrain execution. Other DDR knowledge may inform reasoning through other mechanisms, but must not override active ModeInstructions.

### 9.4 Mode Selection as a Safety Boundary
Agent modes define which ModeInstructions are active. Switching modes MUST reevaluate injected ModeInstructions.

### 9.5 Avoiding Global Instruction Accumulation
The system MUST NOT accumulate instructions across unrelated modes or tasks.

### 9.6 Operational Implications
This approach ensures strong procedural compliance while keeping instruction injection precise and context-relevant.

---

## 10. Example ModeInstruction Summary Derived from SYS-004

This example illustrates how the approved DDR SYS-004 - Aptix File and Patch Contract can be summarized into ModeInstructions.

### 10.1 Example modeInstructions (derived from SYS-004)
- MUST treat the phrases "aptix file bundle" and "aptix file patch" as hard mode selectors.
- MUST select exactly one response mode per request: File Bundle Mode or Patch Mode.
- MUST NOT mix File Bundle and Patch modes in a single response.
- MUST emit a top-level `files` array when operating in File Bundle Mode.
- MUST emit a top-level `patches` array when operating in Patch Mode.
- MUST ask for clarification or choose the more restrictive mode when invocation intent is ambiguous.
- MUST emit JSON only when a machine-applicable bundle or patch is requested or clearly implied.
- MUST include a `root` field and ensure all paths are relative to it.
- MUST use POSIX-style path separators and avoid `./` and `../` in all paths.
- MUST restrict file operations to the allowed set: `create`, `replace`, `delete`, `patch`, `gitPatch`.
- MUST include `content` for `create` and `replace` operations.
- MUST NOT include `patches` for `create` or `replace` operations.
- MUST NOT include `content` or `patches` for `delete` operations.
- MUST include a `patches` array for `patch` operations.
- MUST include unified diff content for `gitPatch` operations.
- MUST ensure all patches are nested inside a `files[]` entry when in File Bundle Mode.
- MUST NOT create, delete, or fully replace files when operating in Patch Mode.
- MUST refuse to emit JSON and respond in natural language when a safe patch cannot be produced.
- MUST assume only the following top-level directories exist: `./ddrs`, `./ddrs/jsonl`, `./src`, `./tests`.
- MUST NOT assume any other top-level folders exist unless the user explicitly provides or confirms them.
- MUST serialize JSON using pretty-printed, multiline formatting.
- MUST escape all JSON string content according to JSON rules.
- MUST NOT emit raw multiline strings inside JSON fields.
- MUST validate JSON serialization before emitting any bundle.
- MUST emit JSON inside a single guarded code block.
- MUST refuse to emit JSON if correctness or safety cannot be guaranteed.

---

## 11. Tool Usage Instructions (Baseline DDR Ingestion and ModeInstruction Derivation)

The following instructions define the baseline behavior for tools that ingest DDRs and derive ModeInstructions.

These rules apply by default unless a future DDR explicitly defines alternative ingestion or transformation behavior.

```text
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
