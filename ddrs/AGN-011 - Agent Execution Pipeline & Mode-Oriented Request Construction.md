# AGN-011 — Agent Execution Pipeline & Mode-Oriented Request Construction

**ID:** AGN-011
**Title:** Agent Execution Pipeline & Mode-Oriented Request Construction
**Status:** Approved

## Approval Metadata
- **Approved By:** Kevin Wolf
- **Approval Timestamp:** 2025-12-06 18:45:00 EST (UTC-05:00)

---

# 1. Purpose

AGN-011 defines how **Mode** is persisted and flows through the Aptix Agent execution pipeline, from request construction to LLM calls to tool execution and back to the client. It focuses on three execution concerns:

1. How Mode is persisted long-term and normalized on every turn.
2. How Mode drives tool availability and request construction.
3. How Mode changes (via TUL-007) affect request, response, and session state.

This DDR is strictly about **execution orchestration**—prompt shaping, mode semantics, mode catalogs, and tool filtering rules are defined in separate DDRs.

---

# 2. 50K-Foot Summary

AGN-011 governs the deterministic use of Mode inside the Aptix Agent execution pipeline. It specifies:

- How new sessions receive a default mode.
- How Mode is persisted and normalized on follow-up turns.
- How Mode drives server tool selection and merging with client tool definitions.
- How the pipeline responds when the Mode Change Tool (TUL-007) is invoked.
- How Mode propagates from server → response → next request → server.

Everything else about Mode semantics, catalogs, or roles is out of scope.

---

# 3. Mode Persistence & Normalization

## 3.1 Default Mode for New Sessions

- All new sessions must start with Mode **"general"**, unless a non-empty Mode is supplied by the caller.
- If `AgentExecuteRequest.Mode` is empty on a new session, it must be set to "general" before session creation.
- The initial Mode is persisted on the `AgentSession`.

## 3.2 Long-Term Mode Storage

- `AgentSession` is the authoritative store for the current Mode.
- `AgentSessionTurn` may record a snapshot of Mode for that turn.
- `AgentSession` also maintains a **ModeHistory** entry for every mode change:
  - previous mode
  - new mode
  - timestamp
  - reason
  - user/org identifiers (if desired)

Mode changes are persisted only through `IAgentSessionManager.SetSessionModeAsync`.

## 3.3 Request-Time Mode Normalization

### New Sessions
- If `ConversationId` is empty:
  - If `request.Mode` is empty → set to "general".
  - Persist this Mode into the new session.

### Follow-Up Turns
- The orchestrator must load the session and **overwrite** any client-supplied `request.Mode` with the persisted `AgentSession.Mode`.
- If the client provided a mismatched mode, log a warning but continue with the persisted mode.

### Guarantee
- After normalization, `request.Mode` is always non-empty and correct for the turn.

## 3.4 Response Mode Normalization

- Without a mode change, `response.Mode = request.Mode`.
- If a mode change occurs during the turn, the final response must reflect the **updated** mode.

Clients must treat `response.Mode` as authoritative and send it back on the next turn (even though the server will normalize it).

---

# 4. Mode-Driven Request & Tool Population

## 4.1 Role of Mode in Request Construction

Once normalized, `request.Mode` drives:
- Server tool availability
- Mode-specific prompt layers (defined elsewhere)
- Final tool list sent to the LLM

## 4.2 Mode-Driven Server Tool Retrieval

`AgentRequestHandler.MergeServerTools` must:
- Use `request.Mode` to retrieve server tools from the Mode Catalog
- Always include the `agent_change_mode` tool, regardless of mode

## 4.3 Merging Client + Server Tools

Steps:
1. Parse `request.ToolsJson` for client tools.
2. Append all server tool schemas.
3. Store the merged array back into `request.ToolsJson`.

Rules:
- Server tools never remove client tools.
- Mode filtering never removes the mode change tool.

## 4.4 When Tools Are Selected

Tool selection happens **before** the Reasoner loop starts.

- The tool list is *not* changed mid-turn, even if Mode changes.
- New mode tools will become available **next turn**.

## 4.5 Response Mode Population

- No change → echo normalized request mode.
- Mode change → set response.Mode to the updated mode.

## 4.6 Client Expectations

Clients must:
- Use `response.Mode` for the next request
- Understand server normalization will override mismatches

## 4.7 Guarantees

After tool merging:
- `request.Mode` is definite and correct
- `request.ToolsJson` includes both client tools and mode-selected server tools
- The mode used is stable for the entire Reasoner loop

---

# 5. Mode Change Handling via TUL-007

## 5.1 Detection

The Reasoner must detect calls where:
```
toolCall.Name == "agent_change_mode"
```

If multiple mode-change calls occur:
- The **last successful** one wins
- A warning is logged

## 5.2 Executing the Mode Change Tool

The Reasoner must:
1. Execute the tool server-side
2. Parse the result JSON (mode, branch, reason)
3. Persist the new mode via `SetSessionModeAsync`

## 5.3 Updating the In-Flight Request

If the Reasoner will call the LLM again this turn:
- Update `request.Mode` immediately to the new mode
- Do **not** update the tool list mid-turn

## 5.4 Updating Final Response

`AgentExecuteResponse.Mode` must reflect the **post-change** mode.

## 5.5 Next Turn Behavior

On the next request:
- Orchestrator normalizes request.Mode from the persisted session
- Client and server remain synchronized

## 5.6 Branch Handling

- `branch=false` → continue in same session
- `branch=true` → pipeline simply surfaces the flag; new-session creation semantics are out of scope for AGN-011

## 5.7 Guarantees
- Mode changes are atomic, deterministic, and visible in the same turn
- New mode tools appear **next turn**
- ModeHistory records every transition

---

# 6. Out of Scope

AGN-011 does not define:
- Mode semantics (AGN-013)
- The Mode Catalog (TUL-010)
- Prompt shaping rules
- Tool filtering semantics (TUL-008)
- Branch/new-session logic

This DDR covers only the **execution pathway** of Mode.

---

# 7. Conclusion

AGN-011 defines a strict, predictable, and durable Mode lifecycle across the Aptix Agent execution pipeline. Mode flows cleanly:

**Session → Request → Reasoner → Tool Execution → Response → Next Request**

This allows modes to act as stable behavioral contexts for all LLM turns, uninterrupted by client drift or mid-turn inconsistency.
