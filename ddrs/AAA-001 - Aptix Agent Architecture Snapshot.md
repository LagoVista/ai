# AAA-001 — Aptix Agent Architecture Snapshot

**ID:** AAA-001
**Title:** Aptix Agent Architecture Snapshot
**Status:** Draft
**DdrType:** Referential
**Approved By:** Kevin D. Wolf
**Approval Timestamp:** 2025-12-21T16:21:00.000Z
---

## 1. Purpose

This DDR captures a point-in-time architectural snapshot of the Aptix Agent ecosystem, including agents, tools, modes, sessions, RAG integration, and orchestration. It is intended as a reference map for future DDRs rather than a normative behavior specification.

## 2. Scope

This document describes the runtime and structural architecture of the Aptix Agent stack as of December 2025. It summarizes concepts that are defined more formally in other DDRs (for example SYS-001, SYS-004, TUL-007, TUL-011, AGN-013). It does not replace those DDRs and does not introduce new behavior on its own.

---

## 3. High-level architecture

At a high level the Aptix Agent system consists of:

1. The LLM interaction layer (OpenAI responses API, schema, tool calls).
2. The AgentReasoner loop that coordinates LLM calls and server tools.
3. The session layer that persists AgentSession and AgentSessionTurn records.
4. The tooling layer (IAgentTool implementations and AgentToolRegistry).
5. The mode and prompt layer (mode catalog, system prompts, RAG scopes).
6. The client integration layer (VS Code extension and other hosts).

Data and control flow from the user, through the client, into the agent backend, then out to tools and back to the LLM.

---

## 4. Key components

- AgentRequestHandler: builds AgentExecuteRequest, injects tools JSON and mode, and calls the reasoner.
- AgentReasoner: runs the multi-iteration loop of LLM → tools → LLM until there are no tools left or a client tool must be executed.
- AgentSession and AgentSessionTurn: record context, mode, conversation history, and metrics for each turn.
- IAgentSessionManager: creates and updates sessions and turns, and provides methods such as SetSessionMode used by tools like the mode change tool defined in TUL-007.
- AgentToolRegistry and IAgentTool: define and validate the server-side tool ecosystem, including name, usage metadata, and schema.

---

## 5. DDR landscape (summary only)

This section only lists representative DDRs that shape the architecture; the full details live in those DDRs.

- SYS-001: development workflow and DDR lifecycle.
- SYS-004: Aptix file and patch contract for file editing bundles.
- SYS-005: InvokeResult conventions for consistent success and error reporting.
- TUL-005: DDR management tools and related operations.
- TUL-007: agent session mode change tool.
- TUL-011: server-side tool implementation contract.
- AGN-013: mode data structure and mode catalog concepts (in progress at the time of this snapshot).

---

## 6. Mode system overview

Modes are static design-time objects that describe how the LLM should behave in a given context. Each mode has at least:

- An internal id and a human-readable key.
- A short description of when the mode should be used.
- One or more system prompt fragments that are attached to each call while the session is in that mode.
- Information about which tools are most relevant in that mode, for example via tool group hints or allowed tool lists.
- Optional RAG scopes or collections that should be preferred when building the RAG context block.

The current session stores the active mode key as a simple string. Per-session state such as the current DDR id or workflow id is deliberately kept out of the mode itself and lives in the session or related models instead.

---

## 7. Session and reasoner interaction

The normal flow for a turn is:

1. The client sends a user message and metadata to the backend.
2. AgentRequestHandler loads the AgentSession and current mode and prepares an AgentExecuteRequest.
3. Server-side tools are merged with any client tools into ToolsJson.
4. AgentReasoner calls the LLM and inspects any tool calls in the response.
5. Server tools are executed via IAgentToolExecutor with an AgentToolExecutionContext.
6. If only server tools were used, their results are fed back into the LLM for another iteration.
7. If any client tools are required, the reasoner returns control to the client along with the tool calls so the host can execute them.

The mode is read from the session at the start of the turn. If the mode is changed during the turn by the mode change tool, that change is persisted through IAgentSessionManager and visible to subsequent turns.

---

## 8. Tooling layer

The tooling layer is defined primarily by TUL-011 and the AgentToolRegistry implementation.

Key points:

- Every tool implements IAgentTool, has a stable Name, and is registered with AgentToolRegistry.
- Every tool declares ToolName, ToolUsageMetadata, and GetSchema as static members so the registry can validate the type at startup.
- Some tools are fully executed on the server (IsToolFullyExecutedOnServer is true), while others represent client tools that the host environment must execute.
- The mode change tool defined in TUL-007 is always available regardless of the current mode and is responsible for persisting mode changes that the user has confirmed.

---

## 9. RAG and prompts

RAG context is prepared before each LLM call and injected as a single block of text or structured content. The specific scope and collections used may depend on the active mode, but this DDR treats that as an implementation detail.

System prompts are composed from:

- Global Aptix system guidance.
- Mode-specific system prompt segments.
- Optional developer or host prompts for the current integration.
- Optional short reason strings explaining why the session is in the current mode, for example values captured by the mode change tool.

The combined prompt set constrains tool usage, expected style, and safety behavior for the agent.

---

## 10. Future work

The following topics are expected to have dedicated DDRs:

- A full mode catalog (TUL-010) that enumerates all modes, their keys, and their detection hints.
- Detailed mode data structures and system prompt patterns (AGN-013).
- A more advanced mode transition and branching framework (AGN-011).
- Higher-level agent evolution topics such as multi-agent orchestration and workflow planning (AGN-012 and related DDRs).

AAA-001 itself is primarily descriptive; it should be periodically updated when major foundational DDRs change so that it continues to represent an accurate map of the running system.
