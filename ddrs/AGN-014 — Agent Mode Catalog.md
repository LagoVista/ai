# AGN-014 — Agent Mode Catalog

**ID:** AGN-014  
**Title:** Agent Mode Catalog  
**Status:** Approved

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-06 16:30:00 EST (UTC-05:00)

---

## 1. Purpose

This DDR defines the Aptix Agent Mode Catalog and the runtime service responsible for exposing mode metadata to the LLM. It specifies:

- The design-time shape of modes as consumed by the catalog (building on AGN-013).
- How modes are projected into summaries for tools and UI.
- How the catalog exposes modes, tools, and a plain-text Mode Catalog System Prompt block to the LLM.
- How validation and invariants are enforced at startup so the agent never runs with an inconsistent mode set.

AGN-014 focuses on mode discovery, prompt exposure, and toolbelt mapping. It does not redefine mode semantics (AGN-013), session storage (TUL-009), or the low-level behavior of the mode change tool (TUL-007).

---

## 2. Scope and Non-Goals

### 2.1 In Scope

This DDR defines:

- The concrete design-time model used by the Agent Mode Catalog (`AgentMode`).
- The projection type (`AgentModeSummary`) and its relationship to `AgentMode`.
- The contract for the `IAgentModeCatalogService` interface.
- The structure and content of the Mode Catalog System Prompt block.
- Startup validation rules and invariants for the mode catalog.
- The initial seed modes that must exist in the catalog for V1.

### 2.2 Out of Scope

This DDR does not define:

- How modes are persisted in an `AgentSession` (TUL-009).
- How the LLM decides that a different mode is needed (future TUL-010).
- The internal operation of the `agent_change_mode` tool (TUL-007).
- Workflow semantics or workflow catalogs (TUL-006).
- Prompt composition for other concerns (these belong to AGN-011 / AGN-012 / future prompt DDRs).

Mode recognition behavior is intentionally deferred; see Section 9.

---

## 3. AgentMode Data Model (Design-Time)

AGN-013 defines the conceptual Mode object. AGN-014 refines the concrete design-time model used by the catalog.

Each `AgentMode` is a static, design-time configuration object with the following notable fields:

- **Id**
  - Canonical immutable identifier.
  - GUID with hyphens removed, uppercase hexadecimal (32 characters, `0–9A–F`).
  - Persisted in `AgentSession` as the mode identifier.

- **Key**
  - Human-readable, stable string key (for example, `general`, `ddr_authoring`).
  - snake_case, lowercase letters, digits, and underscores only.
  - Used in prompts, tools, and all LLM-facing references to modes.

- **DisplayName**
  - UI-facing display label.

- **Description**
  - Short explanation of what the mode is and what it covers.

- **WhenToUse**
  - Single-line sentence describing when this mode should be selected.
  - Optimized for inclusion in system prompts and catalogs.
  - Rendered as `<key>: <when-to-use sentence>`.

- **IsDefault**
  - Boolean flag indicating which mode is the default when no explicit mode is present.

- **Status**
  - Lifecycle status: `"active"`, `"experimental"`, or `"deprecated"`.

- **Version**
  - Simple version string (for example, `"v1"`, `"v1.1"`).

- **ModeInstructions**
  - Array of short, mode-specific instruction lines for the LLM when this mode is active.

- **BehaviorHints**
  - Optional hints such as `"preferStructuredOutput"`, `"avoidDestructiveTools"`.

- **HumanRoleHints**
  - Short descriptions of what the human is typically doing in this mode, used in prompts and UI.

- **ExampleUtterances**
  - Representative user requests that clearly belong to this mode.

- **AssociatedToolIds**
  - List of tool names (IAgentTool names) that are enabled when this mode is active.

- **ToolGroupHints**
  - Optional grouping hints for UI or reasoning (for example, `"general"`, `"ddr"`, `"workflow"`).

- **RagScopeHints**
  - Simple hints describing preferred or de-emphasized RAG collections or tags.

- **StrongSignals, WeakSignals**
  - Advisory recognition metadata used to suggest when the mode may be appropriate (see Section 9).

`AgentMode` instances are immutable at runtime and loaded into the catalog at startup.

---

## 4. AgentModeSummary Projection

The catalog exposes a lightweight `AgentModeSummary` type for listing and tooling:

- **Id** — copied from `AgentMode.Id` (GUID, no hyphens, uppercase).
- **Key** — copied from `AgentMode.Key` (snake_case).
- **DisplayName** — copied from `AgentMode.DisplayName`.
- **Description** — either `AgentMode.Description` or, if null, `AgentMode.WhenToUse`.
- **SystemPromptSummary** — always `AgentMode.WhenToUse`.
- **IsDefault** — copied from `AgentMode.IsDefault`.
- **HumanRoleHints** — copied from `AgentMode.HumanRoleHints`.
- **ExampleUtterances** — copied from `AgentMode.ExampleUtterances`.

Each `AgentMode` MUST provide a `CreateSummary()` method that projects itself into an `AgentModeSummary`. The catalog uses `CreateSummary()` to implement `GetAllModesAsync`.

---

## 5. IAgentModeCatalogService Contract

The Agent Mode Catalog is exposed via `IAgentModeCatalogService`. The interface has four responsibilities:

1. Enumerate all modes in summary form.
2. Build a Mode Catalog System Prompt block for the LLM.
3. Resolve a mode by key.
4. Resolve the tool list for a mode.

The interface is defined conceptually as:

- `Task<IReadOnlyList<AgentModeSummary>> GetAllModesAsync(CancellationToken cancellationToken)`
  - Returns a summary list of all modes known to the catalog.
  - Used by tools (for example, `agent_list_modes`), UI, and diagnostics.

- `string BuildSystemPrompt(string currentModeKey)`
  - Builds a plain-text Mode Catalog System Prompt block.
  - Uses `currentModeKey` to identify the current mode. If the key is null, empty, or unknown, the default mode is used instead.

- `AgentMode GetMode(string modeKey)`
  - Returns the full `AgentMode` definition for the provided mode key.
  - The `modeKey` parameter MUST be the mode key (for example, `"ddr_authoring"`), not the Id.
  - If no mode with that key exists, the implementation MUST throw an `InvalidOperationException` with a message that includes the invalid key and the list of valid keys.

- `List<string> GetToolsForMode(string modeKey)`
  - Returns all tool IDs associated with the given mode key.
  - The result MUST be a defensive copy (mutating the list must not mutate catalog state).
  - If the mode key is unknown, the method MUST throw the same exception style as `GetMode`.

All mode lookups in this interface are keyed by `Key`, not `Id`. Session storage and persistence use `Id`, while prompts and tool contracts use `Key`.


---

## 6. Mode Catalog System Prompt Block

### 6.1 Purpose

For every LLM call, the agent MUST provide a Mode Catalog System Prompt block that:

- Identifies the current mode by key.
- Lists all available modes and their `WhenToUse` guidance.
- Explains how the LLM should think about mode switching and how to use the `agent_change_mode` tool.

This block is appended as a system-level instruction and is separate from other prompt content (for example, role descriptions, workflow instructions).

### 6.2 Format

The Mode Catalog System Prompt block MUST be plain text and follow this structure:

- First line: `"Current Mode: <modeKey>"`
- Blank line.
- A `"Available Modes:"` section with one line per mode:
  - `"- <key>: <WhenToUse>"`
- Blank line.
- A `"Mode Switching:"` section with three bullet lines describing switching behavior.

A canonical example (for illustration) is:

- `Current Mode: ddr_authoring`
- `Available Modes:`
  - `- general: Use this mode for everyday Q&A, explanation, and lightweight assistance.`
  - `- ddr_authoring: Use this mode when the user wants to create, refine, or validate Aptix DDR specifications following SYS-001.`
  - `- workflow_authoring: Use this mode when defining, editing, or validating Aptix workflows using TUL-006.`
- `Mode Switching:`
  - `- If the user’s request clearly matches another mode’s "when to use" description, you may recommend switching.`
  - `- If the user expresses interest in switching, follow the instructions in the agent_change_mode tool.`
  - `- If you need more detail about modes, call the agent_list_modes tool.`

The exact wording of the Mode Switching bullets MUST remain aligned with TUL-007, which governs the behavior of the agent session mode change tool.

### 6.3 Current Mode Resolution

- The session layer stores the mode as `AgentMode.Id` (GUID, no hyphens, uppercase).
- Before calling `BuildSystemPrompt`, the agent looks up the mode by Id to obtain `AgentMode.Key`.
- `BuildSystemPrompt` is always called with the mode key, never with the Id.
- If the provided key is null, empty, or does not match any mode, the catalog MUST fall back to the default mode and use its key as the current mode.

The agent performs no automatic mode classification; only the LLM, guided by the Mode Catalog system-prompt block and TUL-007’s confirmation rules, may propose and initiate mode changes.

---

## 7. Validation Rules & Invariants

The Agent Mode Catalog MUST be validated at startup. If validation fails, the agent MUST NOT start; initialization should throw an exception and fail fast. This prevents prompt construction and mode switching from operating on an inconsistent or partial mode set.

### 7.1 Mode Id Format

- Every mode Id MUST:
  - Be a 32-character string.
  - Contain only uppercase hexadecimal characters (`0–9`, `A–F`).
  - Represent a GUID with hyphens removed (for example, `"3F8E4F377F7A4C189C7F6A8B9F945C11"`).
- All Id values MUST be unique across the catalog.
- If any Id is missing, malformed, or duplicated, startup MUST fail.

### 7.2 Mode Key Format

- Every mode Key MUST:
  - Be non-empty.
  - Use snake_case with lowercase letters, digits, and underscores only (`[a-z0-9_]+`), for example `"general"`, `"ddr_authoring"`, `"workflow_authoring"`.
- Keys MUST be unique across the catalog.
- If any Key is missing, invalid, or duplicated, startup MUST fail.

### 7.3 Default Mode Requirements

- Exactly one mode MUST have `IsDefault == true`.
- All other modes MUST have `IsDefault == false`.
- If zero or more than one default mode is found, startup MUST fail.

### 7.4 WhenToUse Requirement

- Every mode MUST define a non-empty `WhenToUse` value.
- `WhenToUse` MUST be short enough to be rendered inline in the system prompt as:
  - `<key>: <when-to-use sentence>`
- If any mode has a missing or empty `WhenToUse`, startup MUST fail.

### 7.5 Status Values

- If `Status` is present, it MUST be one of:
  - `"active"`, `"experimental"`, or `"deprecated"`.
- Any other `Status` value is invalid and MUST cause startup to fail.

### 7.6 Validation Timing

- Validation MUST run once at agent startup when the Mode Catalog is constructed.
- No LLM calls or session handling may proceed unless validation succeeds.
- Validation may be implemented as a catalog constructor check or a dedicated validator invoked during initialization; in either case, failures MUST be treated as fatal configuration errors.

---

## 8. Seed Modes for V1

For V1 of the Aptix agent, the catalog MUST define at least the following modes. The exact Id values are fixed by this DDR.

### 8.1 general

- **Id:** `"3F8E4F377F7A4C189C7F6A8B9F945C11"`
- **Key:** `"general"`
- **DisplayName:** `"General"`
- **Description:** General-purpose assistance for everyday Q&A, explanation, and lightweight help.
- **WhenToUse:** `"Use this mode for everyday Q&A, explanation, and lightweight assistance."`
- **IsDefault:** `true`
- **Status:** `"active"`
- **Version:** `"v1"`
- **AssociatedToolIds:** includes at least `"agent_change_mode"`, `"agent_list_modes"`, and `"agent_workflow_registry"`.

### 8.2 ddr_authoring

- **Id:** `"A9E1F9C15A0C4F8D9AF51F3E8B2A6D22"`
- **Key:** `"ddr_authoring"`
- **DisplayName:** `"DDR Authoring"`
- **Description:** Structured creation, refinement, and validation of Aptix DDR specifications following SYS-001.
- **WhenToUse:** `"Use this mode when the user wants to create, refine, or validate Aptix DDR specifications following SYS-001."`
- **IsDefault:** `false`
- **Status:** `"active"`
- **Version:** `"v1"`
- **AssociatedToolIds:** includes at least `"agent_change_mode"`, `"agent_list_modes"`, `"agent_workflow_registry"`, and `"agent_ddr_manager"`.

### 8.3 workflow_authoring

- **Id:** `"0FB81E6A8337444BA00A0CE28E3A1F78"`
- **Key:** `"workflow_authoring"`
- **DisplayName:** `"Workflow Authoring"`
- **Description:** Creation, refinement, and validation of Aptix agent workflows using the Workflow Registry Tool (TUL-006).
- **WhenToUse:** `"Use this mode when defining, editing, or validating Aptix workflows using TUL-006."`
- **IsDefault:** `false`
- **Status:** `"active"`
- **Version:** `"v1"`
- **AssociatedToolIds:** includes at least `"agent_change_mode"`, `"agent_list_modes"`, `"agent_workflow_registry"`, and the workflow authoring tool.

Implementations MAY add additional modes in the future as long as all invariants in Section 7 are maintained and new modes are introduced via DDR governance.

---

## 9. Deferred Mode Recognition Mechanics (Informative / Future DDR)

AGN-014 defines the structure and usage of recognition metadata fields `StrongSignals`, `WeakSignals`, and `ExampleUtterances`, but it does not define the algorithm or heuristics for selecting a mode based on those signals.

Mode recognition is intentionally deferred to a future DDR (TUL-010). The reasons are:

1. Early system simplicity: the agent currently has a small number of modes, and switching between them is strongly user-directed.
2. Safety through confirmation: TUL-007 requires explicit user confirmation before a mode change takes effect, reducing the risk of incorrect recognition.
3. Architectural flexibility: mode selection behavior may evolve as more modes and workflows are added.

Until TUL-010 is defined, the following interim rules apply:

- The LLM may reference `StrongSignals`, `WeakSignals`, and `ExampleUtterances` to suggest a mode change.
- The LLM MUST never automatically switch modes.
- All mode changes MUST use the `agent_change_mode` tool and MUST follow the confirmation and branching rules defined in TUL-007.
- Implementations MUST NOT treat signal arrays as classifiers; they are semantic hints only.
- Session mode state may only change via an explicit `agent_change_mode` tool invocation.

When the number of modes increases or fine-grained recognition becomes important, a dedicated DDR (TUL-010) will define:

- Recognition thresholds and confidence scoring.
- Semantic similarity rules or embeddings.
- Text-pattern heuristics.
- How the LLM determines when to consider a mode change.

For now, this DDR treats all recognition metadata as advisory only.

---

## 10. Implementation Notes (Informative)

The initial implementation of the Agent Mode Catalog for V1 is expected to:

- Use an in-memory, hard-coded list of `AgentMode` instances matching the seed modes in Section 8.
- Run validation at startup and fail fast on any inconsistencies.
- Provide `GetMode` and `GetToolsForMode` implementations that throw clear exceptions on unknown mode keys.
- Use `AgentMode.CreateSummary()` to implement `GetAllModesAsync`.
- Be the single source of mode metadata for all higher-level services, tools, and prompt builders.

Future DDRs may allow the catalog to be populated from configuration, databases, or RAG-indexed sources, but the contract defined in AGN-014 (especially identifier formats, invariants, and prompt behavior) MUST remain stable unless explicitly revised by governance.
