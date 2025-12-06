# TUL-011 — Agent Tool Contract

**ID:** TUL-011  
**Title:** Agent Tool Contract  
**Status:** Approved  
**Owner:** Kevin Wolf & Aptix  

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-06 13:30:00 EST (UTC-05:00)

---

## 1. Purpose

This DDR defines the formal contract for all Aptix agent tools (IAgentTool). It standardizes how tools are identified, registered, executed, tested, and documented so the Reasoner and LLM can call them safely and consistently.

---

## 2. Interface and Registration

- All tools must implement IAgentTool with: Name, IsToolFullyExecutedOnServer, and ExecuteAsync(string argumentsJson, AgentToolExecutionContext context, CancellationToken).
- Tools are created via dependency injection and should accept IAdminLogger plus any required services in their constructor.
- Each tool must declare public const string ToolName and public const string ToolUsageMetadata as fields (not properties). Both must be non-empty; ToolName must match regex ^[a-zA-Z0-9_-]+$ and be returned by the Name property.
- Each tool must declare public static object GetSchema() which returns an OpenAI-style function tool schema object.
- AgentToolRegistry.RegisterTool<T>() uses reflection to enforce these rules and will log and throw InvalidOperationException if a tool is malformed or a ToolName is duplicated.
- Startup.ConfigureServices wires up the registry and registers all tools; a StartupTests.ConfigureServices_WithValidTools_DoesNotThrow test ensures that all registered tools satisfy the contract before deployment.

---

## 3. Execution Semantics

- ExecuteAsync must deserialize argumentsJson into a private Args class using Json.NET, validate required fields, and return InvokeResult<string>.FromError(...) for invalid input.
- On success, the tool must create a small Result object, serialize it to JSON, and return InvokeResult<string>.Create(json).
- Tools should include helpful IDs (such as ConversationId and SessionId) from AgentToolExecutionContext in the result where appropriate.
- Validation failures (missing or bad arguments) must be returned as failed InvokeResult instances with clear, human-readable messages; tools should not throw for expected validation errors.
- Unexpected exceptions must be logged via IAdminLogger.AddException and converted into a generic failed InvokeResult<string> without leaking stack traces or sensitive details.
- Long-running tools should observe the CancellationToken and exit cooperatively when cancellation is requested.

---

## 4. Schema and Documentation

- GetSchema must return an object that serializes to a function tool schema with: type = function, name = ToolName, a short description, and a parameters object with type = object, properties, and required arrays.
- Parameter properties must declare simple JSON types (string, number, integer, boolean, object, array) and a short description for each field.
- Schemas must be deterministic and stable; they must not include timestamps, GUIDs, or random values.
- ToolUsageMetadata provides a short, natural-language usage guideline describing when the LLM should call this tool.
- The schema description provides a concise summary of what the tool does; it should be consistent with ToolUsageMetadata but may be slightly more compact.

---

## 5. Testing Expectations

- Each tool must be covered by unit tests that validate at least: a successful ExecuteAsync happy path, argument validation failures, and exception handling behavior.
- Registration contract testing can be satisfied at the solution level via StartupTests.ConfigureServices_WithValidTools_DoesNotThrow, which exercises AgentToolRegistry.RegisterTool for all production tools.
- Tools may also have optional tool-specific registration tests that call AgentToolRegistry.RegisterTool<YourTool>() and assert that no exception is thrown.
- Tests should deserialize the JSON result payload and verify that key fields are present and correctly populated.

---

## 6. Reference Example: HelloWorldTool

HelloWorldTool is the canonical minimal example of a compliant Aptix agent tool:

- ToolName = agent_hello_world; Name returns ToolName.
- ToolUsageMetadata explains that the tool generates a personalized greeting when the user asks to be greeted or welcomed.
- The Args class contains a single string property Name; ExecuteAsync validates that Name is non-empty.
- On success, ExecuteAsync returns JSON containing Message, ConversationId, and SessionId fields in a HelloWorldResult payload.
- GetSchema returns a function tool schema with one required string parameter name and a short description describing that the tool creates a friendly greeting using the user's name.

TUL-011 is now approved and serves as the authoritative contract for all Aptix agent tools.