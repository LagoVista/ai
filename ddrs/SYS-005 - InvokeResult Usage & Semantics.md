# SYS-005 — InvokeResult Usage & Semantics

**ID:** SYS-005  
**Title:** InvokeResult Usage & Semantics  
**Status:** Approved

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-03 17:00:00 EST (UTC-05:00)

---

## 1. Audience & Scope — InvokeResult as the result contract

This DDR defines how agents and developers must use `InvokeResult` and `InvokeResult<T>` across the LagoVista / Aptix ecosystem.

- `InvokeResult` and `InvokeResult<T>` are the only sanctioned result types for operations that can fail.
- Public method signatures must return `InvokeResult` or `InvokeResult<T>` (or their `Task<...>` async forms) for such operations.
- Internal `ValidationResult` inheritance is an implementation detail; agents must not introduce or expose `ValidationResult` as a public result type.

Permitted signatures:
- `InvokeResult MethodName(...)`
- `InvokeResult<T> MethodName(...)`
- `Task<InvokeResult> MethodNameAsync(...)`
- `Task<InvokeResult<T>> MethodNameAsync(...)`

Pure helpers that cannot fail (simple mappers, formatters) may return plain types, but any meaningful operation that can fail must use the `InvokeResult` pattern.

---

## 2. Canonical success and failure semantics

### 2.1 Core rule

For both `InvokeResult` and `InvokeResult<T>`:

- Success: no errors are present.  
- Failure: one or more errors are present.  
- Warnings: may be present in either case and do not turn success into failure.

Agents must rely on the success indicator implicit in the absence or presence of errors and must not invent alternate success flags.

### 2.2 Constructing success

Commands (no payload):

- Use `InvokeResult`.  
- Prefer `InvokeResult.Success` or `new InvokeResult()`.

Queries (with payload):

- Use `InvokeResult<T>`.  
- Prefer `InvokeResult<T>.Create(result)` and overloads that accept timings or redirect URLs when appropriate.

### 2.3 Constructing failure

Expected failures (validation and business rules):

- Use `InvokeResult.FromError(...)`, `InvokeResult.FromErrors(...)`.  
- Use `InvokeResult<T>.FromError(...)`, `InvokeResult<T>.FromErrors(...)` for typed results.

Unexpected failures (exceptions and system issues):

- Use `InvokeResult.FromException(tag, ex, timings?)`.  
- Use `InvokeResult<T>.FromException(tag, ex)`.

These helpers attach reserved exception error codes (such as `EXC9999`, `EXC9998`, `EXC9997`) and capture message, stack trace, and context tag.

### 2.4 Payload semantics for InvokeResult<T>

- On success, callers may safely read `Result`.  
- On failure, callers must not rely on the value of `Result`.

Agents must always check success before using `Result` in consuming code or tests.

### 2.5 Warnings

Warnings represent non-fatal issues (fallbacks, partial processing, degraded paths). They must be preserved when propagating results but do not change success to failure. If the caller must treat the outcome as failed, that condition must appear in errors, not only in warnings.

### 2.6 Translating failures between types

When an upstream call fails and the current layer needs a different generic type, agents must preserve errors, warnings, and timings:

- From untyped to typed: `InvokeResult<TTarget>.FromInvokeResult(upstream)`  
- From one typed result to another: `upstreamResult.Transform<TTarget>()`

`Transform<T2>()` uses `FromInvokeResult(this.ToInvokeResult())` to translate failure state between generic types.

---

## 3. Service signature and flow patterns

### 3.1 Standard signatures

Synchronous:

- Commands: `public InvokeResult DoThing(...)`  
- Queries: `public InvokeResult<T> GetThing(...)`

Asynchronous:

- Commands: `public Task<InvokeResult> DoThingAsync(...)`  
- Queries: `public Task<InvokeResult<T>> GetThingAsync(...)`

Async methods must use the `Async` suffix and return `Task<InvokeResult>` or `Task<InvokeResult<T>>`.

### 3.2 Layering

All main layers speak `InvokeResult`:

- Domain services: core business operations.  
- Application and orchestrator services: workflows across multiple services.  
- Controller and API boundaries covered by this DDR: return `Task<InvokeResult>` or `Task<InvokeResult<T>>`.

Example controller boundary:

```csharp
public async Task<InvokeResult<UserDto>> Post(CreateUserRequest request)
{
    return await _appService.CreateUserAndNotifyAsync(request);
}
```

Any mapping to framework-specific types (such as `IActionResult`) is performed by adapter layers outside the scope of this DDR.

### 3.3 Fail-fast chaining

Typical pattern:

1. Validate inputs.  
2. Call upstream operation.  
3. If upstream fails, return its failure (possibly transformed) immediately.  
4. If upstream succeeds, continue.

Example:

```csharp
public async Task<InvokeResult<OrderDto>> PlaceOrderAsync(PlaceOrderRequest request)
{
    var validation = ValidateRequest(request);
    if (!validation.Successful)
    {
        return InvokeResult<OrderDto>.FromInvokeResult(validation);
    }

    var domainResult = await _orderDomain.PlaceOrderAsync(request);
    if (!domainResult.Successful)
    {
        return domainResult.Transform<OrderDto>();
    }

    var dto = _mapper.ToDto(domainResult.Result);
    return InvokeResult<OrderDto>.Create(dto, domainResult.Timings);
}
```

### 3.4 Combining multiple operations

For all-or-nothing workflows, each failing step returns immediately using `FromInvokeResult(...)` or `Transform<T2>()`.

For best-effort workflows, non-critical failures may be represented as warnings in a final successful result, or as errors when domain rules require failure.

### 3.5 Validation pattern

Validation helpers should:

- Return `InvokeResult`.  
- Use `AddUserError(...)` for user-facing validation and business rule errors.  
- Be called at the start of operations, with failures converted via `InvokeResult<T>.FromInvokeResult(...)` when needed.

### 3.6 Commands vs queries

- Commands (primary purpose is side effects): `InvokeResult` / `Task<InvokeResult>`.  
- Queries (primary purpose is returning data): `InvokeResult<T>` / `Task<InvokeResult<T>>`.

### 3.7 Async and timings

Async flows must await upstream operations before returning. When performance is important, use `TimingBuilder` and attach `ResultTiming` entries to final results, either via overloads that accept timings or by copying timing lists onto the result.

---

## 4. Error modeling and categories

### 4.1 Error planes

Errors fall into three conceptual planes:

1. Validation and user input errors.  
2. Business rule errors.  
3. System and infrastructure errors.

Agents must choose the appropriate plane for each error.

### 4.2 Validation and user-facing errors

Expected failures caused by malformed or incomplete input or violated constraints:

- Use `AddUserError(...)` for these.  
- Use `FromError(...)` and `FromErrors(...)` helpers when building simple failure results.  
- Use clear, actionable, user-facing messages.

### 4.3 Business rule errors

Expected failures where input is well-formed but the requested action is not allowed by domain rules (for example, user already active, invalid state transition). Use the same mechanisms as validation errors, but word messages to reflect business rules rather than malformed input.

### 4.4 System and infrastructure errors

Unexpected failures (exceptions, network problems, database issues, serialization failures, etc.) must be converted using `FromException(tag, ex, timings?)` or `InvokeResult<T>.FromException(tag, ex)`.

These helpers:

- Use reserved exception error codes.  
- Capture messages, stack traces, and context tags for diagnostics.

### 4.5 Error codes

Domains may define their own error codes, for example `USER_DUPLICATE_EMAIL`, `ORDER_INVALID_STATE`. Exception-related codes (`EXC9xxx`) are reserved for `FromException` paths. When codes are defined in domain DDRs, agents must use them and tests must assert them.

### 4.6 Aggregating errors

When composing multiple operations:

- Use `Concat(...)` to merge errors and warnings.  
- Use `FromInvokeResult(...)` and `Transform<T2>()` to preserve errors and timings while changing generic types.

### 4.7 Messages and details

- `Message` should be human-readable and suitable for UI or API consumers.  
- `Details` may contain deeper diagnostic information such as stack traces and tags.

Agents must avoid placing sensitive internal details into user-facing messages.

---

## 5. Exceptions vs InvokeResult

### 5.1 Exceptions are not for normal failures

Agents must not throw exceptions for:

- Invalid input.  
- Domain rule violations.  
- Expected not-found cases (unless they specifically use `RecordNotFoundException` as described below).  
- Expected integration failures.

These outcomes must be represented as `InvokeResult` failures.

### 5.2 When exceptions are acceptable

Exceptions are acceptable for:

- Catastrophic runtime errors.  
- Critical infrastructure problems.  
- Invariant violations that should never occur.  
- Cancellation and shutdown control flows.

Such exceptions must be caught at appropriate boundaries and converted into `InvokeResult` failures using `FromException(...)`.

### 5.3 Required catch boundaries

The following layers must catch exceptions and convert them to `InvokeResult` failures:

- Orchestrators and application services.  
- Controller and API logic that return `InvokeResult`.  
- Tool and agent boundaries.

Example pattern:

```csharp
public async Task<InvokeResult<Dto>> DoWorkAsync(Request request)
{
    try
    {
        var domainResult = await _domain.DoWorkAsync(request);
        if (!domainResult.Successful)
        {
            return domainResult.Transform<Dto>();
        }

        var dto = Map(domainResult.Result);
        return InvokeResult<Dto>.Create(dto, domainResult.Timings);
    }
    catch (Exception ex)
    {
        return InvokeResult<Dto>.FromException("DoWorkAsync", ex);
    }
}
```

### 5.4 Special case: RecordNotFoundException

`RecordNotFoundException` is a special, allowed exception for not-found semantics when a record is expected to exist and a central handler is responsible for uniform handling.

- It may be thrown by repositories or domain services that rely on central handling.  
- It must be handled by a top-level handler that converts it into an `InvokeResult` failure or an appropriate HTTP or tool-level not-found response.

Example top-level handler:

```csharp
public async Task<InvokeResult<UserDto>> GetUserAsync(string id)
{
    try
    {
        return await _appService.GetUserAsync(id);
    }
    catch (RecordNotFoundException ex)
    {
        return InvokeResult<UserDto>.FromError(ex.Message);
    }
    catch (Exception ex)
    {
        return InvokeResult<UserDto>.FromException("UserController.GetUser", ex);
    }
}
```

Agents must not invent additional exception types for expected not-found cases; they must either use `RecordNotFoundException` or return an appropriate `InvokeResult` failure.

### 5.5 Propagation and non-swallowing

Exceptions reaching a catch boundary must be either:

- Converted via `FromException(...)`, or  
- Re-thrown intentionally to a higher-level handler (for example, `RecordNotFoundException` from lower layers).

Agents must not swallow exceptions silently or convert them to vague errors that lose diagnostic detail.

---

## 6. Testing rules for InvokeResult

### 6.1 Principles

Tests must:

- Explicitly assert success or failure.  
- Assert payload values only when success is expected.  
- Assert errors, codes, and messages for expected failure cases.  
- Assert warnings and timings only when meaningful for the scenario.

### 6.2 Asserting success

Example:

```csharp
var result = await _service.DoThingAsync();

Assert.That(result.Successful, Is.True);
Assert.That(result.Errors, Is.Empty);
```

For typed results:

```csharp
Assert.That(result.Successful, Is.True);
Assert.That(result.Result, Is.Not.Null);
Assert.That(result.Result.Name, Is.EqualTo("ExpectedName"));
```

### 6.3 Asserting failure

Example:

```csharp
var result = await _service.CreateUserAsync(request);

Assert.That(result.Successful, Is.False);
Assert.That(result.Errors, Is.Not.Empty);
Assert.That(result.Errors[0].Message, Does.Contain("Email already exists"));
```

With codes when defined:

```csharp
Assert.That(result.Errors[0].ErrorCode, Is.EqualTo("USER_DUPLICATE_EMAIL"));
```

### 6.4 Warnings and timings

Warnings:

- Assert counts and messages only for scenarios where warnings are expected.

Timings:

- Assert presence and specific keys (for example `DB`, `LLM`) when required by domain DDRs.

### 6.5 Testing transforms and FromInvokeResult

For `Transform<T2>()`:

```csharp
var upstream = InvokeResult<Foo>.FromError("failure");
var transformed = upstream.Transform<Bar>();

Assert.That(transformed.Successful, Is.False);
Assert.That(transformed.Errors.Count, Is.EqualTo(1));
```

For `FromInvokeResult`:

```csharp
var upstream = InvokeResult.FromError("failure");
var typed = InvokeResult<MyDto>.FromInvokeResult(upstream);

Assert.That(typed.Successful, Is.False);
Assert.That(typed.Errors[0].Message, Does.Contain("failure"));
```

### 6.6 Testing exception handling

When a method converts exceptions using `FromException`:

```csharp
_mockRepo.Setup(r => r.LoadAsync(It.IsAny<string>()))
         .Throws(new Exception("boom"));

var result = await _service.DoSomethingAsync("123");

Assert.That(result.Successful, Is.False);
Assert.That(result.Errors.Any(e => e.ErrorCode == "EXC9998"), Is.True);
```

For `RecordNotFoundException`, lower-level tests may assert that it is thrown, while higher-level tests assert that it is converted into an `InvokeResult` not-found failure.

---

## 7. Extension rules for new domains

### 7.1 Allowed extensions

New domains may extend:

- Error codes (namespaces such as `USER_`, `ORDER_`, `DEVICE_`).  
- Error messages and canonical wording.  
- Timing keys used in `ResultTiming.Key`.  
- Validation and business rules encoded as `InvokeResult` errors.

These extensions plug into the existing `InvokeResult` structure and semantics.

### 7.2 Prohibited changes

Domains may not:

- Change the structure of `InvokeResult` or `InvokeResult<T>`.  
- Redefine success or failure (success always means no errors).  
- Override or repurpose exception error codes for non-exception scenarios.  
- Introduce competing top-level result wrapper types for the same kind of contract.

### 7.3 New operation guidelines

For each new operation, domains must:

- Decide whether it is a command (`InvokeResult`) or query (`InvokeResult<T>`).  
- Define expected validation and business failures and represent them as errors.  
- Define how system failures are handled and converted from exceptions.  
- Define not-found semantics, including when `RecordNotFoundException` should be used.  
- Define error codes, messages, and timing keys as needed.  
- Ensure tests follow the testing rules in this DDR.

### 7.4 Chaining across domains

When chaining operations from multiple domains, agents must:

- Use fail-fast patterns.  
- Preserve errors, warnings, and timings via `Concat(...)`, `FromInvokeResult(...)`, and `Transform<T2>()`.  
- Avoid discarding or obscuring upstream errors unless a domain DDR explicitly requires masking.

### 7.5 Interaction with SYS-001 and drift

All DDRs, including this one, must be stored under `./ddrs` in their repositories and follow the SYS-001 workflow for creation, review, and approval. DDRs are indexed globally for search and reasoning.

If the indexed content for SYS-005 ever diverges from the repository version, this is DDR drift and must be treated as a critical issue requiring manual review. Automated processes must not silently reconcile or override the DDR content.

SYS-005 is the authoritative specification for how `InvokeResult` and `InvokeResult<T>` are to be used, extended, and tested across the LagoVista / Aptix ecosystem.