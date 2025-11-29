# AGN-003 — Aptix Responses API Request Construction

**Title:** Responses API Request Construction  
**Status:** Accepted  
**Version:** 1.2  
**Owner:** Aptix Orchestrator / Reasoner  
**Namespace:** LagoVista.AI.Helpers  

---

## 1. Purpose

This DDR defines how Aptix constructs the request payload for the OpenAI `/v1/responses` API.

It standardizes:
- how system and user messages are composed
- how tools and tool choices are passed
- how RAG context, tool results, and per-request system prompts are injected

The canonical implementation is `LagoVista.AI.Helpers.ResponsesRequestBuilder`.

---

## 2. Inputs

The request builder consumes:

- `ConversationContext` (LagoVista.AI.Models)
  - `ModelName` (string) — target LLM model id
  - `System` (string) — base boot/system prompt for this conversation
  - `Temperature` (float) — model sampling temperature

- `AgentExecuteRequest` (LagoVista.Core.AI.Models)
  - `Mode` (string) — high-level agent mode, e.g. `QA`, `CODE_EDIT`, `DDR_CREATION`
  - `Instruction` (string) — primary user instruction
  - `ResponseContinuationId` (string?) — previous response id for `/responses` continuation
  - `ToolsJson` (string?) — JSON array of OpenAI tool/function schemas
  - `ToolChoiceName` (string?) — explicit tool choice to force
  - `ToolResultsJson` (string?) — JSON array of previous tool results for continuation turns
  - `SystemPrompt` (string?) — **optional per-request boot instructions provided by the agent client**

- Additional builder parameters:
  - `ragContextBlock` (string?) — pre-formatted `[CONTEXT]` block per AGN-002
  - `toolUsageMetadataBlock` (string?) — large, delimited block with usage guidance for all tools (from `IServerToolUsageMetadataProvider`)
  - `stream` (bool?) — whether responses should be streamed via SSE

---

## 3. High-level behavior

The builder produces a `ResponsesApiRequest` with the following top-level fields:

- `model` — taken from `ConversationContext.ModelName`
- `temperature` — from `ConversationContext.Temperature`
- `stream` — from input parameter
- `previous_response_id` — populated on continuation turns only
- `input` — array of messages (`system`, `user`)
- `tools` — OpenAI tool/function schemas (initial turns only)
- `tool_choice` — explicit tool choice, if provided

Two cases are distinguished:

1. **Initial request** — `AgentExecuteRequest.ResponseContinuationId` is null/empty
2. **Continuation request** — `ResponseContinuationId` is non-empty

---

## 4. System message construction (initial requests)

For initial requests, the builder constructs a single `system` message:

```jsonc
{
  "role": "system",
  "content": [
    { "type": "text", "text": ConversationContext.System },
    { "type": "text", "text": AgentExecuteRequest.SystemPrompt }, // optional
    { "type": "text", "text": toolUsageMetadataBlock }             // optional
  ]
}
```

### 4.1 Base system prompt

`ConversationContext.System` is the **canonical Aptix boot prompt**, owned by the orchestrator.
It defines:
- overall agent persona and responsibilities
- generic RAG and tool-usage rules
- safety and compliance instructions

This value MUST always be present (may be an empty string but SHOULD NOT be).

### 4.2 Per-request SystemPrompt

`AgentExecuteRequest.SystemPrompt` allows a caller (e.g., a specific agent client) to provide
additional, **scoped boot instructions** for this particular request.

Examples:
- narrowing the domain ("Focus only on billing models for this session")
- style or tone preferences ("Answer in terse bullet points")
- temporary constraints ("Do not edit files, only summarize")

Rules:
- If `SystemPrompt` is null/empty/whitespace, it is **omitted** from the `system.content` array.
- If present, it is appended as a second `text` item after `ConversationContext.System`.
- `SystemPrompt` is only included on **initial** `/responses` calls; continuation requests omit the entire system message and rely on `previous_response_id`.
- `SystemPrompt` MUST NOT contradict core safety instructions contained in `ConversationContext.System`.

### 4.3 Tool usage metadata block

`toolUsageMetadataBlock` is a single large text block containing delimited usage guidance
for all registered server tools (see AGN-005 and the ToolUsageMetadata contract).

Rules:
- If null/empty, it is omitted.
- If present, it is appended as a third `text` item in the system message.
- The block typically uses delimiters such as:

  ```text
  <<<APTIX_SERVER_TOOL_USAGE_METADATA_BEGIN>>>
  <<<APTIX_TOOL_USAGE_BEGIN name='tool_name'>>>
  ...
  <<<APTIX_TOOL_USAGE_END name='tool_name'>>>
  <<<APTIX_SERVER_TOOL_USAGE_METADATA_END>>>
  ```

This ensures the LLM sees consistent, high-priority guidance about how tools should be used.

### 4.4 Continuation requests

For continuation requests (`ResponseContinuationId` not null/empty):
- **No system message is sent**.
- `dto.PreviousResponseId` is set to `ResponseContinuationId`.
- The underlying LLM resumes from the prior response state.

---

## 5. User message construction

The builder always constructs a single `user` message per call:

```text
[MODE: <Mode>]

[INSTRUCTION]
<Instruction>
```

This is encoded as:

```jsonc
{
  "role": "user",
  "content": [
    { "type": "text", "text": "[MODE: ...]\n\n[INSTRUCTION]\n..." },
    { "type": "text", "text": ragContextBlock },     // optional
    { "type": "text", "text": toolResultsBlock }     // optional, continuation only
  ]
}
```

### 5.1 Mode + Instruction

The first `content` item always contains:

- `[MODE: <AgentExecuteRequest.Mode>]` header
- `[INSTRUCTION]` header
- The raw `AgentExecuteRequest.Instruction` string

This gives the model a structured anchor for routing and behavior.

### 5.2 RAG context block

If `ragContextBlock` is non-empty, it is appended as a second `content` item in the same `user` message.
The block is expected to be pre-formatted according to AGN-002 (e.g., `[CONTEXT]` header and chunk list).

### 5.3 Tool results block (continuations)

If `ToolResultsJson` is non-empty, the builder converts it to a human-readable
`[TOOL_RESULTS]` block via `ToolResultsTextBuilder.BuildFromToolResultsJson` and appends
it as another `content` item of type `text`.

This allows the model to reconcile previous tool executions when continuing a turn.

---

## 6. Tools and tool choice

### 6.1 ToolsJson

If `AgentExecuteRequest.ToolsJson` is non-empty and this is an **initial** request:

- `ToolsJson` is parsed as a JSON array.
- Each element that is a JSON object is added to `ResponsesApiRequest.Tools`.
- If parsing fails, tools are silently omitted (caller/orchestrator should log upstream).

On continuation requests, `ToolsJson` is ignored and no tools are sent.

### 6.2 ToolChoiceName

If `ToolChoiceName` is non-empty, the builder sets:

```jsonc
"tool_choice": {
  "type": "tool",
  "name": "<ToolChoiceName>"
}
```

This forces the model to call a specific tool.

---

## 7. Streaming behavior

The optional `stream` parameter is passed through to `ResponsesApiRequest.Stream`.

- If null, the default behavior of the underlying client is used.
- If true, the caller is expected to handle Server-Sent Events (SSE).

---

## 8. Acceptance criteria

The builder is compliant with AGN-003 if:

- It sets `model`, `temperature`, and `stream` consistently from `ConversationContext` and input.
- It correctly distinguishes initial vs continuation requests using `ResponseContinuationId`.
- It includes exactly one `system` message on initial requests containing:
  - `ConversationContext.System` as the first content item.
  - `AgentExecuteRequest.SystemPrompt` as the second content item, when provided.
  - `toolUsageMetadataBlock` as the third content item, when provided.
- It omits the system message entirely on continuation requests and sets `previous_response_id`.
- It always sends a single `user` message with `[MODE]` and `[INSTRUCTION]` plus optional RAG context and tool results blocks.
- It populates `tools` only on initial requests and only when `ToolsJson` is a valid JSON array.
- It sets `tool_choice` only when `ToolChoiceName` is supplied.

The canonical implementation is `ResponsesRequestBuilder.Build`, and unit tests under `LagoVista.AI.Tests` MUST cover:
- initial vs continuation behavior
- RAG context inclusion
- tool schemas and tool choice
- tool results block behavior
- the interaction of `System`, `SystemPrompt`, and `toolUsageMetadataBlock`.

---

**End of AGN-003**
