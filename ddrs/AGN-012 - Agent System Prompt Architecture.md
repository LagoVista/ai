# AGN-012 — Agent System Prompt Architecture

## 50K-Foot Summary

AGN-012 defines the **system prompt architecture** used by Aptix agents. It establishes a stable, multi-layered structure that every LLM call must follow, ensuring consistency, safety, interpretability, and extensibility across all modes and agent behaviors.

As the agent matures, its capabilities depend not only on tools and RAG context, but on a well-structured set of instructions that:
- Bind the model to the correct mode
- Convey the agent's identity and constraints
- Provide domain-specific reasoning guidance
- Integrate relevant RAG context and metadata
- Declare behavioral rules, safety constraints, and tool usage expectations

AGN-012 becomes the **root architecture** through which every future behavior is expressed.

### 1. System Prompt Layering Model
This DDR introduces the concept of a **layered prompt stack**, where each layer contributes a different type of instruction:
- **Global Aptix System Layer** — universal rules, safety constraints, tool-calling behavior
- **Agent Context Layer** — persona, specialty, constraints from AgentContext
- **Mode Context Layer** — instructions specific to the current mode (from TUL-010)
- **Session Context Layer** — state reminders, prior commitments, continuation rules
- **RAG Context Layer** — retrieved snippets, constraints on usage, grounding rules
- **Task-Specific Layer** — optional, ephemeral instructions for a given turn or objective

The combined prompt must remain deterministic, composable, and explainable.

### 2. Prompt Composition Rules
This DDR will define:
- The ordering rules for layers
- Merge rules (when layers override or augment one another)
- Constraints around prompt length and summarization
- How instructions must be narrowed or expanded depending on mode

### 3. Mode Integration
The system prompt must:
- Embed the **current mode**, its meaning, and its constraints
- Provide mode-specific behavioral rules
- Indicate when the LLM should request a mode change
- Ensure consistent mapping to the toolbelt (per TUL-008)

### 4. RAG Integration Contract
AGN-012 establishes how RAG content is injected:
- Where the RAG block appears in the prompt
- How grounding must be used
- What the model should do when RAG is missing, incomplete, contradictory, or irrelevant

### 5. Behavior Directives
The prompt architecture governs:
- How the agent reasons
- When it should reflect, validate, or double-check its outputs
- When it must ask for clarification
- When tool usage is required, optional, or disallowed

This DDR codifies these guidelines as part of the prompt layer definitions.

### 6. Safety, Transparency, and Stability
AGN-012 formalizes:
- How the agent communicates limitations
- How to avoid hallucinations
- Required self-checks or disclaimers in certain modes
- Rules for escalation when the model is uncertain

### 7. Extensibility Framework
This DDR defines how future layers and instructions can be added without breaking existing agents:
- Versioning of prompt layers
- How modes may add or override instructions
- How new tools may introduce new prompt fragments

### 8. Out of Scope
AGN-012 does **not** define:
- The mode catalog (TUL-010)
- Tool filtering or tool schema discovery (TUL-008)
- Mode persistence or change tools (TUL-009)
- Planning, memory layers, or agent policy governance (future AGN series DDRs)

Instead, it defines the stable **instructional spine** that powers the entire agent execution pipeline.
