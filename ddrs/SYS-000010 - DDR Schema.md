# Metadata
TLA: SYS-000010
Title: DDR Schema
Type: Referential 
Summary: This DDR describes the exact format and parts required to create a DDR. SYS-001 is our workflow; this is the expected output.
Status: Approved
Creator: Kevin D. Wolf
References: SYS-001, SYS-004
Creation Date: 2026-01-01T20:03:30.805Z
Last Updated Date: 2026-01-01T20:03:30.805Z
Last Updated By: Kevin D. Wolf

# Body
## 1 - Overview
Summary: Defines the purpose and scope of the DDR Schema and clarifies that SYS-001 defines the workflow while this document defines the output format.
Purpose
This DDR defines the authoritative schema (format + required parts) for all DDRs. It specifies required metadata, allowed DDR types, required document block structure, body chapter structure rules, approval/finalization fields, and output artifact location rules so that DDRs are consistent and machine-retrievable.

Scope
This schema applies to any DDR authored in this system, regardless of domain topic.

Relationship to SYS-001
- SYS-001 defines the workflow for producing DDRs.
- SYS-000010 defines the expected DDR artifact format produced by that workflow (i.e., what the resulting DDR must contain and how it is structured).

## 2 - Definitions
Summary: Defines key terms used throughout this schema so field names, structure, and approval semantics are interpreted consistently.
- DDR: A Decision / Design Document Record used to capture a decision, rationale, constraints, and the resulting standard or guidance.
- Schema: In this DDR, “schema” means the required fields, sections, ordering, and gating rules that constitute a valid DDR.
- Title: Human-readable name of the DDR.
- TLA: Short identifier (typically an acronym) used to reference the DDR.
- Summary: A short description (1–3 sentences) describing what the DDR is and why it exists.
- DDR Type: The single primary classification for a DDR, restricted to: Instruction, Referential, Generation, Policy / Rules / Governance.
- Expected Outputs: The explicit list of artifacts the DDR authoring effort will generate (e.g., a single markdown DDR file).
- Approval fields: The required “sign-off” fields that indicate the DDR is formally approved (approver name + approval timestamp).
- Final: A DDR state reached only when the human explicitly approves it and the approval fields are recorded.
- Aptix File Bundle: The required delivery mechanism for generated artifacts (details governed by SYS-004).

## 3 - DDR Metadata Schema (Required)
Summary: Defines the required metadata fields and the strict formatting rules used to store them in markdown.
Required metadata fields for compliant DDRs (one per line using `Field Name: value`):
- TLA:
- Title:
- Type:
- Summary:
- Status:
- Creator:
- References:
- Creation Date:
- Last Updated Date:
- Last Updated By:

Field encoding rules:
- One field per line.
- Field lines MUST use the format: `Field Name: value`.
- Field names MUST match the schema labels exactly.
- Labels may never contain a colon.

TLA format:
- MUST be exactly 10 characters in the form `AAA-NNNNNN`.
- Example: `SYS-000010`.

Status enumeration:
- Draft | In Review | Approved | Tabled | Cancelled | Superseded

References:
- Comma-delimited list (minimal format for now).

Timestamps:
- `Creation Date` and `Last Updated Date` MUST be ISO 8601 UTC in the exact format `YYYY-MM-DDThh:mm:ss.mmmZ` (e.g., `1/1/2026, 3:03:30 PM`).
- On initial save: `Last Updated Date = Creation Date` and `Last Updated By = Creator`.

Filename convention:
- MUST be `<TLA> - <Title>.md`.

## 4 - DDR Type Schema (Required)
Summary: Restricts DDRs to exactly one primary type selected from a fixed allowed set.
Allowed types (exactly one):
- Instruction
- Referential
- Generation
- Policy / Rules / Governance

## 5 - DDR Document Structure (Required Blocks & Order)
Summary: Defines the universal top-level blocks that every DDR must include and the exact header strings used.
Every DDR MUST use these top-level blocks in this order:
1) `# Metadata` (MUST be line 1)
2) `# Body`
3) `# Approval`

## 6 - Body Structure Schema (Required)
Summary: Defines the required structure for chapters inside `# Body` while keeping chapter content free-form.
- `# Body` MUST contain one or more chapters.
- Each chapter MUST use: `## <ChapterNumber> - <Chapter Title>`.    
- `<ChapterNumber>` MUST use dotted numeric form (e.g., `1`, `1.1`, `2.1.3`).
- The separator between number and title MUST be exactly ` - `.
- Immediately under each chapter heading there MUST be: `Summary: <text>` (1–3 sentences).
- Chapter content is otherwise free-form; deeper headings MAY use `###`, `####`, etc.

## 7 - Output Correctness & Compliance
Summary: Defines minimum requirements for drafts and full compliance requirements for generated/approved outputs.
Draft minimum requirements (MUST):
- Drafts MUST always have: TLA, Title, Summary, Creator, Creation Date, Last Updated Date, Last Updated By.

Compliant DDR requirements (MUST):
- Compliant outputs MUST satisfy all structural requirements in this schema, including required blocks/order, full required metadata set, body chapter rules, filename/TLA alignment, and approval rules.

## 8 - Approval & Finalization Schema
Summary: Defines the required fields in `# Approval` and the rules for considering a DDR final.
If `Status: Approved`, the `# Approval` block MUST contain:
- `Approver:`
- `Approval Timestamp:`

`Approval Timestamp` MUST be ISO 8601 UTC in the exact format `YYYY-MM-DDThh:mm:ss.mmmZ`.
A DDR MUST NOT be treated as final unless the human explicitly approves it and these fields are present and populated.

## 9 - Output Artifact Definition
Summary: Defines the authoritative storage artifact, consumption model, and required repository paths for DDR assets.
- Source-of-truth is the markdown DDR file, used as a durable datastore for the agent/LLM.
- Primary interaction is via the agent; human-friendly exports MAY be generated as PDF (summary or detailed) derived from the markdown.
- DDR markdown assets MUST be stored at `ddrs/{ID} - {Title}.md`.
- JSONL assets (if used in the future) MUST be stored at `ddrs/jsonl/{ID}.jsonl`, but are not produced for this DDR.

# Approval
Approver: Kevin D. Wolf
Approval Timestamp: 2026-01-01T20:03:30.805Z
