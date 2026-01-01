# AGN-000037 — Agent Control Plane

**TLA:** AGN-000037  
**Title:** Agent Control Plane  
**Status:** Approved  
**DDR Type:** Referential  

## Approval Metadata
- **Approved By:** Kevin D. Wolf  
- **Approval Timestamp (Local):** 2025-12-30 15:42 ET  

---

## 1. Purpose & Scope

### Purpose
This DDR defines the **Agent Control Plane (ACP)** and its associated first-class constructs—**Active Work Context (AWC)**, **Control Plane Router (CPR)**, and the supporting resolver/catalog/toolbox/command abstractions—within the agent runtime.

The intent is to make control-plane decisions **deterministic and inspectable**, reduce implicit state tracking inside the LLM, and standardize how UI/tooling interacts with an agent’s current work focus.

### Scope
This DDR specifies:
- Responsibilities and boundaries for ACP, AWC, and CPR
- Core data shapes used for resolution, commands, and context
- UI-first routing rules and control-plane outputs
- Sample control flows (representative end-to-end sequences)

Out of scope:
- Domain/business logic
- LLM prompting strategies beyond the interaction contract
- Tool implementation details (beyond the command contract)
- Persistence guarantees beyond session/runtime scope

---

## 2. Definitions

### Agent Control Plane (ACP)
A lightweight, deterministic, pre-LLM layer responsible for:
- Mode switching (session-level)
- Entity selection/activation (AWC)
- Command routing
- Emitting UI intents (pickers/forms/launchers)

**ACP performs routing and resolution, not reasoning.**

### Active Work Context (AWC)
Authoritative, agent-owned state describing the **current work focus** (active entity selection), including optional business scoping.

AWC answers:
- What domain/sub-scope applies? (e.g., sales)
- What entity type is being worked on?
- Which specific entity instance is active?

**Note:** The active **Mode** is stored at the **session level** (not in AWC). Mode expresses which behaviors are acceptable; AWC expresses *what* the agent is focused on.

### Control Plane Router (CPR)
A deterministic router within ACP that:
- Detects obvious control intent
- Resolves user input against eligible resolver sources
- Emits a single structured action (open picker, invoke command, ask one clarifying question, or continue with LLM)

### Resolver Catalog
A runtime-accessible catalog of selectable items (modes, DDRs, personas, templates, etc.) exposed in an enum-like way for resolution and UI pickers.

### Command Definition
A stand-alone, registerable mapping from a control-plane intent to a tool invocation, constrained to **exactly one parameter** (the resolved `Id`), and declaring context effects (e.g., whether it updates session Mode and/or AWC).

---

## 3. Architecture Overview (Components & Boundaries)

### Components
- **Agent Control Plane (ACP)** (control-plane layer)
  - **Control Plane Router (CPR):** intent detection + resolution + single action emission
  - **Resolver layer:** access to resolver sources (catalog-backed or provider-backed)
  - **Command Registry:** command definitions (single-parameter contract, declared context effects)

- **Session State (authoritative runtime state)**
  - **Mode:** session-level behavioral state (what behaviors are acceptable)
  - **AWC:** active work context (domain + active entity selection + related entities)

- **UI (optional, preferred when available)**
  - Pickers/forms/confirmations driven by resolver sources + commands

- **Tools (execution layer)**
  - Execute actions; may update session state only via explicit command semantics

- **LLM (reasoning/content generation)**
  - Produces outputs within the boundaries of current **Mode** + **AWC**
  - Not a state container; must not invent or mutate session/AWC implicitly

### Control/data boundaries (normative)
- **ACP/CPR**
  - Must be deterministic and inspectable
  - Must not do domain reasoning or multi-step planning
  - Must emit exactly one action per turn

- **Session State**
  - **Mode is session-level** (not stored inside AWC)
  - **AWC is entity focus** (domain + entity type/id), changed only by explicit commands

- **Tools**
  - Implement execution details
  - Must not be indirectly invoked by “best guess” without passing through CPR decision rules (UI-first where available)

- **LLM**
  - Must not override AWC/Mode; only reacts to them

### Minimal runtime loop (conceptual)
```text
User Input
  -> ACP/CPR (detect intent + resolve)
      -> { OpenPicker | InvokeCommand | AskClarifyingQuestion | ContinueWithLLM }
          -> (Optional) Tool execution + session/AWC update
              -> LLM response within current Mode + AWC
```

---

## 4. Active Work Context (AWC)

### Data shapes
```text
EntityHeader
- Id           (string)   // machine-readable identifier (canonical)
- DisplayName  (string)   // human-readable name/title
```

```text
RelatedEntityRef
- EntityType   (string)   // e.g. "persona"
- Header       (EntityHeader)
- Role         (string)   // e.g. "audience", "source", "owner"
```

```text
ActiveWorkContext
- Domain           (string?)             // optional business scope, e.g. "sales"
- EntityType       (string?)             // active work target type, e.g. "email_template"
- EntityHeader     (EntityHeader?)       // active work target header
- RelatedEntities  (RelatedEntityRef[]?) // contextual references
```

### Interpretation notes (normative)
- **Mode is session-level state** and is not stored in AWC.
- AWC is interpreted within the current session’s Mode (Mode governs acceptable behaviors; AWC governs current work focus).

### Lifecycle & rules (normative)
- Exactly **one active work target** may exist at a time (`EntityType` + `EntityHeader.Id`).
- AWC is modified **only** via explicit commands/tools (never inferred by the LLM).
- `RelatedEntities` may be populated/updated when an entity is loaded/activated, based on known relationships.
- The user can explicitly “switch focus” to a related entity; that is a context switch (the related entity becomes the active work target).

### Invariants
- If `EntityHeader` is set, `EntityType` must also be set.
- `EntityHeader.Id` is the canonical identity used for tool invocation and persistence lookups.
- `EntityHeader.DisplayName` is for UX and can be stale; it must not be treated as authoritative identity.
- If `RelatedEntities` are present, each must include `EntityType`, `Header.Id`, and `Role`.

---

## 5. Resolver Catalogs

### Purpose
Resolver catalogs provide enum-like selectable data (modes, personas, DDRs, templates, etc.) for:
- Deterministic resolution (text → canonical `Id`)
- UI pickers (browse/search/select)
- Reducing schema coupling (ACP routes without knowing tool schemas)

### Catalog item shape
```text
ResolverItem
- Header        (EntityHeader)   // Header.Id is canonical; Header.DisplayName is UX
- Description?  (string?)
- Aliases[]     (string[])
- Keywords[]    (string[])
- UsageStats?   (object?)        // optional; informational only
```

```text
ResolverCatalog
- CatalogId     (string)         // e.g. "modes", "ddrs", "personas", "email_templates"
- Items[]       (ResolverItem[])
- Version?      (string?)
- RefreshedAt?  (timestamp?)
- TTLSeconds?   (number?)
```

### ResolverSource (catalog-backed or provider-backed)
Resolver items may come from either:
- A **CatalogId** (data/UI-managed catalog), or
- A **ProviderId** (code-backed provider)

In both cases, the output is the same: a list of `ResolverItem[]` suitable for pickers and resolution.

### Toolbox-based catalog activation (normative)
Catalogs are **atomic** and are activated via configured **Toolboxes**. A session’s current **Mode** may include **multiple toolboxes**, and the active toolboxes determine which catalogs ACP/CPR can use for resolution.

```text
ToolboxDefinition (relevant fields)
- ToolboxId        (string)
- DisplayName      (string)
- CatalogIds[]     (string[])     // catalogs enabled by this toolbox
- CommandIds[]?    (string[])     // optional: commands exposed by this toolbox
- RequiredRoles[]? (string[])     // optional gating
```

```text
ModeDefinition (relevant fields)
- ModeId           (string)
- DisplayName      (string)
- ToolboxIds[]     (string[])     // multiple toolboxes can be active in a mode
```

### Activation rules (normative)
- ACP/CPR resolves only against catalogs whose `CatalogId` appears in the **union** of `CatalogIds` across the session’s active toolboxes (as determined by current Mode and role gating, if present).
- When Mode changes, the active toolbox set (and therefore active catalogs) may change accordingly.
- If a catalog is not active in the current Mode/toolbox set, it must not be used for resolution or shown in pickers.

---

## 6. Command Definitions (Single-Parameter Contract)

### Purpose
Commands are stand-alone, registerable definitions that ACP can route to deterministically. They map user control intent to “real code” (tools/functions) while keeping ACP decoupled from tool schemas.

### Core rule (normative)
Every command invocation accepts **exactly one parameter**, whose value is a resolved canonical **Id** (`EntityHeader.Id`).

### Multi-turn flows (clarification)
CPR emits **exactly one action per turn**, but a user journey may span multiple turns (e.g., open picker → user selects → invoke command). Two-step experiences are implemented as **multiple stand-alone commands**, not multi-parameter commands.

### Command classes
- **Executable command**: once a `ResolvedId` is known, it is invoked immediately.
- **Launcher command**: an entry-point command whose purpose is to deterministically lead to a selection step (typically by opening a picker for a target catalog and/or ensuring the correct Mode/toolboxes are active). Launcher commands remain stand-alone and registerable.

### Command definition shape
```text
CommandDefinition
- CommandId             (string)
- DisplayName           (string)
- Description?          (string?)

- CommandKind?          ("executable" | "launcher")

- ToolName              (string)    // executable implementation hook
- SingleParameterName   (string)    // exactly one parameter name
- ResolverSource        (one-of)    // where the selectable Ids come from
    - CatalogId         (string)
    - ProviderId        (string)    // code-backed provider returning ResolverItem[]

- PickerType?           (string?)   // UI hint

- TargetCatalogId?      (string?)   // for launcher: which catalog should be presented next (if not implied)
- TargetModeId?         (string?)   // for launcher: which Mode should be active (optional; session-level)

- SetsSessionMode?      (bool)
- SetsActiveContext?    (bool)
- ActiveEntityType?     (string?)   // required if SetsActiveContext = true

- RequiresConfirmation? (bool)
- ProducesSideEffects?  (bool)
```

---

## 7. Control Plane Router (CPR)

### Purpose
CPR detects obvious control intent, resolves against eligible resolver sources, and emits **exactly one** structured action.

### Responsibilities (normative)
1. Detect obvious control intent.
2. Determine eligible resolution space (only active catalogs per Mode + toolboxes; or eligible providers).
3. Resolve user input.
4. Emit **exactly one** action.

### Outputs (normative)
CPR must emit **exactly one** action per turn, chosen from:
1) **OpenPicker**
2) **InvokeCommand**
3) **AskClarifyingQuestion** (exactly one question)
4) **ContinueWithLLM**

### Action shapes
```text
OpenPicker
- PickerType      (string?)
- ResolverSource  (CatalogId | ProviderId)
- PrefilterText?  (string)
- HighlightId?    (string?)
```

```text
InvokeCommand
- CommandId       (string)
- ResolvedId      (string)
```

```text
AskClarifyingQuestion
- QuestionText    (string)
- Options?        (string[])
```

```text
ContinueWithLLM
- ReasonCode?     (string?)
```

### Non-responsibilities (normative)
- No domain reasoning
- No multi-step planning
- No retries/loops
- No prompt rewriting
- No implicit mutation of session Mode or AWC

---

## 8. UI-First Routing Rules

### Rule of thumb (normative)
- **If UI is available**
  - Prefer **OpenPicker** when resolution is not a strict exact match.
  - Prefilter the picker using the user’s input.
  - Highlight the best candidate, but do not auto-execute unless policy explicitly allows.

- **If UI is not available**
  - Auto-invoke only when resolution confidence is strict.
  - Otherwise emit **AskClarifyingQuestion** (exactly one question).

- **If uncertain / out of scope**
  - Emit **ContinueWithLLM**.

### Confirmation & safety (normative)
Commands marked `RequiresConfirmation = true` (and/or `ProducesSideEffects = true`) must not execute without an explicit confirmation step.

---

## 9. Sample Control Flows

### Flow A — One-and-done: switch to DDR mode
```text
User: "switch to DDR mode"
CPR: detect intent -> SetMode
Resolve: modes -> "ddr"
Action: InvokeCommand(SetMode, "ddr")
Tool: set_mode("ddr")
Session: Mode="ddr" (toolboxes/cats updated accordingly)
```

### Flow B — Launcher: “work on email templates” (UI available)
```text
User: "I want to work on email templates"
CPR: launcher intent -> OpenEmailTemplates

Action (turn 1): OpenPicker(CatalogId="email_templates", prefilter="email templates")
User selects: "TPL-123 (Q1 CFO Outreach)"

Action (turn 2): InvokeCommand(SetActiveEmailTemplate, "TPL-123")
Tool: set_active_email_template("TPL-123")
AWC: EntityType="email_template", EntityHeader.Id="TPL-123"
RelatedEntities: may be populated (e.g., audience persona)
```

### Flow C — Template with related persona context
```text
Precondition: AWC active template = TPL-123
Tool loads relationships:
  RelatedEntities += { EntityType="persona", Header={Id="PERS-22", DisplayName="CFO - MidMarket"}, Role="audience" }

User: "rewrite this template for the CFO persona"
CPR: ContinueWithLLM
LLM: uses active template + related persona as context
```

### Flow D — Side-effecting action requires confirmation
```text
User: "send this to the Q1 pilot list"
CPR: SendTemplateToMailerList; resolve mailer list -> "LIST-9"
Action: AskClarifyingQuestion("Confirm send to 'Q1 pilot list'?", ["Yes","No"])
User: "Yes"
Action: InvokeCommand(SendTemplateToMailerList, "LIST-9")
Tool executes (using active template from AWC as needed)
```

---

## 10. Non-Goals & Guardrails

### Non-goals (normative)
- ACP/CPR must not perform domain reasoning.
- ACP/CPR must not do multi-step planning or retries/loops.
- ACP must not store implicit memory.
- ACP must not require tool schema awareness beyond command definitions (single-parameter contract).
- The LLM must not implicitly set or override session Mode or AWC.

### Guardrails (normative)
- **One-action constraint:** CPR emits exactly one action per turn.
- **Explicit state changes only:** Session Mode and AWC may be modified only via explicit commands/tools with declared context effects.
- **Confirmation for side effects:** commands marked as confirmation-required/side-effecting must not execute without explicit confirmation.
- **LLM-initiated actions are still gated:** The LLM may propose/initiate actions via tooling, but execution must still pass through the same confirmation/authorization guardrails at the command execution boundary.

---

## 11. Extensibility & Naming Canonicalization

### Extensibility pattern (normative)
Adding a new capability should require only:
1. A resolver source (CatalogId and/or ProviderId)
2. One or more command definitions (executable and/or launcher)
3. Association to one or more toolboxes, activated via Mode → ToolboxIds

No changes to CPR logic should be required.

### Canonical names
- Agent Control Plane (ACP)
- Active Work Context (AWC)
- Control Plane Router (CPR)
- EntityHeader
- Resolver Catalog / ResolverItem
- Toolbox
- CommandDefinition
