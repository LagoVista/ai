# DDR — TUL-012: Mode Change Tool Trigger
**DDR ID:** TUL-012  
**Title:** Mode Change Tool Trigger  
**Status:** Approved  
**Approved by:** Kevin D. Wolf  
**Approved at:** 2025-12-18 00:00 ET

## Summary
This DDR talks about how when a mode is changed tools can be ran after the mode as been entered.

## 1) Context & Problem Statement
Aptix supports multiple agent “modes” (e.g., `general`, `ddr`, `code`) that tailor behavior, instructions, and available tools to a specific workflow.

For some modes, switching instructions alone is not sufficient. On entering a mode, we want an automatic **mode-entry bootstrap** step that runs one or more tools to load or assemble additional context needed for that mode. Examples:
- Entering **DDR mode** should automatically load the DDR process/workflow context so the assistant immediately follows the correct gates and conventions.
- Entering **code mode** may automatically request/upload relevant files from the client so the assistant can operate with the correct repository context.

This DDR defines a standard mechanism for **post-mode-entry tool execution** whose purpose is **context hydration**. Tool results may be handled in either of two ways (depending on the tool):
- **Injected into the LLM context/conversation** (potentially summarized or formatted), and/or
- **Stored as internal mode/session state** for later use without necessarily being directly injected into the chat.

### Problem being solved
- There is currently no single, explicit specification for how mode entry triggers these context-hydration tool runs, including how they are selected, sequenced, and associated with the newly entered mode.
- Without a defined mechanism, each mode risks ad-hoc bootstrapping, leading to inconsistent behavior and difficulty extending the system with new modes that require initialization context.

## 2) Goal & Non-Goals
### Goal
Define a clear, testable specification for **mode-entry tool triggering** used to **hydrate context** for the newly entered mode, including:
- When mode-entry triggers are evaluated (only after the mode is entered).
- Which tools may be run as part of mode entry (mode-defined bootstrap tools; multiple tools supported).
- How tool outputs are handled (inject/store/both).
- How to ensure tool execution is associated with the **new mode**.
- How to make the behavior predictable and debuggable (traceability).

Success looks like:
- A consistent mechanism any mode can use to declare “on-enter bootstrap tools”.
- Deterministic behavior validated by automated tests.
- Entering a mode immediately provides the mode’s expected context.

### Non-Goals
This DDR does not attempt to:
- Define the full content of any specific mode’s bootstrap context, only the mechanism for loading it.
- Redesign mode taxonomy or unrelated tool execution flows.

## 3) Definitions & Concepts
- **Mode**: Named configuration determining agent policy, instructions, and available tools.
- **Mode change**: Transition from current mode to target mode.
- **Mode entered (mode entry boundary)**: Point at which target mode is active for enforcement/execution.
- **Bootstrap / context hydration**: Loading/preparing context needed for the entered mode.
- **Mode-entry bootstrap tools**: Mode-defined ordered list of tools executed after mode entry to hydrate context.
- **Tool execution context**: Effective environment under which a tool runs (active mode, policies, tool registry).
- **Context injection**: Adding tool output into LLM working context/conversation (possibly summarized/structured).
- **Internal state storage**: Persisting tool output as mode/session state.
- **Bootstrap plan**: Resolved ordered tool list + per-tool output handling for a mode entry attempt.
- **Correlation / mode-change id**: Unique identifier linking a mode entry attempt to tool runs and notifications.
- **Ordering guarantee**: Defined sequencing rules for multiple bootstrap tools.

## 4) Proposed Behavior / Specification (High-Level)
Mode-entry bootstrap tools run **after** the mode is entered and are used to hydrate context for that mode.

Lifecycle (conceptual):
1. Mode change requested (`mode_old` → `mode_new`)
2. Enter `mode_new` (instructions/policy/tool availability reflect `mode_new`)
3. Record “mode entered” boundary and create/attach correlation id
4. Resolve bootstrap plan for `mode_new` (ordered tools + inject/store/both)
5. Execute bootstrap tools in order under `mode_new` execution context
6. Apply outputs (inject/store/both as configured)
7. Mark mode “ready” only if bootstrap completes successfully

## 5) Triggering Rules & Ordering Guarantees
- Bootstrap begins after the target mode becomes active and enters a bootstrap phase.
- Bootstrap is required for mode readiness.
- Bootstrap plan is mode-defined and ordered; tools are required (no silent skipping).
- Deterministic declared order; serial execution.
- Tools run under target-mode context.
- Output handling is deterministic (recommended store then inject when both).
- Fail-fast: stop on first failure; do not continue.
- Stay in new mode but mark **not ready**.
- Surface system error to user; user can try changing mode again to rerun tools.
- Correlation id ties together request, tool runs, and notification.

## 6) Error Handling & Safety Constraints
- Failures are system problems (not user/data problems).
- On failure: stop, mark not ready, surface error, preserve correlation id.
- No explicit “retry” action; retry occurs by changing mode again (new attempt, new correlation id).
- **Bootstrap cannot change modes**:
  - `mode_change_tool` is disallowed during bootstrap.
  - Any attempt to change modes during bootstrap is blocked and treated as bootstrap failure.
- Not-ready behavior: allow non-bootstrap tools but warn results may be degraded; workflows depending on hydration should be treated as unsafe until bootstrap succeeds.

## 7) Observability & Auditability
- Correlation id required and propagated across mode change, bootstrap, per-tool records, readiness state, and user notification.
- Record minimum events: mode change requested, mode entered, bootstrap started, per-tool start/end/outcome, bootstrap completed, readiness state, user notification.
- Avoid logging raw injected/stored content; record metadata/hashes/redacted summaries as appropriate.
- Record blocked attempts to call `mode_change_tool` during bootstrap as causal failures.

## 8) Testing Strategy (High-Level)
- Bootstrap is kicked off by **AgentReasoner** upon detecting mode change.
- Introduce a dedicated **Mode Entry Bootstrap Service** invoked by AgentReasoner.
- Service exposes one entry point: **`execute(params) -> InvokeResult<DETAILS>`**; tests focus on this method.

Test categories:
- Success path, ordering, target-mode context
- Fail-fast behavior, not-ready state, user notification
- Prohibition of mode changes during bootstrap (`mode_change_tool` blocked)
- Correlation id propagation and audit event emission
- Output handling modes (inject/store/both) at the contract boundary
