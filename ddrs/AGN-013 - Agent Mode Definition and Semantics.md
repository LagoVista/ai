# AGN-013 — Agent Mode Definition and Semantics

**ID:** AGN-013

**Title:** Agent Mode Definition and Semantics

**Status:** Approved

**Approved By:** Kevin Wolf

**Approval Timestamp:** 2025-12-06 14:35:00 EST (UTC-05:00)

---

# 1. Purpose

This DDR defines the **data model**, **semantics**, and **system-wide responsibilities** of an Aptix Agent "Mode". The Mode framework provides structured behavioral control over the agent by specifying:

- Which tools are available
- What system prompts and instructions the agent receives
- How RAG scoping behaves
- How to recognize when a user intends to use this mode
- What happens when switching to a new mode

Modes are **design-time**, **immutable**, **stateless**, and **centrally registered**. They form the foundation for multi-domain agent reasoning.

This DDR does **not** define how modes are persisted (TUL-009) or how tools are filtered (TUL-008). It defines the Mode object itself.

---

# 2. Role of a Mode in the Aptix Ecosystem

A Mode is a **behavioral contract** that shapes the agent's operations during a session. Each Mode influences four major subsystems:

1. **Prompt construction** (AGN-011 / AGN-012) — via instructions, hints, and framing
2. **Toolbelt availability** (TUL-008) — via lists of associated tool IDs
3. **RAG context shaping** — via guidance on collections and boosts
4. **Mode recognition** (TUL-010) — via Strong/Weak signals

Modes are never created dynamically; they are defined once and distributed through the Mode Catalog (TUL-010).

---

# 3. Mode Data Model

A Mode is a static configuration object containing the following fields.

## 3.1 Identity & UI Metadata

### `Id`
- A **GUID with hyphens removed** (e.g., `"a4f731928cab40e8b5ac317830d7dd17"`).
- The **canonical immutable key**.
- Stored in sessions (TUL-009) and used for lookup.

### `Key`
- Human-readable identifier (e.g., `"General"`, `"DDR Authoring"`).
- May change without affecting system integrity.

### `Description`
- Short explanation of the Mode's purpose and domain.

### `When to Use`
- A single-line sentence that describes when this mode should be selected.
- Optimized for inclusion in system prompts and catalogs.
- Used by the Mode Catalog and prompt builders to tell the LLM: “If the user request looks like X, prefer this mode.”
- MUST be concise enough to read as:
```
<key>: <when-to-use sentence>
```

### `Summary`
- Description explains what the mode is and covers.
- WhenToUse explains when the agent should choose this mode.

---

## 3.2 User Interaction Metadata

### `WelcomeMessage`
A message delivered once when switching into this Mode.

### `ModeInstructions`
Instructions describing expected LLM behavior while in this Mode.

### `BehaviorHints` (optional)
Structured hints such as:
- `preferStructuredOutput`
- `avoidDestructiveTools`

---

## 3.3 Tools

### `AssociatedToolIds`
List of tool IDs that are enabled when this Mode is active.

### `ToolGroupHints` (optional)
Small metadata used for UI grouping or LLM reasoning hints.

---

## 3.4 RAG Scoping Metadata

### `RagScopeHints`
Guidance on:
- Preferred RAG collections
- Tags to boost or avoid
- Collections to exclude

---

## 3.5 Recognition Metadata

Used by Mode classification (TUL-010) and optionally by the LLM.

### `Recognition.StrongSignals`
Phrases strongly associated with this Mode.

### `Recognition.WeakSignals`
Hints that could lean toward this Mode but are not definitive.

### `Recognition.ExampleUtterances`
Representative user requests that clearly belong to this Mode.

---

## 3.6 Lifecycle Metadata

### `Status`
`"active"`, `"experimental"`, or `"deprecated"`.

### `Version`
Simple version string (e.g., `"v1"`).

---

# 4. Statelessness Rules

Modes MUST remain:
- **Design-time only**
- **Immutable**
- **Stateless**
- **Cross-session shared**

No Mode may store or depend on:
- Current DDR being authored
- Current workflow being edited
- User decisions
- Partial results

All session/runtime state must live in:
- `AgentSession` (TUL-009)
- Conversation/task models
- Backend domain objects

Mode holds **rules**, never **state**.

---

# 5. How Other Systems Consume Modes

## 5.1 Prompt Assembly (AGN-011 / AGN-012)
Prompt builders consume:
- `Key`, `Description`
- `WelcomeMessage`
- `ModeInstructions`
- `BehaviorHints`
- `RagScopeHints`

Prompts must always identify the **current active Mode**.

---

## 5.2 Toolbelt Construction (TUL-008)
The toolbelt layer uses:
- `AssociatedToolIds`
- `ToolGroupHints`

Only tools listed here are available to the LLM in this Mode.

---

## 5.3 Session Layer (TUL-009)
- Stores only the **Mode.Id**.
- Determines when to switch modes.
- Emits the Mode welcome message.

---

## 5.4 Mode Detection (TUL-010)
Consumes:
- StrongSignals
- WeakSignals
- ExampleUtterances

Used for classification but not stored in session state.

---

# 6. Relationship to Mode Catalog (TUL-010)

The Mode Catalog is the **single source of truth** for all Mode definitions.

It must:
1. Store all Modes statically
2. Enforce unique `Id` and `Key`
3. Provide lookup by Id and Key
4. Expose enumeration for UI, LLM detection, debugging

Mode definitions evolve only through DDRs.

---

# 7. Extensibility, Versioning & Validation

## 7.1 Extensibility
Modes may gain fields but must remain:
- Stateless
- Design-time
- Backward-compatible

## 7.2 Versioning
`Version` increments when:
- Instructions change
- Tool associations change
- Recognition signals change

`Id` never changes.

## 7.3 Validation Rules
Catalog must validate:
- `Id` is a GUID (hyphens removed)
- `Key` is unique and non-empty
- Recognition lists are strings
- Tools referenced actually exist
- No circular Mode references

---

# 8. Example Mode Definitions

Two fully worked examples (General Mode and Workflow Authoring Mode) illustrate how the Mode schema is applied in practice.

These examples are conceptual and not prescriptive, serving to clarify expectations for Mode authors.

---

# End of DDR AGN-013
