# TUL-010 — Agent Mode Catalog Tool

**ID:** TUL-010  
**Title:** Agent Mode Catalog Tool  
**Status:** Approved  

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-06 15:10:00 EST (UTC-05:00)

---

## 1. Purpose

This DDR defines the Agent Mode Catalog Tool, a server-side Aptix agent tool that lets the LLM retrieve a read-only list of all configured agent modes and their high-level metadata.

The tool exists so that the LLM can:
- Discover which modes exist and what they are for.
- Explain mode options to the user (for example, before suggesting a mode change).
- Avoid guessing about mode semantics when the backend already has a canonical catalog.

The tool does not:
- Change the current mode or session state.
- Implement mode selection or mode-change workflows.
- Persist or mutate any catalog data.

Mode selection, mode-change workflows, and catalog storage details are defined in other DDRs. TUL-010 focuses strictly on the contract, behavior, and shape of the Agent Mode Catalog Tool.

---

## 2. Dependencies and Abstraction Boundary

The tool relies on an abstraction over the mode catalog. For this DDR we define a minimal conceptual interface; its full behavior and implementation will be specified in a separate catalog DDR.

The tool depends on an injected service that can return a list of mode summaries:

- The service is named `IAgentModeCatalogService`.
- It exposes a single method used by this tool:
  - `Task<IReadOnlyList<AgentModeSummary>> GetAllModesAsync(CancellationToken cancellationToken)`
- `AgentModeSummary` is a simple data transfer object with at least:
  - `Id` — string, GUID without hyphens.
  - `Key` — stable mode key string used in sessions and tools.
  - `DisplayName` — human-readable name for UI.
  - `Description` — short explanation of the mode.
  - `SystemPromptSummary` — single-paragraph summary of how prompts should treat this mode.
  - `IsDefault` — boolean indicating whether this is the default mode.
  - `HumanRoleHints` — optional array of strings describing which human roles this mode is primarily suited for. May be null or empty.
  - `ExampleUtterances` — optional array of sample user requests that clearly belong to this mode. May be null or empty.

TUL-010 does not constrain how the catalog is persisted (for example, configuration file versus code), nor how `AgentModeSummary` is enriched. It only requires that this minimal shape is available to the tool at runtime.

---

## 3. Tool Contract

### 3.1 Identity and Registration

The Agent Mode Catalog Tool is a standard `IAgentTool` implementation with the following identity:

- `Name` constant: `agent_list_modes`.
- `IsToolFullyExecutedOnServer`: `true`.
- `ToolName` constant: public constant string with the same value as `Name`.
- `ToolUsageMetadata` constant: human-readable usage guidance string.
- `GetSchema()` static method: returns an OpenAI-style function tool schema object.

The tool must satisfy all `AgentToolRegistry` reflection-based contracts:

- `public const string ToolName` exists, non-empty, and matches the OpenAI tool-name pattern.
- `public const string ToolUsageMetadata` exists and is non-empty.
- `public static object GetSchema()` exists, is public static, takes no parameters, and returns `object`.

Registration via `AgentToolRegistry.RegisterTool<AgentListModesTool>()` must succeed without exceptions when dependencies are correctly wired.

### 3.2 Inputs (Arguments Schema)

The tool accepts a single optional argument:

- `includeExamples` (boolean, optional)
  - When `true`, the tool should attempt to include example utterances for each mode (when available).
  - When `false` or omitted, example utterances may be omitted, null, or an empty array.

The arguments schema should be represented to the LLM as:

- Type: `object`.
- Properties:
  - `includeExamples`: type `boolean`, with a description explaining its behavior.
- No required properties.

### 3.3 Outputs (Result Payload Shape)

On success, the tool returns an `InvokeResult<string>` whose `Result` is a JSON string with the following top-level shape:

- An object with a single property:
  - `modes`: array of mode descriptors.

Each mode descriptor contains at least:

- `id`: string, the mode identifier (GUID without hyphens).
- `key`: string, stable mode key.
- `displayName`: string.
- `description`: string.
- `systemPromptSummary`: string (may be empty).
- `isDefault`: boolean.
- `humanRoleHints`: array of strings, or null, or an empty array.
- `exampleUtterances`: array of strings, or null, or an empty array, depending on the `includeExamples` argument and catalog data.

The exact casing (for example, camel-case) is defined in the implementation but must remain stable once published. The tool must always return syntactically valid JSON on success.

On failure, the tool returns an `InvokeResult<string>` marked unsuccessful with an appropriate `ErrorMessage` and no modes payload. It should log errors via `IAdminLogger`.

---

## 4. Runtime Semantics and Constraints

1. The tool is strictly read-only:
   - It must not change the current session mode.
   - It must not change any human role or agent persona.
   - It must not persist any changes to the catalog or other backend state.

2. The tool is safe to call:
   - There are no side effects beyond logging and normal request tracking.
   - Multiple calls with the same catalog contents must be idempotent and return functionally equivalent results.

3. The tool must handle catalog failures gracefully:
   - If `IAgentModeCatalogService.GetAllModesAsync` returns null or throws, the tool must:
     - Log the error via `IAdminLogger`.
     - Return an unsuccessful `InvokeResult<string>` with a concise error message.
   - The tool must not throw unhandled exceptions out of `ExecuteAsync`.

4. Performance expectations:
   - The mode catalog is expected to be small (for example, tens of entries).
   - A full fetch on each tool call is acceptable.
   - Any additional caching behavior is the responsibility of the catalog service, not this tool.

---

## 5. Usage Guidance for the LLM

The `ToolUsageMetadata` constant must describe when and how the LLM should use this tool. At minimum, it must convey the following guidance:

- Call this tool when:
  - The user asks what modes are available.
  - The user wants help understanding or choosing between modes.
  - You are about to propose a mode change and want to present the user with a list of options and their descriptions.

- Do not call this tool:
  - On every user message.
  - When you already know which mode you are in and do not need to present options.
  - As a substitute for the mode-change tool (TUL-007).

The tool is informational only. Mode selection and mode-change workflows are handled elsewhere.

---

## 6. Testing and Validation Expectations

At a minimum, the following tests must exist:

1. Schema and registration test:
   - Create an `AgentToolRegistry` with a mock `IAdminLogger`.
   - Call `RegisterTool<AgentListModesTool>()`.
   - Assert that no exception is thrown.
   - This confirms that:
     - `ToolName` is present and valid.
     - `ToolUsageMetadata` is present and non-empty.
     - `GetSchema()` is present and correctly shaped.

2. Happy path with catalog data:
   - Mock `IAgentModeCatalogService` to return a non-empty list of `AgentModeSummary` values.
   - Call `ExecuteAsync` with an empty or minimal arguments JSON.
   - Assert:
     - The result is successful.
     - The returned JSON parses correctly.
     - The `modes` array is present and contains at least one entry.
     - Required fields on the first entry are populated (`id`, `key`, `displayName`, `description`, `isDefault`).

3. Happy path with `includeExamples`:
   - Mock `IAgentModeCatalogService` to provide sample `ExampleUtterances` values.
   - Call `ExecuteAsync` with `{"includeExamples": true}`.
   - Assert that the parsed JSON includes `exampleUtterances` with the expected values.

4. Catalog failure path:
   - Configure `IAgentModeCatalogService.GetAllModesAsync` to throw an exception.
   - Call `ExecuteAsync`.
   - Assert:
     - The result is unsuccessful.
     - The error message is non-empty.
     - No malformed JSON is returned.

These tests mirror the patterns used for other tools (for example, the Hello World and calculator tools) and ensure that the Agent Mode Catalog Tool behaves predictably and safely.

---

## 7. Future Extensions (Non-Normative)

Future DDRs may extend this tool by:

- Adding optional filters to the arguments (such as by key prefix, status, or human-role hints).
- Including additional metadata in the mode summaries (such as status, version, or detection-weight hints).
- Introducing pagination for very large catalogs.

Any such extensions must remain backward-compatible with the shape defined here and must preserve the read-only nature of the tool.
