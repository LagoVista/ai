# **AGN-004**  
**Title:** Aptix Responses API Result Mapping  
**Status:** Proposed  
**Domain:** Aptix Orchestrator  
**Owner:** Kevin Wolf  
**Tags:** responses-api, mapping, envelope, tools, aptix  

---

## **Objective**

Define how the Aptix Orchestrator converts a raw OpenAI `/responses` API result into the internal `AgentExecuteResponse` envelope consumed by Aptix clients such as the VS Code extension.

This DDR establishes:

- which `/responses` fields Aptix extracts
- how they map onto the strongly typed `AgentExecuteResponse`
- how tool calls are represented
- how token usage, finish reasons, and errors are handled
- how conversation threading and IDs are propagated

The final goal is a predictable, stable, transparent mapping layer that isolates the OpenAI API shape from client-facing behavior and enables higher-level Aptix features such as DDR management, readiness checks, implementation planning, and code generation.

---

## **Context**

AGN-001 through AGN-003 define the upstream phases of Aptix LLM calls:

- **AGN-001** — DDR Management Flow
- **AGN-002** — RAG Context Injection Pattern
- **AGN-003** — Responses API Request Construction

AGN-004 now defines the *downstream* side: once `/responses` returns, how is that raw JSON normalized into an `AgentExecuteResponse`?

The mapping rules here apply uniformly across all Aptix interaction modes (DDR, readiness, planning, codegen) and establish the long-term contract between the Orchestrator and clients.

---

## **Design**

### **1. AgentExecuteResponse Contract**

The enriched `AgentExecuteResponse` used by Aptix is:

```csharp
public class AgentExecuteResponse
{
    public string Kind { get; set; }

    public string ConversationId { get; set; }
    public string TurnId { get; set; }
    public string AgentContextId { get; set; }

    public string ConversationContextId { get; set; }
    public string ResponseContinuationId { get; set; }

    public string Mode { get; set; }

    public string ModelId { get; set; }

    public string Text { get; set; }

    public string FinishReason { get; set; }

    public LlmUsage Usage { get; set; } = new LlmUsage();

    public List<SourceRef> Sources { get; set; } = new List<SourceRef>();

    public FileBundle FileBundle { get; set; }

    public List<string> Warnings { get; set; } = new List<string>();

    public string ErrorCode { get; set; }

    public string ErrorMessage { get; set; }

    public string RawResponseJson { get; set; }

    public List<AgentToolCall> ToolCalls { get; set; } = new List<AgentToolCall>();
}

public class AgentToolCall
{
    public string CallId { get; set; }
    public string Name { get; set; }
    public string ArgumentsJson { get; set; }
}

public class LlmUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
```

This envelope:
- preserves all essential OpenAI metadata the Orchestrator needs, 
- provides clients with a consistent, typed API,
- includes raw and structured tool calls,
- supports RAG `Sources`, patch `FileBundle`s, and warnings for future codegen phases.

---

### **2. Mapping Raw `/responses` → AgentExecuteResponse**

Given a raw response (conceptually):

```jsonc
{
  "id": "resp_abc123",
  "model": "gpt-5.1",
  "output": [ ... ],
  "usage": {
    "prompt_tokens": 210,
    "completion_tokens": 310,
    "total_tokens": 520
  }
}
```

The Orchestrator performs the following mapping:

#### **2.1. Conversation and Continuation Fields**
- `ConversationId` ← from the incoming `AgentExecuteRequest`
- `ConversationContextId` ← from execution context
- `AgentContextId` ← from execution context
- `ResponseContinuationId` ← `response.id` (NEXT turn uses this as `previous_response_id`)
- `TurnId` ← also set to `response.id` unless overridden by an internal turn counter
- `Mode` ← from the incoming `AgentExecuteRequest`

#### **2.2. Model Information**
- `ModelId` ← `response.model`

#### **2.3. Token Usage**
```
Usage.PromptTokens     = response.usage.prompt_tokens
Usage.CompletionTokens = response.usage.completion_tokens
Usage.TotalTokens      = response.usage.total_tokens
```

If some fields are unavailable, they default to `0`.

#### **2.4. Finish Reason**
`FinishReason` is taken from:
- the final output segment’s `finish_reason`, or
- the dominant finish reason if multiple exist.

Typical values: `"stop"`, `"length"`, `"tool_use"`, `"error"`.

#### **2.5. Text Extraction**
`Text` is the concatenation of all natural language text segments found in `response.output`.

Rules:
- Include all `output_text` or message-like segments.
- Preserve order.
- Join with single blank lines.
- If no text exists (tool-only response), `Text` is empty or `null`.

#### **2.6. Tool Calls**
For each tool call in the output:

```
ToolCalls.Add(new AgentToolCall {
    CallId        = tool_call.id,
    Name          = tool_call.name,
    ArgumentsJson = <raw arguments JSON>
});
```

This preserves the arguments exactly as emitted by the model.

Clients deserialize based on the tool name.

#### **2.7. RawResponseJson**
`RawResponseJson` is a full serialization of the entire LLM response, including fields Aptix does not yet use.

This allows:
- client-side debugging,
- transparent inspection of LLM behavior,
- forward compatibility.

#### **2.8. Sources / FileBundle / Warnings**
None of these are populated directly from `/responses`. Instead:
- `Sources` is filled by RAG context tracking (mapping chunk Ids used in the request).
- `FileBundle` is populated by later phases (e.g., DDR / code patching tools).
- `Warnings` may be added by orchestration heuristics (e.g., truncated context, ambiguous tool calls).

---

### **3. Error Handling Rules**

If the call to `/responses` fails (HTTP error or API error):

- `Kind = "error"`
- `ErrorCode` ← error type or status
- `ErrorMessage` ← server or API error message
- `RawResponseJson` ← raw error payload when available
- `ModelId`, `ToolCalls`, `Usage` may be left defaulted

If the call succeeds but the model reports a logical error (e.g., missing context):
- `Kind` remains `"ok"`
- `ErrorMessage` may be included in `Text`
- Clients decide how to react

---

### **4. Kind Classification**

`Kind` is a high-level classification used by clients to decide how to present results.

The Orchestrator assigns:

- `"ok"` → natural text and/or tool calls, no structural errors
- `"tool-only"` → no user-facing text, only tool calls
- `"error"` → HTTP or API-level error
- `"empty"` → no text, no tool calls (rare)

This is not dictated by OpenAI; it is an Aptix convention.

---

### **5. Turn Lifecycle**

The Orchestrator:
1. Sends request with `previous_response_id` (except on first turn).
2. Receives `/responses.id`.
3. Stores this as `ResponseContinuationId` on the server.
4. Returns it to the client inside `AgentExecuteResponse`.
5. Client includes it in the next `AgentExecuteRequest` to continue the conversation.

This keeps the server/LLM chain aligned with the client-side conversation ID.

---

## **Risks / Open Questions**

1. **Multiple text segments with interleaved tool calls**  
   The model may interleave partial text → tool call → partial text. Our current approach concatenates only text segments; future refinement may annotate order.

2. **Streaming support**  
   `/responses` may support streaming later; this DDR assumes fully materialized results.

3. **Rich finish reasons**  
   Some workflows may need per-segment finish reasons; this DDR simplifies to a single one.

4. **Token usage inconsistencies**  
   Some models may omit usage fields on error; Aptix defaults to zero.

---

## **Testing Strategy**

- Unit tests verifying:
  - extraction of `ModelId`, `Usage`, and `FinishReason`
  - extraction and concatenation of text
  - detection and serialization of tool calls
  - construction of RawResponseJson
  - classification of `Kind`
  - error path behavior (HTTP/API failures)

- Integration tests with live `/responses`:
  - DDR flows: tool calls only
  - Mixed flows: text + tool calls
  - No-RAG vs RAG-heavy contexts
  - Long output vs truncated output (finish reason = length)

---

## **Notes / Rationale**

AGN-004 finalizes the foundational infrastructure for Aptix LLM interactions:

- **AGN-001** set the workflow basis (DDR management).
- **AGN-002** defined input context structure (RAG blocks).
- **AGN-003** defined outbound request construction.
- **AGN-004** now defines inbound result processing.

Together, these create the complete contract for *how Aptix talks to the model and how the model talks back*, which is critical before implementing higher-level orchestration such as readiness checks, implementation planning, or code generation.
