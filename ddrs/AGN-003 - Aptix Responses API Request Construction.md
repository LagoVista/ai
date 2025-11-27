# **AGN-003**  
**Title:** Aptix Responses API Request Construction  
**Status:** Proposed  
**Domain:** Aptix Orchestrator  
**Owner:** Kevin Wolf  
**Tags:** responses-api, orchestration, rag, prompts, tools, aptix  

---

## **Objective**

Define a consistent, repeatable pattern for how the Aptix Orchestrator constructs requests to the OpenAI `/responses` API.

This DDR specifies:

- how the **ConversationContext** (boot/system prompt + mode) is used
- how the **AgentExecuteRequest** fields are mapped into a `/responses` request
- how the **RAG context block** (per AGN-002) is incorporated
- how tools and continuation IDs are handled (first vs subsequent turns)

The goal is to have a single, well-defined "request envelope" that all Aptix agents use, regardless of higher-level workflow (DDR management, readiness checks, implementation planning, code generation, etc.).

---

## **Context**

AGN-001 defines the DDR management flow and introduces the idea of an **Aptix Reasoner** and DDR-focused tools such as `ddr_document`.

AGN-002 defines the canonical **RAG context injection pattern**, specifying how retrieved chunks are formatted into a single, labeled `[CONTEXT]` block with `=== CHUNK N ===` delimiters, metadata, and fenced code blocks.

At the implementation level, Aptix has:

- **AgentContext** – identifies which backends, credentials, and configuration to use for LLM, vector DB, and storage.
- **ConversationContext** – defines the reasoning profile for a task, including the initial system prompt ("boot prompt"), preferred model, default tools, and conceptual mode.
- **AgentExecuteRequest** – a client-facing request shape used by the VS Code extension and other front-ends:
  - `AgentContext`
  - `ConversationContext`
  - `ConversationId`
  - `ResponseContinuationId`
  - `Mode`
  - `Instruction`
  - `WorkspaceId`, `Repo`, `Language`, `RagScope`, `Tags`, `ActiveFiles`

The Orchestrator must combine these into a **concrete `/responses` request** that:

- obeys the multi-turn semantics of `previous_response_id`
- injects the correct system prompt on the first turn only
- passes tools when appropriate
- attaches the RAG context block in a predictable way
- preserves `Mode` as a `[MODE: …]` header in the user input

AGN-003 defines this construction pattern.

---

## **Design**

### **1. High-level Request Envelope**

Aptix defines a conceptual **Reasoner Request Envelope**:

- Input:
  - `ConversationContext` (boot prompt, default model, default tools)
  - `AgentExecuteRequest` (mode, instruction, workspace, RAG hints)
  - `RagContextBlock` (string per AGN-002, may be empty)
- Output:
  - A single `/responses` request object with:
    - `model`
    - `input` (system + user messages as appropriate)
    - `tools` (only when needed)
    - `tool_choice` (optional)
    - `previous_response_id` (for continuation)

The Orchestrator code is responsible for constructing this envelope consistently.

---

### **2. Mapping ConversationContext to the `/responses` request**

The **ConversationContext** provides the static per-conversation parameters:

- **System prompt (boot prompt)**
  - A long-lived instruction set for the Aptix Reasoner.
  - Only included on the **first** `/responses` call of a conversation.
- **Default model**
  - e.g., `gpt-5.1`.
- **Default tools** (optional)
  - Tools that are generally relevant for this conversation type (e.g., DDR tools for a DDR-focused context).

The mapping:

- `model` in `/responses` is taken from `ConversationContext` (with fallback to a global default).
- On the **first turn** (no `ResponseContinuationId`):
  - A system message is included:
    ```jsonc
    {
      "role": "system",
      "content": [
        { "type": "text", "text": "<boot prompt from ConversationContext>" }
      ]
    }
    ```
- On **subsequent turns** (non-empty `ResponseContinuationId`):
  - No system message is sent.
  - `previous_response_id` is set to the last response id.

---

### **3. Mapping AgentExecuteRequest to the `/responses` request**

**AgentExecuteRequest** carries the dynamic, per-call inputs:

- `ConversationId` – client-side thread identifier.
- `ResponseContinuationId` – last `/responses` `id`, if any.
- `Mode` – high-level phase name (e.g., `DDR_CREATION`, `DDR_REFINEMENT`).
- `Instruction` – primary user instruction for this turn.
- `WorkspaceId`, `Repo`, `Language`, `RagScope`, `Tags`, `ActiveFiles` – hints for RAG and logging.
- `ToolsJson` – (for v1) a serialized array of tool definitions, provided by the client.
- `ToolChoiceName` – optional instruction to force a specific tool.

The Orchestrator constructs the **user message** as follows:

1. **Mode and Instruction block**

   The first `content` item in the user message contains **mode** and **instruction** using a consistent header pattern:

   ```text
   [MODE: <Mode>]

   [INSTRUCTION]
   <Instruction>
   ```

   Examples:

   ```text
   [MODE: DDR_CREATION]

   [INSTRUCTION]
   Create a DDR that captures the DDR management workflow described below…
   ```

   ```text
   [MODE: DDR_REFINEMENT]

   [INSTRUCTION]
   Refine the current DDR for clarity and add missing Risks / Open Questions.
   ```

2. **RAG Context block**

   The second `content` item in the user message contains the RAG context block as defined by AGN-002:

   ```text
   [CONTEXT]

   === CHUNK 1 ===
   Id: ctx_1
   Path: Billing/Managers/InvoiceManager.cs
   Lines: 40-85
   Language: csharp
   ```csharp
   public class InvoiceManager
   {
       ...
   }
   ```

   === CHUNK 2 ===
   Id: ctx_2
   Path: Billing/Api/InvoiceController.cs
   Lines: 10-55
   Language: csharp
   ```csharp
   [ApiController]
   [Route("api/invoices")]
   public class InvoiceController : ControllerBase
   {
       ...
   }
   ```
   ```

   If no RAG context is available or needed, the `[CONTEXT]` block may be omitted entirely (i.e., only the instruction block is sent).

3. **User message shape**

   Combining the two blocks, the user message is:

   ```jsonc
   {
     "role": "user",
     "content": [
       {
         "type": "text",
         "text": "[MODE: " + Mode + "]\n\n[INSTRUCTION]\n" + Instruction
       },
       {
         "type": "text",
         "text": RagContextBlock // may be empty or omitted
       }
     ]
   }
   ```

---

### **4. First-turn vs continuation behavior**

The Orchestrator must distinguish between:

- **Initial turn** – no `ResponseContinuationId` in `AgentExecuteRequest`
- **Continuation turn** – `ResponseContinuationId` is set to a previous `/responses.id`

#### **4.1 Initial turn**

When `AgentExecuteRequest.ResponseContinuationId` is `null` or empty:

- Include **system message** with the `ConversationContext` boot prompt.
- Include **user message** with `[MODE]`, `[INSTRUCTION]`, and optional `[CONTEXT]`.
- Include **tools**, if provided by the client (`ToolsJson`), or default tools from `ConversationContext`.
- If `ToolChoiceName` is populated, set `tool_choice` accordingly; otherwise omit and let the model choose.

The resulting `/responses` request (conceptually) is:

```jsonc
{
  "model": "<from ConversationContext>",
  "input": [
    {
      "role": "system",
      "content": [ { "type": "text", "text": "<boot prompt>" } ]
    },
    {
      "role": "user",
      "content": [
        { "type": "text", "text": "[MODE: ...]\n\n[INSTRUCTION]\n..." },
        { "type": "text", "text": "<RagContextBlock>" }
      ]
    }
  ],
  "tools": <parsed ToolsJson or defaults>,
  "tool_choice": {
    "type": "tool",
    "name": "<ToolChoiceName>"
  }
}
```

If `ToolChoiceName` is empty, `tool_choice` is omitted or set to `"auto"`.

#### **4.2 Continuation turn**

When `AgentExecuteRequest.ResponseContinuationId` is **non-empty**:

- Do **not** include the system message.
- Do **not** resend tools unless the set of tools has changed.
- Set `previous_response_id` to `ResponseContinuationId`.
- Include the same user message structure (`[MODE]`, `[INSTRUCTION]`, `[CONTEXT]`).

The resulting `/responses` request:

```jsonc
{
  "model": "<from ConversationContext>",
  "previous_response_id": "<ResponseContinuationId>",
  "input": [
    {
      "role": "user",
      "content": [
        { "type": "text", "text": "[MODE: ...]\n\n[INSTRUCTION]\n..." },
        { "type": "text", "text": "<RagContextBlock>" }
      ]
    }
  ],
  "tool_choice": {
    "type": "tool",
    "name": "<ToolChoiceName>"
  }
}
```

If tools are unchanged from previous calls, the `tools` field is omitted; the Reasoner retains the tool definitions from the initial turn.

---

### **5. Tools and Tool Choice**

For the DDR-only MVP (per AGN-001), the VS Code extension owns the tool definitions and passes them to the server as serialized JSON (`ToolsJson`). The server:

- Accepts `ToolsJson` as an opaque JSON string.
- Parses it into a JSON array for the `/responses.tools` field on the **initial turn**.
- Optionally merges with default tools defined by the `ConversationContext`.

The **ToolChoiceName** field guides the `tool_choice` parameter:

- If `ToolChoiceName` is `null` or empty → `tool_choice` is omitted (default: `auto`).
- If `ToolChoiceName` is set (e.g., `"ddr_document"`) →

  ```jsonc
  "tool_choice": {
    "type": "tool",
    "name": "ddr_document"
  }
  ```

This allows the agent to enforce mode-specific tools, e.g.:

- `Mode = DDR_CREATION` → `ToolChoiceName = "ddr_document"`
- `Mode = DDR_REVIEW` → `ToolChoiceName = "ddr_review"`
- `Mode = DDR_SEARCH` → `ToolChoiceName = "ddr_search_result"`

---

### **6. ResponseContinuationId and Turn Tracking**

On each `/responses` call, the Orchestrator:

- Reads the returned `id` from the OpenAI response.
- Stores it as the new `ResponseContinuationId` for that conversation.
- Returns it to the client in `AgentExecuteResponse.ResponseContinuationId`.

The client (e.g., VS Code extension) sends this value back in the next `AgentExecuteRequest` when continuing the same logical conversation.

This gives Aptix:

- a stable threading model between client and server (`ConversationId`)
- a stable threading model between server and LLM (`ResponseContinuationId` / `previous_response_id`)

---

## **Risks / Open Questions**

1. **Context size growth**  
   - Repeatedly injecting large `[CONTEXT]` blocks may approach token limits.  
   - Future work may introduce context caching or diff-based context strategies.

2. **Mode transitions**  
   - This DDR assumes the `Mode` header is sufficient to steer behavior.  
   - Additional per-mode constraints may be added in future DDRs (e.g., AGN-004: Mode Contracts).

3. **Tool set evolution**  
   - For now, tools are driven primarily from the client.  
   - Over time, tool definitions may migrate to the server as canonical sources.

4. **RAG absence**  
   - When no RAG information is available, behavior should still be well-defined.  
   - This DDR allows `[CONTEXT]` to be omitted, but future work may define explicit placeholders.

---

## **Testing Strategy**

- Unit tests for the request builder that:
  - verify presence/absence of system message on first vs continuation calls
  - verify correct inclusion of `[MODE]` and `[INSTRUCTION]` in the first text block
  - verify correct insertion of the RAG context block from AGN-002
  - verify tools and `tool_choice` are mapped correctly from `ToolsJson` / `ToolChoiceName`
  - verify `previous_response_id` is set from `ResponseContinuationId`

- Integration tests using a mock LLM or sandbox model:
  - confirm multi-turn behavior works as expected (Reasoner retains tools and system prompt)
  - confirm the Reasoner can reference chunks by `Id` and `Path`
  - confirm that mode changes across turns do not require new system prompts

- Logging verification:
  - log the constructed request envelopes for early runs
  - confirm that sensitive information is handled according to security policies

---

## **Notes / Rationale**

Defining a single, canonical request-construction pattern is critical for:

- consistent behavior across all Aptix workflows (DDR, readiness, planning, codegen)
- predictable multi-turn conversation behavior with `/responses`
- easier debugging and telemetry (all requests share the same structure)
- future evolution (e.g., centralized tool management, richer mode contracts)

AGN-003 builds directly on AGN-001 (DDR Management Flow) and AGN-002 (RAG Context Injection Pattern), completing the foundational trio of DDRs that define:

1. What DDRs are and how we manage them (AGN-001)
2. How we present retrieved code/data to the LLM (AGN-002)
3. How we construct the actual `/responses` API requests for the Aptix Reasoner (AGN-003)

Together, these DDRs establish the basic infrastructure for future work on readiness checks, implementation planning, and automated code modifications.
