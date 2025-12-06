# AGN-011 — Agent Execution Pipeline & Mode-Oriented Request Construction

## 50K-Foot Summary

AGN-011 defines how the Aptix backend assembles each LLM call in a mode-aware, deterministic manner. It establishes the rules for:

- Constructing system prompts
- Injecting the current mode
- Injecting the appropriate RAG scope and context blocks
- Declaring what the LLM must output
- Understanding when and how the LLM may request a mode change
- Ensuring consistency across new turns and follow-up tool-result turns

Where TUL-007 defines *what modes mean*, AGN-011 defines *how every LLM call is orchestrated using mode and session state*.

### 1. Request Construction Contract
AGN-011 formalizes how the backend builds an `AgentExecuteRequest`, including:
- Current session mode
- System prompts (core + mode-specific)
- RAG scope injection
- Client vs server tools
- ToolResults for follow-up calls

### 2. System Prompt Layering
This DDR will specify a clear separation of:
- Global Aptix system prompt (always included)
- Agent-specific prompt (from AgentContext)
- **Mode-specific instructions** (from TUL-010's catalog)
- Conversation/turn metadata

### 3. RAG Scope Contract
Defines how and when the backend attaches RAG context blocks, including:
- Session-level scope
- Mode-level filtering
- Turn-level augmentation

### 4. Mode-Driven Behavioral Directives
Mode (e.g., "general", "ddr-authoring", "workflow-authoring") determines:
- How the agent interprets user intent
- How the model should behave
- Whether the model should propose or avoid tool usage

### 5. Mode Change Workflow (High-Level)
This DDR does **not** define persistence or the ChangeMode tool (covered in TUL-009). Instead, it specifies:
- How requests should reflect the current mode
- How responses should indicate a proposed mode change
- How the pipeline proceeds after tool execution

### 6. Out of Scope
AGN-011 does *not* define:
- The mode catalog (TUL-010)
- Tool filtering rules (TUL-008)
- Mode persistence (TUL-009)

It focuses exclusively on **LLM-call orchestration**, ensuring all agent executions use a stable, predictable, and mode-driven contract.
