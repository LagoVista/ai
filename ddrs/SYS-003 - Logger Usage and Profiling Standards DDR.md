# SYS-003 — Logger Usage and Profiling Standards DDR

**ID:** SYS-003  
**Title:** Logger Usage and Profiling Standards  
**Status:** Approved  
**Owner:** Kevin Wolf & Aptix  
**Scope:** All LagoVista / Aptix code (human- and agent-authored) that performs logging or profiling via `IAdminLogger`.

---

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-02 14:30:00 EST (UTC-05:00)

---

## 1. Purpose & Scope

1. SYS-003 defines the canonical **logging and profiling usage contract** for all code that logs via `IAdminLogger`. It is designed to be **self-contained** so that agents can generate correct logging code **without** seeing the underlying logger implementation.
2. This DDR is a **usage standard**, not an implementation spec. It defines how to:
   - Obtain `IAdminLogger` via DI
   - Call logging and profiling APIs
   - Apply tag and KVP conventions
   - Control noise
   - Protect security and privacy
3. All logging in the LagoVista / Aptix ecosystem must go through `IAdminLogger`. The underlying `ILogger` interface is never injected or called directly in application code.
4. This DDR includes the necessary **method signatures**, **enums**, and **namespace requirements** so that generated code compiles using only this document as reference.

### 1.1 Surface Area: IAdminLogger Only

- Application code must depend on:
  - `LagoVista.IoT.Logging.Loggers.IAdminLogger`
- Although `IAdminLogger` inherits from `LagoVista.Core.PlatformSupport.ILogger`, code must **not** inject or call `ILogger` directly.
- The effective logging surface is the union of:
  - Methods defined on `IAdminLogger`
  - Methods inherited from `ILogger`
- All examples and rules assume an injected field:
  - `private readonly IAdminLogger _adminLogger;`

### 1.2 Required Namespaces for Logging Usage

Agents should assume the following namespaces when generating logging code:

- `using LagoVista.IoT.Logging.Loggers;`   // IAdminLogger and admin-specific helpers  
- `using LagoVista.Core.PlatformSupport;`  // LogLevel, MetricType, TimedEvent  
- `using LagoVista.Core.Validation;`       // InvokeResult, InvokeResult<T>  
- `using System;`                          // Exception, DateTime, etc.  
- `using System.Collections.Generic;`      // KeyValuePair<,>, Dictionary<,>

Additional namespaces may be required by the calling context but are outside the scope of this DDR.

---

## 2. Logger Acquisition & Lifetime via DI

### 2.1 Required Injection Pattern

1. All non-test code must obtain the logger via **constructor injection**:
   - Add `IAdminLogger` as a constructor parameter.
   - Store it in a `private readonly IAdminLogger _adminLogger;` field.
2. This pattern applies to:
   - Services, controllers, and handlers
   - Orchestrators and agents
   - Background workers and hosted services
   - Console apps that later resolve dependencies from DI

### 2.2 Prohibited Patterns

Generated code must **not**:

- Inject `ILogger` directly.  
- Use static or global logger instances.  
- Resolve `IAdminLogger` via `IServiceProvider` (service locator).  
- Use property injection (`public IAdminLogger Logger { get; set; }`).  
- Instantiate implementation types directly (except as explicitly allowed for tests and console bootstrapping).

### 2.3 Test-Friendly Logger Construction

For **tests**, it is valid to construct a concrete logger:

- `new AdminLogger(new ConsoleLogger())`
- Required namespaces:
  - `using LagoVista.IoT.Logging.Loggers;`  
  - `using LagoVista.IoT.Logging.Utils;`

This instance implements `IAdminLogger` and writes output to the console, which is useful for integration-style tests. Unit tests may also use mocking (e.g., `Mock<IAdminLogger>`) according to test DDRs.

### 2.4 Console Application Bootstrapping

For **console apps**, it is valid to:

1. Construct a logger instance once at startup (e.g., `new AdminLogger(new ConsoleLogger())`).  
2. Register it with DI as `IAdminLogger`.  
3. Continue to follow the standard constructor injection pattern for all other types.

---

## 3. Log Levels, Metric Types, and Core APIs

This section defines the enums and core API surface available via `IAdminLogger` (inherited from `ILogger`).

### 3.1 Enums

Required namespace: `using LagoVista.Core.PlatformSupport;`

#### 3.1.1 LogLevel

```csharp
public enum LogLevel
{
    UserEntryError,
    Error,
    UnhandledException,
    Warning,
    Message,
    StateChange,
    Verbose,
    TimedEvent,
    Metric,
    ConfigurationError,
    Authentication,
    Authorization
}
```

High-level mapping for agents:

- `UserEntryError` – Invalid user input, validation failures.  
- `Error` – Recoverable system errors that did not crash the process.  
- `UnhandledException` – Top-level catches of unexpected exceptions.  
- `Warning` – Suspicious or degraded behavior that still succeeded.  
- `Message` – Important informational events (start/stop, key checkpoints).  
- `StateChange` – Significant state transitions (service, workflow, configuration).  
- `Verbose` – Detailed diagnostic trace; used sparingly.  
- `TimedEvent` – Events representing timed operations.  
- `Metric` – Metric-related events.  
- `ConfigurationError` – Misconfiguration or invalid settings.  
- `Authentication` – Login/token-related events.  
- `Authorization` – Access control checks and denials.

#### 3.1.2 MetricType

```csharp
public enum MetricType
{
    Event,
    Aggregate
}
```

- `Event` – A single occurrence measurement (latency for one call, count for one operation).  
- `Aggregate` – Pre-aggregated metric (counts, averages over a window).

### 3.2 Core ILogger API (Used via IAdminLogger)

Required namespaces:

- `using System;`  
- `using System.Collections.Generic;`  
- `using LagoVista.Core.PlatformSupport;`

The inherited `ILogger` surface (called **only** via an `IAdminLogger` instance) is:

```csharp
bool DebugMode { get; set; }

TimedEvent StartTimedEvent(string area, string description);
void EndTimedEvent(TimedEvent evt);

void AddKVPs(params KeyValuePair<string, string>[] args);

void AddCustomEvent(LogLevel level, string tag, string customEvent, params KeyValuePair<string, string>[] args);

void AddException(string tag, Exception ex, params KeyValuePair<string, string>[] args);

void TrackEvent(string message, Dictionary<string, string> parameters);

void TrackMetric(string kind, string name, MetricType metricType, double count, params KeyValuePair<string, string>[] args);
void TrackMetric(string kind, string name, MetricType metricType, int count, params KeyValuePair<string, string>[] args);

void Trace(string message, params KeyValuePair<string, string>[] args);
```

Agents must always call these APIs on `_adminLogger` (an `IAdminLogger` instance), never on `ILogger` directly.

---

## 4. IAdminLogger Error and InvokeResult APIs

Required namespaces:

- `using System.Collections.Generic;`  
- `using LagoVista.Core.Validation;`  
- `using LagoVista.IoT.Logging.Loggers;`

Additional surface specific to `IAdminLogger`:

```csharp
void AddError(string tag, string message, params KeyValuePair<string, string>[] args);

void AddError(ErrorCode errorCode, params KeyValuePair<string, string>[] args);

void AddConfigurationError(string tag, string message, params KeyValuePair<string, string>[] args);

void AddMetric(string measure, double duration);
void AddMetric(string measure, int count);

void LogInvokeResult(string tag, InvokeResult result, params KeyValuePair<string, string>[] args);
void LogInvokeResult<TResultType>(string tag, InvokeResult<TResultType> result, params KeyValuePair<string, string>[] args);
```

Usage rules for agents:

1. `AddError(string, string, args)` – For business or system errors best described with a message plus metadata.
2. `AddError(ErrorCode, args)` – For structured errors represented by `ErrorCode` values; prefer this if an `ErrorCode` is available.
3. `AddConfigurationError` – For misconfiguration or invalid settings; prefer this over `AddError` when configuration is at fault.
4. `LogInvokeResult` (both overloads):
   - Always log **failed** `InvokeResult` / `InvokeResult<T>` instances.  
   - Optionally log successful results for important or audited workflows.  
   - Agents do not inspect the details of `InvokeResult`; they pass the result and relevant KVPs.
5. `AddMetric` is a convenience for simple numeric metrics; detailed patterns are covered under metrics and profiling.

---

## 5. Metrics & Profiling (TimedEvent + Metrics)

### 5.1 Profiling with TimedEvent (Required Pattern)

- `TimedEvent StartTimedEvent(string area, string description);`  
- `void EndTimedEvent(TimedEvent evt);`

Rules for agents:

1. Use `StartTimedEvent` / `EndTimedEvent` for **non-trivial operations**, such as:
   - External HTTP or database calls
   - Significant orchestrations or workflows
   - Background/batch jobs
2. Always ensure `EndTimedEvent` is called, typically in a `finally` block.  
3. `area` should identify a high-level feature (for example, `OrderProcessing`).  
   `description` should identify the specific operation (for example, `ProcessOrderAsync`).

### 5.2 Simple Metrics via AddMetric

- `void AddMetric(string measure, double duration);`  
- `void AddMetric(string measure, int count);`

Guidance:

- Use dot-separated metric names (for example, `OrderProcessing.DurationMs`).  
- Use the `double` overload for durations or continuous values.  
- Use the `int` overload for counts.  
- Use `AddMetric` when a single numeric value is sufficient and no extra metadata is required.

### 5.3 Rich Metrics via TrackMetric

- `TrackMetric(string kind, string name, MetricType metricType, double count, params KeyValuePair<string, string>[] args);`  
- `TrackMetric(string kind, string name, MetricType metricType, int count, params KeyValuePair<string, string>[] args);`

Guidance:

- `kind` – Logical group (for example, `OrderProcessing`, `HttpClient`).  
- `name` – Specific metric name (for example, `ChargeLatencyMs`).  
- `metricType` – `Event` for single measurements, `Aggregate` for pre-aggregated values.  
- Use KVPs to add context (tenant, provider, endpoint, etc.).

Agents should choose `TrackMetric` when richer tagging and metadata for dashboards/alerting is important.

---

## 6. Tagging, Correlation, and Structured KVPs

### 6.1 Tag Format (Required)

All logger APIs that accept a `tag` parameter must use this pattern:

- `[ClassName__MethodName]`  
- `[ClassName__MethodName__Activity]` (for multi-activity methods)

Rules for agents:

1. Use the exact class and method names, in PascalCase, separated by double underscores.  
2. Do not include namespaces.  
3. Always wrap the tag in square brackets.

Examples (as strings in code):

- `[OrderProcessor__Process]`  
- `[OrderProcessor__Process__Validate]`  
- `[UserService__CreateUser]`

This convention applies to **all** tag parameters across all logging APIs.

### 6.2 Tags in Trace Messages

When writing `Trace` messages, prepend the same tag at the beginning of the text:

- Example: `"[UserService__CreateUser] - Starting user creation flow."`

This ensures textual logs can still be grouped by class and method.

### 6.3 Standard KVP Keys

Agents should consistently use the following keys when relevant:

- `CorrelationId`  
- `TenantId`  
- `UserId`  
- `DeviceId`  
- `Operation`  
- `Status`  
- `ErrorCode`  
- `Endpoint`  
- `Provider`  
- `OrderId`  
- `EntityId`

These keys should be reused across logs and metrics for consistent analysis.

### 6.4 String.ToKVP Helper

There is an extension method on `string`:

- `stringValue.ToKVP("KeyName")`

This produces a `KeyValuePair<string, string>` with key `KeyName` and value `stringValue`.

Agents should:

- Prefer `.ToKVP("KeyName")` when the value is already a string.  
- Use `value.ToString().ToKVP("KeyName")` when the value is not a string.

Example usage pattern (conceptual):

- `tenantId.ToKVP("TenantId")`  
- `correlationId.ToKVP("CorrelationId")`  
- `orderId.ToString().ToKVP("OrderId")`

### 6.5 args Usage

- Use inline KVPs for a few metadata values.  
- Build an array if there are many.  
- Never pass `null` as the `params` array.

### 6.6 AddKVPs for Ambient Metadata

- `AddKVPs` may be used to attach common metadata (such as `CorrelationId` and `TenantId`) to the logger's ambient context.  
- Agents may still pass these values explicitly; duplication is not forbidden.

---

## 7. Environment, Noise Control, and DebugMode

### 7.1 DebugMode Rules

- `bool DebugMode { get; set; }` is exposed on the logger.  
- Agents must treat `DebugMode` as **read-only** and **must not modify it** in generated code.  
- The application host configures `DebugMode` (typically `true` in development, `false` in production).

Agents may conditionally emit additional diagnostic logs when `DebugMode` is true, but must never rely on `DebugMode` for functional correctness.

### 7.2 Noise Control

- `Trace` should be used for **meaningful diagnostics and progress**, not for every small step.  
- Avoid per-item logs inside large loops.  
- Do not spam logs for every retry or minor operation.  
- Prefer structured events (`AddCustomEvent`, `LogInvokeResult`) for important events.

### 7.3 Progress Tracing for Bulk Operations (Encouraged)

For bulk or long-running operations (for example, processing thousands of items):

1. It is encouraged to emit **batched progress** `Trace` messages (such as every 100 items or at logical checkpoints).  
2. Progress messages should include at least:
   - Items processed vs total  
   - Approximate percent complete  
   - Elapsed time  
   - Rough estimate of remaining time or completion window (for example, minutes vs hours)
3. Progress messages should be tagged with the standard tag format and may use a tag variant that includes `__Progress` for clarity.

These progress traces are allowed in production as long as they are batched and not per-item.

---

## 8. Security, Privacy, and Payload Guidance

### 8.1 Data That Must Never Be Logged

Agents must never log:

- Passwords or password hashes  
- API keys, access tokens, refresh tokens  
- Connection strings or secrets  
- Private keys or certificates  
- Credit card numbers or other PCI data  
- Authentication headers

This prohibition applies to all logging methods and all KVPs.

### 8.2 Identity and PII

- Logging internal identifiers (such as `UserId`, `TenantId`, `DeviceId`, `OrderId`) is generally acceptable.  
- Potential PII (for example, email address, phone number, full name, physical address) should be logged only when necessary and may be truncated or masked.  
- If in doubt, prefer omitting or partially masking PII.

### 8.3 Redaction Patterns

When context is needed but the value is sensitive, agents should:

- Replace the actual value with a placeholder (for example, `***`).  
- Or log a masked/partial version.

Key rule: secrets and highly sensitive values must never appear in logs.

### 8.4 Payload Size

Agents must avoid logging entire large payloads (for example, full JSON documents or file contents).

- If logging content is necessary, truncate to a bounded length and append `...`.  
- For binary content, log metadata only (such as size, type, and file name).

### 8.5 Exception Logging

When using `AddException`:

- Pass the exception object to capture stack trace and message.  
- Attach only safe metadata via KVPs.  
- Do not embed raw request bodies or configuration objects that may contain secrets.

### 8.6 Security & Payload Checklist

Agents generating logging code should verify:

1. No passwords, tokens, secrets, or connection strings are logged.  
2. Only appropriate identifiers (IDs) and limited PII are logged.  
3. Payloads are truncated or summarized when large.  
4. Exception logs do not expose secrets or large raw payloads.  
5. Tags and KVP keys follow SYS-003 conventions.

---

## 9. Persistence of Workflow & Integration with SYS-001

1. SYS-003 was authored following the SYS-001 workflow:  
   - Identifier assigned (`SYS-003`).  
   - 50K-foot summary produced.  
   - Bullets expanded and reviewed one by one.  
   - Explicit approval provided before bundle generation.
2. SYS-003 is the **canonical logging usage contract** for all agents and tools. Agents must not rely on the source definition of `IAdminLogger` or `ILogger`; this DDR provides the authoritative surface and conventions.
3. Logging usage must be consistent across all repositories and code types (services, tools, agents, console apps, background jobs) according to SYS-003.
4. The DDR file path is:

   `./ddrs/SYS-003 - Logger Usage and Profiling Standards DDR.md`

5. SYS-003 must be indexed into the global DDR/RAG system. If the indexed copy ever differs from the repository copy, this is considered DDR drift and requires manual review before automation continues.
6. All future logging-related specs, tools, and agents must build on SYS-003. Any new logging APIs or conventions must result in an update and re-approval of this DDR before agents may use them.
7. SYS-003 is a long-term governance artifact; Aptix and associated agents must remember and apply these rules automatically for all logging-related code generation and refactoring.
