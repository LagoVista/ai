# AGN-005 — Aptix Agent Tool Implementation Specification

**Title:** Agent Tool Implementation and Execution Contract  
**Status:** Accepted  
**Version:** 1.1  
**Owner:** Aptix Orchestrator / Reasoner  
**Namespace:** LagoVista.AI.Services.Tools  
**Last Updated:** 2025-11-29  

---

## 1. Purpose

This DDR defines the required contract for implementing Aptix Agent Tools executed by the Aptix Reasoner via the Agent Tool Executor.

All tools:
- run *only* on the server side
- implement `IAgentTool`
- communicate strictly through JSON argument payloads and JSON result payloads
- expose a static `GetSchema()` describing their OpenAI tool/function schema
- expose a `ToolUsageMetadata` constant string describing detailed, LLM-facing usage guidance
- participate in a strict call/return protocol through the OpenAI `/responses` API

This guarantees stability, validation, deterministic schemas, consistent logging, predictable error flows, rich usage guidance, and turn resumption.

---

## 2. Tool Contract

### 2.1 Required Interface

All tools MUST implement:

```csharp
public interface IAgentTool
{
    string Name { get; }

    Task<InvokeResult<string>> ExecuteAsync(
        string argumentsJson,
        AgentToolExecutionContext context,
        CancellationToken cancellationToken = default);

    static object GetSchema();
}
```

In addition, all tool *types* MUST define:

```csharp
public const string ToolUsageMetadata = """...""";
```

- `GetSchema()` exposes the OpenAI tool/function JSON schema.
- `ToolUsageMetadata` exposes human-readable, LLM-facing usage guidance that the Reasoner will surface in the system prompt.

> Note: `ToolUsageMetadata` is a **contractual requirement of this DDR**, but cannot be enforced via the `IAgentTool` interface because it is a static member. Compliance is enforced by convention, code review, and automated validation.

### 2.2 Naming Rules
- Unique system-wide
- `lowercase_snake_case`
- Must match the `name` inside `GetSchema()`
- Used directly by the Reasoner to bind calls to tool instances

Example:
```csharp
public const string ToolName = "testing_ping_pong";
public string Name => ToolName;
```

---

## 3. Argument Handling

### 3.1 JSON Arguments Only
Tools receive the raw JSON string from the LLM tool call.

Tools MUST:
- Validate for null/empty
- Deserialize using **Json.NET**
- Never throw on deserialization errors
- Treat missing/null fields as defaults
- Return `InvokeResult<string>.FromError()` on validation issues

DTOs SHOULD be private classes:
```csharp
private sealed class PingPongArgs
{
    public string Message { get; set; }
    public int? Count { get; set; }
}
```

### 3.2 Required Field Enforcement
Tools MUST explicitly validate required fields and return structured error results.

---

## 4. Return Values & Serialization

### 4.1 Return Type
Tools MUST return:
```csharp
InvokeResult<string>
```
where `.Result` contains a JSON string.

### 4.2 Serialization
Tools MUST serialize responses using **Json.NET**:
```csharp
var json = JsonConvert.SerializeObject(result);
return InvokeResult<string>.Create(json);
```

### 4.3 Tools MUST NOT THROW
Exceptions must be caught and wrapped:
```csharp
catch (Exception ex)
{
    _logger.AddException("[ToolName_ExecuteAsync__Exception]", ex);
    return InvokeResult<string>.FromError("ToolName failed to process arguments.");
}
```

---

## 5. Execution Context Requirements

Tools receive:
```csharp
AgentToolExecutionContext context
```

Tools MUST include identifiers in results:
```csharp
SessionId = context?.SessionId,
ConversationId = context?.Request?.ConversationId,
```

This ensures full traceability for sessions, turns, and replay.

---

## 6. Logging Contract

### 6.1 Required exception logging
```csharp
_logger.AddException("[<ToolName>_ExecuteAsync__Exception]", ex);
```

### 6.2 Custom event logging
Tools MAY log custom events using **arrays of KeyValuePair**, NOT dictionaries:
```csharp
_logger.AddCustomEvent(
    LogLevel.Error,
    "FailureInjectionTool.ExecuteAsync",
    "Intentional failure requested.",
    new[]
    {
        new KeyValuePair<string,string>("ConversationId", context?.Request?.ConversationId ?? ""),
        new KeyValuePair<string,string>("SessionId", context?.SessionId ?? ""),
        new KeyValuePair<string,string>("Payload", args.Payload ?? "")
    });
```

---

## 7. Static Schema & Usage Contract

All tools MUST define:

```csharp
public static object GetSchema()
```

and:

```csharp
public const string ToolUsageMetadata = """...""";
```

### 7.1 Schema (`GetSchema`)

`GetSchema()` MUST return an anonymous object following the OpenAI "function" schema format:

```csharp
return new
{
    type = "function",
    name = ToolName,
    description = "...",
    parameters = new
    {
        type = "object",
        properties = new { /* ... */ },
        required = Array.Empty<string>()
    }
};
```

Schema requirements:
- Must be deterministic and stable across process restarts.
- Must fully describe all arguments the tool expects.
- `name` must match `ToolName` and `IAgentTool.Name`.
- `description` must be clear enough for the LLM to decide when to call the tool.

The Reasoner uses this schema to populate the `/responses` request `tools` array. Without a valid schema, the tool cannot be safely exposed to the LLM.

### 7.2 Usage Metadata (`ToolUsageMetadata`)

Each tool MUST define a constant, multi-line string:

```csharp
public const string ToolUsageMetadata = """
<detailed usage guidance>
""";
```

This string:
- Is **LLM-facing usage guidance** for how, when, and why to use the tool.
- Is surfaced by the Aptix Reasoner in the system prompt alongside the tool schema.
- Acts as the canonical, code-adjacent distillation of the DDR for that tool.

#### 7.2.1 Content Requirements

`ToolUsageMetadata` MUST:
- Describe the tool's **primary purpose** and intent.
- Explain **when to use** the tool and when **not** to use it.
- Explain how to construct arguments (e.g., "always use DocPath from RAG snippet headers").
- Describe important behavior such as:
  - precedence rules (e.g., `ActiveFiles` vs. backing store)
  - idempotency expectations
  - paging/limits behavior
  - performance considerations ("call sparingly" vs. "safe to call often")
- Document known **error codes** or error shapes returned in the JSON payload.
- Provide **good vs. bad usage examples** where relevant.
- Be written in clear, concise English that the Reasoner can drop into the system prompt.

`ToolUsageMetadata` MUST NOT:
- Contain secrets, API keys, or environment-specific configuration.
- Contradict the `GetSchema()` definition.
- Make assumptions about internal implementation details that can change without breaking the contract.

#### 7.2.2 Recommended Format

While not strictly required, the following format is RECOMMENDED:

```text
<tool_name> — Usage Guide

Primary purpose:
- ...

Rules:
- ...

Arguments:
- path: ...
- maxBytes: ...

Error codes:
- ALREADY_IN_CONTEXT: ...
- NOT_FOUND: ...

Good usage:
- ...

Bad usage:
- ...
```

This structure makes it easy for the Reasoner to display the guidance in a consistent way for all tools.

#### 7.2.3 Reasoner Responsibilities

The Aptix Reasoner MUST:
- Discover `ToolUsageMetadata` for each tool type via reflection.
- Inject the usage text into the system prompt or a dedicated "tool guidance" section alongside the OpenAI tool schema.
- Ensure that:
  - `ToolUsageMetadata` is kept in sync with the exposed schema.
  - Missing or empty `ToolUsageMetadata` is treated as a configuration error.

---

## 8. Cancellation Support

Tools MUST support `CancellationToken`.

For long-running actions:
```csharp
await Task.Delay(ms, cancellationToken);
```

If canceled:
- Tool MUST return a **successful InvokeResult** describing cancellation.
- Tool MUST NOT throw.

---

## 9. Error Propagation Rules

Tools may intentionally return:
```csharp
InvokeResult<string>.FromError("Message here");
```

Tool failures MUST:
- Propagate through the Tool Executor.
- Be encoded as **tool error messages** in the `/responses` turn.
- Allow the Reasoner to reconcile and continue.

Tools MUST NOT:
- Throw exceptions for normal failures.
- Generate malformed JSON.
- Crash the executor.

---

## 10. Reference Implementations

The following tools strictly conform to this DDR:

- `PingPongTool`
- `CalculatorTool`
- `DelayTool`
- `FailureInjectionTool`

These demonstrate valid patterns:
- argument DTOs
- deterministic schemas
- cancellation support
- error injection and propagation
- structured logging
- inclusion of `ToolUsageMetadata` with clear, LLM-facing guidance

---

## 11. Future Extensions

This DDR allows extension into:
- multi-step or streaming tools
- binary/base64 return payloads
- complex nested JSON schemas
- permission-aware tools
- identity-aware tools using `context.UserId`

No changes to the core interface are required. `ToolUsageMetadata` SHOULD be updated to reflect any new behavior or constraints.

---

## 12. Acceptance Criteria

A tool is compliant IFF:
- It implements all requirements in sections **2–9**.
- It defines a non-empty `ToolUsageMetadata` constant string describing how the tool should be used.
- It never throws unhandled exceptions.
- It includes session + conversation identifiers in results.
- It returns stable JSON payloads.
- The schema output is deterministic and matches the actual argument/response shapes.
- Logging uses correct `KeyValuePair<string,string>` arrays.
- Error results propagate correctly through the orchestrator.
- The Reasoner can discover and surface both the schema (`GetSchema()`) and usage guidance (`ToolUsageMetadata`) for the tool.

---

**End of AGN-005**
