# TUL-008 — Mode-Aware Tool Declaration & Tool Schema Provider

## 50K-Foot Summary

This DDR defines how server-side tools declare the **modes** they support and how the backend selectively exposes tools to the LLM based on the current session mode.

TUL-008 establishes the filtering mechanism that determines *which* tools are included in `ToolsJson` on each Responses API call.

### 1. Tool Mode Declaration
- Each tool must declare a static string collection (e.g., `SupportedModes`) listing the mode names (opaque strings) in which the tool is available.
- Mode names are *not* enums; they are server-defined string identifiers (e.g., "general", "workflow-authoring", "ddr-authoring").
- The LLM does **not** define modes — it only requests changes via a separate tool.

### 2. Tool Schema Provider Responsibilities
- `_serverToolSchemaProvider` is responsible for:
  - Reflecting tool types
  - Reading `SupportedModes`
  - Calling each tool’s `GetSchema()` method
  - Returning only those tools whose `SupportedModes` contains the session’s current mode
- It must perform this filtering **every time** a new or follow-up turn is prepared.

### 3. Integration with Request Handling
- Mode-aware tool filtering occurs during request construction in `IAgentRequestHandler`, before calling the LLM.
- Tool schemas (from both client and server) are merged via `MergeServerTools()`.
- Tool sets **do not update mid-request**; mode changes apply to the *next* iteration.

### 4. Mode Names as Strings
- Modes are treated purely as strings, enabling dynamic, configuration-driven mode sets.
- Tools may support multiple modes or a mode wildcard (defined by implementation, not the LLM).

### 5. Out of Scope
This DDR does *not* define:
- How or when modes change (TUL-009)
- LLM behavioral rules for deciding mode (TUL-007)
- Session persistence structure

TUL-008 focuses exclusively on the **technical filtering mechanism** that ensures tools are correctly surfaced for the active mode.
