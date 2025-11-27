# **AGN-001**  
**Title:** Aptix DDR Management Flow  
**Status:** Proposed  
**Domain:** Aptix Orchestrator  
**Owner:** Kevin Wolf  
**Tags:** ddr, specification, aptix, workflow, vs-code-extension  

---

## **Objective**

Define the initial workflow, tooling, and lifecycle for creating, editing, indexing, and refining Design Decision Records (DDRs) within the Aptix ecosystem. This DDR establishes DDRs as first-class artifacts, outlines how the Aptix Reasoner collaborates with users to generate and improve them, and describes how the VS Code extension and Aptix Orchestrator persist, search, and maintain the DDR corpus.

This represents the foundational, low-risk starting point for building the broader Aptix feature flow before enabling automated code generation, refactoring, or multi-step orchestration.

---

## **Context**

Aptix will ultimately orchestrate multi-phase workflows involving:

- design document creation  
- readiness checks  
- implementation planning  
- code generation and patching  
- validation and error correction  
- final review  

Before tackling those higher-complexity flows, the system needs a reliable way to manage the **spec layer** that drives all downstream automation. DDRs represent the durable design artifacts that govern system behavior, architecture, refactoring strategy, and organizational decisions.

Launching Aptix with **DDR management as the first implemented feature** allows:

- controlled experimentation with the `/responses` API  
- practicing structured tool design  
- testing the VS Code user interface and workflows  
- establishing conventions without high-risk code modifications  
- enabling user + LLM collaboration on specs in a safe sandbox  

---

## **Design**

### **1. DDR File Structure**

DDRs are stored as Markdown files under:

```text
/docs/ddr/
   IDX-0001 - <Title>.md
   IDX-0002 - <Title>.md
   …
```

Each DDR begins with the following header block:

```markdown
### <ID>
**Title:** <Title>  
**Status:** Proposed | Accepted | Rejected | Superseded  
**Domain:** <Domain>  
**Owner:** <Owner>  
**Tags:** <comma-separated-tags>

---
```

Following the header, the body should contain structured sections such as:

- **Objective**
- **Context**
- **Design / Approach**
- **Risks / Open Questions**
- **Testing Strategy**
- **Notes / Rationale**

The exact set of sections may vary based on the DDR type (architecture, workflow, decision, policy), but the structure must remain predictable.

---

### **2. Tooling: `ddr_document`**

Aptix defines a tool named `ddr_document` used exclusively to create or update DDRs.  
This tool is purely **assessment-oriented**: the LLM populates the structure; the agent persists it.

#### **Tool Schema**

```jsonc
{
  "name": "ddr_document",
  "description": "Create or update a Design Decision Record (DDR) for the Aptix system.",
  "parameters": {
    "type": "object",
    "properties": {
      "id": { "type": "string", "description": "DDR ID, e.g. 'IDX-0105' (omitted for new DDRs)." },
      "title": { "type": "string" },
      "status": {
        "type": "string",
        "enum": ["Proposed", "Accepted", "Rejected", "Superseded"]
      },
      "domain": { "type": "string" },
      "tags": {
        "type": "array",
        "items": { "type": "string" }
      },
      "owner": { "type": "string" },
      "markdownBody": {
        "type": "string",
        "description": "Markdown body excluding header fields; the agent will write the full file."
      },
      "notes": {
        "type": "string",
        "description": "A short summary of the intent or major changes, especially for updates."
      }
    },
    "required": ["title", "status", "markdownBody"]
  }
}
```

The model must return DDRs exclusively through this tool during DDR creation or refinement.

---

### **3. VS Code Extension Workflow**

The Aptix VS Code extension acts as the user interface for DDR authoring and retrieval. The extension invokes `/responses` with the appropriate mode prompts and tool definitions.

#### **3.1. Create DDR**
User triggers **Aptix: Create DDR**:

1. User provides an initial description or objective.
2. Extension sends request with:
   - system prompt: Aptix DDR Reasoner contract  
   - user description  
   - forced tool: `ddr_document`
3. LLM constructs a structured DDR.
4. Extension:
   - assigns next `IDX-####`
   - writes file to `/docs/ddr/`
   - opens file for user review

#### **3.2. Refine DDR**
User triggers **Aptix: Refine DDR** while a DDR file is open:

1. Extension extracts the full DDR markdown.  
2. Sends it to the LLM with:
   - `[CURRENT_DDR]`
   - "Refine and clarify this DDR" (or user-provided instruction)
   - tool: `ddr_document`
3. LLM returns improved DDR structure.
4. Extension displays diff or overwrites file depending on user preference.

#### **3.3. Search DDRs**
User triggers **Aptix: Search DDRs**:

1. Extension scans `/docs/ddr/`.
2. Sends list of IDs/titles and query text to `/responses`.
3. LLM returns ranked suggestions (initially plain text; `ddr_search_result` tool may be added later).
4. Extension opens selected DDR.

---

### **4. Orchestrator Responsibilities**

The Aptix Orchestrator (backend service) handles:

- Calling the `/responses` API
- Maintaining conversations using `previous_response_id`
- Passing system prompts only on the first call of a session
- Persisting DDR files created via the `ddr_document` tool
- Managing indexing and metadata (optional v1)
- Validating DDR ID sequences and file naming conventions

No code generation, patching, or implementation planning occurs in this phase.

---

### **5. Reasoner Behavior**

The Aptix DDR Reasoner runs under a lightweight global system prompt that instructs:

- Maintain the meaning of user intent  
- Improve clarity, structure, and completeness  
- Use DDR conventions consistently  
- Produce outputs exclusively through the `ddr_document` tool  
- Surface unclear assumptions or ambiguities inside the DDR body (e.g., under Risks / Open Questions)  
- Keep DDRs concise and effective, not verbose or flowery  

The reasoner is free to reorganize a DDR for clarity but must not change the underlying decision without being explicitly asked to.

---

## **Risks / Open Questions**

1. **Standardization vs Flexibility**  
   - How rigid should the DDR format be?
   - Should we enforce required sections?

2. **Versioning**  
   - Should updates replace the original DDR or create new versions (e.g., supersede old)?

3. **Index File**  
   - Should a master `ddr.index.json` be automatically generated and updated?

4. **Search Tooling**  
   - For MVP, plain LLM-based ranking is fine.  
   - Should we introduce a `ddr_search_result` tool for structured results later?

5. **Human-in-the-loop Policy**  
   - DDRs will eventually feed into readiness checks and implementation planning.  
   - HITL rules will be documented separately once the broader workflow stabilizes.

---

## **Testing Strategy**

- Manual end-to-end testing in the VS Code extension:
  - Create new DDR
  - Refine existing DDR
  - Search DDRs
- Validate file names, header formatting, and index sequences
- Validate that DDR markdown renders correctly in VS Code
- Confirm DDRs are stable and readable across multiple refinement passes
- Confirm the LLM observes tool boundaries (no raw markdown unless requested)

---

## **Notes / Rationale**

This DDR defines the first functional slice of Aptix Orchestrator:  
**a complete DDR authoring, refinement, and discovery subsystem**.

It allows the team to:

- Experiment safely with `/responses` and tool calling  
- Build repeatable patterns for structured LLM outputs  
- Practice conversation continuity (`previous_response_id`)  
- Establish workflows without the risk of code modification  
- Enable high-value spec creation before tackling implementation automation  

This represents the foundation of Aptix’s long-term goal:  
**moving from specs → readiness → planning → implementation → validation**,  
with DDRs always serving as the authoritative source of truth.
