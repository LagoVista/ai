# AGN-005 — Aptix Agent Tool Implementation Specification

**Title:** Agent Tool Implementation and Execution Contract  
**Status:** Accepted  
**Version:** 1.2  
**Owner:** Aptix Orchestrator / Reasoner  
**Namespace:** LagoVista.AI.Services.Tools  
**Last Updated:** 2025-11-29  

---

## 1. Purpose

This DDR defines the contract for implementing Aptix Agent Tools executed by the Aptix Reasoner via the Agent Tool Executor.

The goals for this spec are:

- Provide a single, stable interface (`IAgentTool`) for all tools the LLM can invoke.
- Clearly describe _how we know_ whether a tool is fully executed on the server or requires a client-side executor.
- Clearly describe _where this logic is used_ in the runtime (Agent Tool Executor and Agent Reasoner).
- Ensure tools are safe, deterministic, and observable (logging + error handling).

This document supersedes earlier versions of AGN-005 by adding an explicit execution-mode property on `IAgentTool`.

---

## 2. Scope

**In scope**

- All tools the LLM is allowed to call via the Aptix Orchestrator.
- The `IAgentTool` interface and its expected behavior.
- How tools plug into:
  - `IAgentToolExecutor`
  - `AgentReasoner`
  - `AgentExecuteRequest` / `AgentExecuteResponse`

**Out of scope (for now)**

- Detailed per-tool schemas (arguments / outputs for specific tools).
- Full client-side execution flow (IDE integration, UI behavior, etc.).
- Telemetry pipeline beyond the basic logging requirements.

A future revision may add a dedicated “tool flows” DDR once we have real-world experience with the patterns defined here.

---

## 3. Execution Model Overview

From the LLM’s perspective, every tool is just a JSON-described function that returns JSON. Internally, Aptix breaks tool execution into three layers:

1. **Server Tool Implementation (`IAgentTool`)**
   - Lives in the LagoVista backend.
   - Implements validation, shaping, and optionally full server-side behavior.
   - Exposes `IsToolFullyExecutedOnServer` to declare its execution mode.

2. **Agent Tool Executor (`IAgentToolExecutor`)**
   - Discovers and invokes `IAgentTool` instances.
   - Maps tool implementations onto `AgentToolCall` results.
   - Encodes whether further client-side execution is required.

3. **Agent Reasoner (`AgentReasoner`)**
   - Calls the LLM and receives tool calls.
   - Uses `IAgentToolExecutor` to run tools.
   - Decides whether:
     - It can remain in a server-only loop (all tools fully handled on server), or
     - It must return to the client for additional execution (one or more tools require client execution).

**Important invariant**

> Every tool the LLM can call has a server-side `IAgentTool` implementation. There are no “client-only” tools in the protocol.  
>  
> What we previously called “client tools” are now defined as tools whose server implementation performs only validation + shaping and declares that the final behavior must be executed by a client-side executor.

---

## 4. `IAgentTool` Contract

All agent tools MUST implement the `IAgentTool` interface (namespace may differ slightly based on final project layout):

```csharp
using System.Threading;
using System.Threading.Tasks;
using LagoVista.Core.Validation;
using LagoVista.AI.Models;

namespace LagoVista.AI.Interfaces
{
    public interface IAgentTool
    {
        /// <summary>
        /// Stable, unique tool name used in LLM tool schemas and in
        /// AgentToolCall.ToolName. Must be globally unique within the
        /// Aptix tool catalog.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Human-readable usage guidance for this tool. This content may
        /// be surfaced to LLMs as part of system or developer prompts
        /// and MUST be kept in sync with the actual behavior.
        /// </summary>
        string ToolUsageMetadata { get; }

        /// <summary>
        /// Declares whether this tool performs its full execution on the server.
        ///
        /// - true  => The tool validates inputs AND performs its final behavior
        ///            entirely on the server. The JSON result returned from
        ///            ExecuteAsync can be passed directly back to the LLM as
        ///            a completed tool_result.
        ///
        /// - false => The tool performs only server-side validation, shaping,
        ///            authorization checks, and/or enrichment. It does NOT
        ///            perform the final side effect. Instead, it prepares a
        ///            payload for a client-side executor to consume.
        ///
        /// In both cases, the tool still runs on the server and is invoked
        /// via IAgentToolExecutor. This flag is the canonical source of truth
        /// for execution mode decisions in the Reasoner.
        /// </summary>
        bool IsToolFullyExecutedOnServer { get; }

        /// <summary>
        /// Executes the tool logic.
        ///
        /// Implementations MUST:
        /// - Never throw unhandled exceptions (return failures via InvokeResult).
        /// - Respect the IsToolFullyExecutedOnServer contract.
        /// - Populate the supplied AgentToolCall with status, messages, and
        ///   any JSON payloads produced.
        /// </summary>
        Task<InvokeResult<AgentToolCall>> ExecuteAsync(
            AgentToolCall call,
            AgentToolExecutionContext context,
            CancellationToken cancellationToken);
    }
}
```

Key points:

- The **tool itself** declares whether it is **server-final** (`IsToolFullyExecutedOnServer = true`) or **server-preflight + client-final** (`IsToolFullyExecutedOnServer = false`).
- `IAgentToolExecutor` and `AgentReasoner` MUST treat this property as the single source of truth for execution mode.

---

## 5. Agent Tool Executor Responsibilities

The Agent Tool Executor is responsible for:

1. **Discovery**
   - Maintain a registry of `IAgentTool` instances keyed by `Name`.
   - Return a clear failure if a requested tool name cannot be resolved.

2. **Execution**
   - For each `AgentToolCall` emitted by the LLM, find the matching `IAgentTool`.
   - Invoke `ExecuteAsync`, passing:
     - The `AgentToolCall` representing the LLM’s request.
     - An `AgentToolExecutionContext` containing Agent/Conversation/Session/Org/User info.

3. **Mapping to `AgentToolCall`**
   - After execution, the executor MUST:
     - Set `AgentToolCall.IsServerTool = true` (all tools are mediated by the server).
     - Set `AgentToolCall.WasExecuted` based on whether `ExecuteAsync` completed successfully.
     - Use `IAgentTool.IsToolFullyExecutedOnServer` to decide whether additional client-side execution is required.

   A typical mapping will look like:

   - **Server-final tools** (`IsToolFullyExecutedOnServer == true`)
     - `IsServerTool = true`
     - `WasExecuted = true` (unless there was a failure)
     - Tool populates `ResultJson` with the final LLM-facing result.
     - No explicit “client required” marker is needed for this mode.

   - **Server-preflight tools** (`IsToolFullyExecutedOnServer == false`)
     - `IsServerTool = true`
     - `WasExecuted = true` (preflight completed)
     - Tool populates a “prepared payload” for the client (e.g., a normalized request object).
     - The executor/Reasoner will ultimately ensure that this tool call is surfaced back to the client for the final step.

4. **Error handling**
   - The executor MUST never allow unhandled exceptions to escape.
   - Failures MUST be converted into `InvokeResult` failure responses.
   - `AgentToolCall` should carry error messages so they are visible to the Reasoner and ultimately to the client if needed.

A future revision will define the exact `AgentToolCall` fields used to signal “client execution required” (e.g., a `RequiresClientExecution` flag and/or a dedicated client payload property). For now, **AGN-005 treats `IsToolFullyExecutedOnServer` as the canonical declaration of intent**, and the executor is responsible for mapping that intent into the transport model.

---

## 6. Agent Reasoner Integration

The `AgentReasoner` uses tools via `IAgentToolExecutor`. The relevant control-flow rules are:

1. **Tool discovery loop**
   - After each LLM call, the Reasoner inspects `AgentExecuteResponse.ToolCalls`.
   - For each tool call, it invokes `IAgentToolExecutor.ExecuteServerToolAsync`.

2. **Server-only loop vs. client handoff**
   - If **all** tools associated with a response have `IsToolFullyExecutedOnServer == true` and execute successfully:
     - The Reasoner MAY remain in a server-only loop:
       - Collect tool results.
       - Feed them back to the LLM via `AgentExecuteRequest.ToolResultsJson`.
       - Continue until the LLM returns a response with no additional tool calls or the max iteration safeguard is hit.

   - If **any** tool has `IsToolFullyExecutedOnServer == false`:
     - The Reasoner MUST ensure that the corresponding `AgentToolCall` entries are returned to the client in `AgentExecuteResponse.ToolCalls`.
     - The client is responsible for:
       - Interpreting the preflighted payload from the server.
       - Executing the final behavior locally (IDE, UI, etc.).
       - Sending the final tool results back in a subsequent `AgentExecuteRequest` via `ToolResultsJson`.

3. **How this answers “how we know” and “where it is used”**
   - **How we know**  
     - We know the intended execution mode of a tool because the tool itself declares it via `IAgentTool.IsToolFullyExecutedOnServer`.
   - **Where it is used**  
     - `IAgentToolExecutor` reads `IsToolFullyExecutedOnServer` and maps it into the `AgentToolCall` result.
     - `AgentReasoner` uses those `AgentToolCall` instances (and associated flags) to decide whether it can stay in a server-only loop or must hand control back to the client.

---

## 7. Logging Contract

Tools MUST be well-behaved citizens in the logging ecosystem.

1. **Required exception logging**

   When a tool catches an exception, it MUST log it using the standard pattern:

   ```csharp
   _logger.AddException("[<ToolName>_ExecuteAsync__Exception]", ex);
   ```

2. **Custom events**

   Tools MAY log custom events using arrays of `KeyValuePair<string,string>` (not dictionaries), for example:

   ```csharp
   _logger.AddCustomEvent(
       LagoVista.Core.PlatformSupport.LogLevel.Error,
       "<ToolName>_ExecuteAsync",
       "Tool execution failed validation.",
       new[]
       {
           new KeyValuePair<string, string>("ConversationId", context?.Request?.ConversationId ?? string.Empty),
           new KeyValuePair<string, string>("SessionId", context?.SessionId ?? string.Empty)
       });
   ```

---

## 8. Compliance Checklist

A tool is compliant with AGN-005 if and only if:

- It implements `IAgentTool` exactly as defined in this DDR.
- It provides a stable, unique `Name`.
- It defines a non-empty `ToolUsageMetadata` string that accurately describes usage.
- It sets `IsToolFullyExecutedOnServer` correctly to describe its intended execution mode.
- It never allows unhandled exceptions to escape from `ExecuteAsync`.
- It returns deterministic JSON payloads whose schema matches the advertised behavior.
- It includes session and conversation identifiers in any result or log entries where appropriate.
- It logs exceptions and optional custom events according to the logging contract.
- It integrates cleanly with `IAgentToolExecutor` and can be exercised end-to-end via the Agent Reasoner.

---

**End of AGN-005 (v1.2)**
