# TUL-009 — Session Mode Persistence & ChangeMode Tool

## 50K-Foot Summary

This DDR defines how the backend stores a session’s **current mode**, how the system updates this value, and how the agent exposes a server-side tool (`agent_change_mode`) that the LLM must call to request a mode transition.

TUL-009 governs the *authoritative server-side lifecycle* of session modes.

### 1. Session-Level Mode Storage
- `AgentSession` gains a `string Mode` property.
- Mode represents the current behavioral context of the session.
- Mode values are opaque strings (e.g., "general", "workflow-authoring").
- If null/empty, the system defaults to a server-defined mode (likely "general").

### 2. Session Manager Support
`IAgentSessionManager` must expose:

```csharp
Task SetAgentSessionModeAsync(string sessionId, string mode, EntityHeader org, EntityHeader user);
```

- This method is the **only authoritative write-path** for updating mode.
- Validation and normalization of mode names happens here.

### 3. ChangeMode Tool (`agent_change_mode`)
- A server-side tool that:
  - Accepts `targetMode` and optional `reason`
  - Validates the requested mode
  - Updates the session via `SetAgentSessionModeAsync`
  - Returns `{ previousMode, newMode }` for narration and audit
- This tool is the **exclusive** mechanism for LLM-driven mode changes.
- Tools are not immediately reloaded; changes apply to the *next* request.

### 4. Interaction with the Reasoner
- Mode-change tools run inside the standard `AgentReasoner` tool-execution loop.
- The reasoner does **not** have special logic for mode changes.
- The mode update takes effect only when the next turn is constructed.

### 5. Response Mode Echoing
- `AgentExecuteResponse` includes a `Mode` field:
  - Typically echoes `request.Mode`
  - MAY reflect the new mode after tool execution
  - DOES NOT automatically update the session — only the tool does

### 6. Logging & Auditing
- All mode changes must be logged with:
  - Session ID
  - Previous mode
  - New mode
  - Reason (if provided)
  - Org/user context

### 7. Out of Scope
This DDR does *not* define:
- Tool filtering rules (TUL-008)
- LLM rules for deciding when to change modes (TUL-007)
- Tool schema shapes or reflection models

TUL-009 defines **persistence, execution, and authority** for session modes — the server’s canonical source of truth.
