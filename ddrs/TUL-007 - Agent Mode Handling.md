# TUL-007 — Agent Mode Handling

**ID:** TUL-007  
**Title:** Agent Mode Handling  
**Status:** Draft  
**Owner:** Kevin Wolf & Aptix  
**Scope:** Defines the behavioral contract for modes within Aptix agents.

---

## 1. Purpose

This DDR defines how Aptix agents use **modes** to structure behavior, tool usage, and reasoning. A *mode* represents the **behavioral and domain context** in which an agent operates during an agent session (e.g., "general", "ddr-authoring", "workflow-authoring", "rag-indexing").

TUL-007 focuses on:
- What a mode *is*.
- How the **LLM must behave** with respect to the current mode.
- How the **backend and the LLM coordinate** around mode changes.
- When the LLM should stay in the current mode versus request a mode change.

This DDR intentionally excludes persistence, tool filtering, and system prompt construction, which are deferred to TUL-008, TUL-009, AGN-011, and AGN-012.

---

## 2. Scope and Non-Goals

### 2.1 In Scope
This DDR governs:
- Mode semantics and meaning.
- How the LLM interprets and obeys the current mode.
- Rules for when to propose mode changes.
- The conceptual workflow for LLM-initiated mode change requests.
- Backend–LLM coordination for maintaining consistent mode state.

### 2.2 Out of Scope
The following concerns are covered in other DDRs:
- **Mode persistence** → TUL-009
- **Mode-aware tool filtering** → TUL-008
- **Canonical mode catalog & heuristics** → TUL-010
- **Request construction & prompts** → AGN-011, AGN-012

TUL-007 is strictly about the **behavioral contract** around modes.

---

## 3. Definitions
- **Mode** — An opaque string identifying the current behavioral context (e.g., "general").
- **Current Mode** — The authoritative mode value stored on the backend for an agent session.
- **Mode-Compatible Request** — A user request that can be handled within the current mode.
- **Mode-Incompatible Request** — A request that clearly belongs to another mode’s domain.
- **Mode Change** — A transition from one mode string to another, requested by the LLM via a tool.

---

## 4. Mode Model

### 4.1 Modes as Opaque Strings
- Modes are **string identifiers**, not enums.
- The backend defines which modes exist and their canonical names.
- The LLM must treat modes as opaque; it may not invent new identifiers.

### 4.2 Session-Level Context
- Each agent session has exactly one active mode.
- The mode persists until changed.
- The backend loads and injects the mode into each LLM call.

### 4.3 Default Mode
- Sessions with no mode must default to a safe, broadly applicable mode (typically "general").

---

## 5. LLM Responsibilities

### 5.1 Obey the Current Mode
The LLM must:
- Interpret user requests within the current mode.
- Use reasoning patterns and tools appropriate to that mode.
- Avoid unnecessary mode changes.

### 5.2 Mode-Compatible vs. Incompatible Requests
The LLM must:
- Stay in the current mode for compatible or neutral requests.
- Propose a mode change when the user clearly shifts to a different domain.

### 5.3 Avoid Mode Thrash
The LLM should:
- Prefer stability in ambiguous cases.
- Avoid bouncing between modes unless strongly indicated.

---

## 6. Mode Change Workflow (Conceptual)

### 6.1 Mode Changes Require a Tool
- The LLM is not allowed to silently change modes.
- Mode changes must be requested by calling a server-side tool (defined in TUL-009).
- The tool input must include:
  - `targetMode`: desired mode string
  - `reason`: optional narrative rationale

### 6.2 Backend Authority
- The backend decides whether the requested mode is valid.
- The LLM may not claim a mode change unless the tool indicates success.

### 6.3 Mode Changes Apply to **Future** Calls
- Tools are attached per request; therefore:
  - Current mode cannot change mid-call.
  - Mode changes only affect subsequent LLM requests.

### 6.4 Narrative Expectations
- After a successful mode-change tool call, the LLM should narrate the transition.
- If rejected, the LLM must continue in the prior mode and explain the constraint.

---

## 7. Interaction with Other DDRs

### 7.1 TUL-008 — Mode-Aware Tool Filtering
- Tool availability depends on the mode.
- Changes in mode alter which tools appear in future requests.

### 7.2 TUL-009 — Session Mode Persistence & ChangeMode Tool
- Defines where mode is stored.
- Defines the tool used to change mode.

### 7.3 TUL-010 — Mode Catalog
- Defines canonical modes, their meanings, and identification rules.

### 7.4 AGN-011 / AGN-012
- Define how mode influences request construction, prompts, and RAG context.

---

## 8. Edge Cases
- **Neutral requests**: Answer within the current mode.
- **Mixed requests**: Seek clarification if unsure.
- **Invalid mode requests**: Reject via the tool and explain to the user.
- **Rapid alternation**: Avoid mode thrash; maintain stability unless signals are strong.

---

## 9. Future Extensions
- Richer mode hierarchies
- Policy-driven mode restrictions
- Multi-agent mode negotiation
- Mode-aware memory structures

These must preserve the core contract: modes are session-level, backend-authoritative, string-based, and changed only via explicit tools.
