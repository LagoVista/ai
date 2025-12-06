# TUL-010 — Agent Mode Catalog & Mode Identification Rules

## 50K-Foot Summary

TUL-010 defines the authoritative catalog of **all modes** recognized by the Aptix agent system and the rules by which the LLM identifies or classifies user intent into those modes.

Where TUL-007 defines the *meaning* and *semantics* of modes, TUL-010 is the registry of available modes and the heuristics that help both the LLM and backend determine mode relevance.

### 1. Mode Catalog
This DDR will describe each mode with:
- Canonical string identifier (e.g., "general", "workflow-authoring", "ddr-authoring", "rag-indexing")
- Human-readable purpose
- Expected behaviors and constraints
- Typical tool availability
- Examples of tasks appropriate for the mode

### 2. Mode Identification Rules
The LLM must classify user requests into modes consistently. TUL-010 will define:
- "Strong signals" for each mode (phrases, intents, tasks)
- "Weak/ambiguous signals" and fallback behavior
- When the agent should remain in the current mode
- When it should propose a mode change

### 3. Disambiguation & Neutral Requests
This DDR will specify:
- Handling of mixed-domain requests
- Handling of commands that do not imply any particular mode
- When to ask the user for clarification

### 4. Mapping Modes to Tools (Informational)
While TUL-008 contains the authoritative rules for filtering tools, TUL-010 will list **informational associations**, including:
- Typical tools used in each mode
- Example tool workflows per mode

### 5. Extensibility Rules
This DDR defines how new modes may be added in a controlled, predictable way, including:
- Naming conventions
- Backwards compatibility expectations
- Update requirements for TUL-007 and AGN-011

### 6. Out of Scope
TUL-010 does **not** define:
- How modes are persisted (TUL-009)
- How requests are built (AGN-011)
- How tools declare supported modes (TUL-008)

Instead, it defines the **semantic universe** of modes and the heuristics the system and LLM use to assign user intent.
