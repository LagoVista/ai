# AGN-034 — Agent Control Plane (ACP) & Active Work Context (AWC)

**ID:** AGN-034  
**Title:** Agent Control Plane & Active Work Context  
**Status:** Draft  
**DDR Type:** Instructional / Architectural  

---

## Approval Metadata

- **Approved By:** _TBD_  
- **Approval Timestamp:** _TBD_

---

## 1. Purpose & Scope

### Purpose

This DDR defines the **Agent Control Plane (ACP)** and **Active Work Context (AWC)** as first-class architectural constructs within the agent runtime.

The intent is to:
- Remove implicit state tracking from the LLM
- Make control-plane decisions deterministic and inspectable
- Enable fast, UI-first workflows for enum-like selections
- Allow agents to explicitly know *what* they are working on, not just *how* to respond

### Scope

This DDR specifies:
- Responsibilities of the Agent Control Plane
- Structure and lifecycle of Active Work Context
- Data-driven command and catalog abstractions
- Routing rules for control-plane decisions
- Interaction boundaries between ACP, tools, UI, and the LLM

This DDR does **not** define:
- Domain business logic
- LLM reasoning strategies
- Tool implementation details
- Persistence mechanisms beyond session scope

---

## 2. Definitions

### Agent Control Plane (ACP)

A lightweight, deterministic, pre-LLM layer responsible for:
- Mode switching
- Entity selection and activation
- Command routing
- Emitting UI intents (pickers, forms, launchers)

ACP performs **routing and resolution**, not reasoning.

---

### Active Work Context (AWC)

Authoritative, agent-owned state describing the *current focus of work*.

AWC answers:
- What mode is active?
- What entity type is being worked on?
- Which specific entity instance is active?

AWC is explicit and inspectable by:
- Tools
- UI
- LLM
- Agent infrastructure

---

## 3. Active Work Context Model

```
ActiveWorkContext
- ModeId        (string)   // e.g. "business", "ddr"
- Domain        (string?)  // optional, e.g. "sales"
- EntityType    (string?)  // e.g. "persona", "ddr"
- EntityId      (string?)  // e.g. "ABC123"
```

### Rules

- Exactly **one active entity** may exist at a time
- AWC is modified **only** via commands/tools
- LLM must not infer or override AWC implicitly
- Clearing or switching context is an explicit operation

---

## 4. Data-Driven Resolver Catalogs

### Resolver Catalog

Enum-like selectable data (modes, personas, DDRs, templates, etc.) is exposed via runtime catalogs.

Each catalog item provides a **resolver view only**:

```
ResolverItem
- Id
- DisplayName
- Description
- Aliases[]
- Keywords[]
- (optional) UsageStats
```

Resolver catalogs:
- Are cached
- May be refreshed via TTL or invalidation
- Are independent of tool schemas

---

## 5. Command Definition (Single-Parameter Contract)

All control-plane actions are defined declaratively as **commands**.

```
CommandDefinition
- CommandId
- DisplayName
- PickerType
- ToolName
- SingleParameterName
- SetsActiveContext (bool)
- EntityType (if SetsActiveContext = true)
```

### Constraints

- Every command accepts **exactly one parameter**
- The parameter value is always the resolved item `Id`
- ACP never knows tool schemas or execution logic
- Commands declare whether they update AWC

---

## 6. Control Plane Router (CPR)

### Responsibilities

The Control Plane Router:
1. Detects **obvious control intent**
2. Resolves input against resolver catalogs
3. Emits a **single structured action**

Possible outputs:
- OpenPicker (UI-first)
- InvokeCommand
- AskClarifyingQuestion (one turn only)
- ContinueWithLLM

### Non-Responsibilities

- No domain reasoning
- No multi-step planning
- No prompt rewriting
- No retry loops

---

## 7. UI-First Routing Rules

### Rule of Thumb

- **If UI is available**  
  → Open a picker pre-filtered to user input  
  → Highlight best match (never auto-execute unless exact)

- **If UI is not available**  
  → Auto-execute only with strict confidence  
  → Otherwise ask exactly one clarifying question

- **If uncertain or out of scope**  
  → Defer to the LLM

---

## 8. Command Execution Semantics

Execution of a command performs:
1. Tool invocation with the single parameter
2. Optional update of Active Work Context

This establishes:
- Explicit state ownership by the agent
- Predictable transitions between entities
- UI, tools, and LLM alignment

---

## 9. LLM Interaction Contract

The LLM:
- Receives Mode + Active Work Context as authoritative state
- Reasons and generates content *within that scope*
- Does not track entity identity implicitly
- May request UI via an explicit UI-intent tool (optional)

The LLM is a **reasoner**, not a state container.

---

## 10. Extensibility

This pattern applies uniformly to:
- Modes
- Personas
- Email Templates
- DDRs
- Projects
- Environments
- Toolboxes

Adding a new capability requires:
1. A resolver catalog
2. A command definition
3. (Optional) UI picker or form

No changes to routing logic are required.

---

## 11. Naming (Canonical)

- **Agent Control Plane (ACP)** — control-plane layer
- **Active Work Context (AWC)** — authoritative work state

These names are normative going forward.

---

## 12. Non-Goals & Guardrails

- ACP must not perform reasoning
- ACP must not store implicit memory
- ACP must not accept multi-parameter commands
- ACP must not execute destructive actions without confirmation
- If resolution is unclear → ask once or defer to LLM

---

## 13. Outcome

By introducing ACP and AWC:

- Control-plane decisions become deterministic
- Entity focus becomes explicit and inspectable
- UI and chat workflows converge cleanly
- LLM complexity and hallucination risk are reduced
- The agent evolves toward a true domain operating system

---

**End of DDR**
