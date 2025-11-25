# IDX-041 – Controller Endpoint Description Chunks

**Status:** Accepted

## 1. Description
This DDR defines the **EndpointDescription** chunk format for HTTP controller endpoints in C#. Each chunk describes **one HTTP endpoint** (one controller action method) in a structured way:

- Identity (controller name, action name, route, HTTP verbs)
- Linkage to Manager handlers
- Human-readable summary and description
- Request shape (parameters and body)
- Response shape (status codes, payloads, wrappers)
- Authorization and tenancy semantics

These are **structured** chunks and do not contain the raw controller source code; raw controller code is modeled elsewhere (e.g., `ChunkFlavor = "Raw"`).

## 2. Scope

- `Kind = "SourceCode"`
- `SubKind = "Controller"`
- `ChunkFlavor = "EndpointDescription"`
- Language: C#
- Applies to methods detected as HTTP endpoints via attributes such as:
  - `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`, `[HttpPatch]` and derived variants

### 2.1 Chunk Cardinality

- Exactly **one** EndpointDescription chunk is produced per HTTP endpoint.

### 2.2 Shared Metadata

Each EndpointDescription chunk includes:

- `Kind = "SourceCode"`
- `SubKind = "Controller"`
- `SymbolType = "Endpoint"`
- `ChunkFlavor = "EndpointDescription"`
- `DocId` – shared with the controller source file (IDX-001)
- `PartIndex`, `PartTotal` – position in the file’s chunk sequence (IDX-019)
- `LineStart`, `LineEnd` – inclusive 1-based line range for the action method (IDX-020)
- `CharStart`, `CharEnd` – optional 0-based offsets (IDX-021)
- `PrimaryEntity` – main entity the endpoint operates on (Section 8)

## 3. Endpoint Identity

These fields answer “**Which endpoint is this?**”.

### 3.1 Identity Fields

Example shape:

```jsonc
{
  "ControllerName": "DeviceController",
  "ActionName": "GetDeviceAsync",
  "EndpointKey": "DeviceController.GetDevice",
  "RouteTemplate": "api/devices/{id}",
  "HttpMethods": ["GET"],
  "ApiVersion": "1.0",
  "Area": "DeviceManagement",
  "PrimaryEntity": "Device"
}
```

Required:

- **ControllerName** – controller class name (e.g., `DeviceController`).
- **ActionName** – method name (`GetDeviceAsync`).
- **EndpointKey** – stable identifier for the endpoint. Recommended pattern:
  - `"<ControllerName>.<ActionNameWithoutAsync>"`
- **RouteTemplate** – effective route template (controller-level + method-level combined).
- **HttpMethods** – array of HTTP verbs for this endpoint.

Optional:

- **ApiVersion** – version string from `[ApiVersion]` or similar.
- **Area** – logical area (from `[Area]` or naming conventions).
- **PrimaryEntity** – simple name of primary entity, described in Section 8.

### 3.2 Handler (Manager Linkage)

Controllers typically delegate to a Manager or similar service. We capture that under a `Handler` object:

```jsonc
"Handler": {
  "Interface": "IDeviceManager",
  "Method": "GetDeviceAsync",
  "Kind": "Manager"
}
```

Fields:

- **Interface** – DI-injected interface name (e.g., `IDeviceManager`).
- **Method** – name of handler method invoked by the endpoint.
- **Kind** – logical handler type; starts with `"Manager"` but can be extended later.

Detection strategy (high-level):

1. Inspect constructor-injected interfaces on the controller.
2. For each endpoint method, find the first meaningful call on these injected fields.
3. Use that interface/method pair as the `Handler`.

This links to Manager metadata (IDX-039) via `Handler.Interface` and the Manager’s `PrimaryInterface`/`ImplementedInterfaces`.

## 4. Summary & Description

These fields give a human-readable explanation of what the endpoint does.

Example:

```jsonc
{
  "Summary": "Gets a single device for the current organization.",
  "Description": "Returns a device with configuration, status, and ownership validation for the authenticated organization.",
  "Tags": ["Device", "Read", "OrgScoped"],
  "Notes": [
    "Uses IDeviceManager.GetDeviceAsync internally.",
    "Returns InvokeResult<Device> with standard error handling."
  ]
}
```

- **Summary** (required)
  - Usually from XML `<summary>` on the method.
  - If missing, can be synthesized from HTTP verb, route, action name, and `PrimaryEntity`.

- **Description** (optional)
  - More detailed explanation, ideally from `<remarks>` or adjacent comments. May be synthesized when absent.

- **Tags** (optional)
  - Free-form labels, often derived from Area, entity, HTTP method, tenancy, etc.

- **Notes** (optional)
  - Additional hints about implementation or behavior.

## 5. Request Shape

Describes **what the client sends**: non-body parameters and the optional request body.

### 5.1 Parameter Classification

Each parameter is classified as one of:

- `"Route"` – bound from route segments or `[FromRoute]`
- `"Query"` – `[FromQuery]` or scalar parameters on GET/DELETE by convention
- `"Header"` – `[FromHeader]`
- Request body – complex payload parameter (Section 5.3)
- Service/DI parameter – `[FromServices]` or DI-only types (these are **ignored** in the public request shape)

Heuristics (ordered):

1. `[FromRoute]` → `Route`
2. `[FromQuery]` → `Query`
3. `[FromHeader]` → `Header`
4. Name appears in `{}` of `RouteTemplate` → `Route`
5. Complex type on POST/PUT/PATCH and no body yet → Request body
6. Simple scalar (string, Guid, numeric, bool, DateTime, enum, etc.):
   - GET/DELETE → `Query`
   - POST/PUT/PATCH → `Query` unless `[FromBody]` is present
7. `[FromServices]` or DI kinds → ignored
8. Otherwise → `Unknown`

### 5.2 Parameters (Non-Body)

All non-body parameters are represented as:

```jsonc
"Parameters": [
  {
    "Name": "id",
    "Source": "Route",
    "Type": "Guid",
    "IsRequired": true,
    "IsCollection": false,
    "DefaultValue": null,
    "Description": "Unique device identifier."
  }
]
```

Per-parameter fields:

- **Name** – parameter name
- **Source** – `Route | Query | Header | Unknown`
- **Type** – type name (nullable types use the underlying type name)
- **IsRequired** – true for route params, non-nullable scalars without defaults, or explicitly `[Required]`
- **IsCollection** – true for collections (`List<T>`, `T[]`, etc.)
- **DefaultValue** – string representation when a default is present
- **Description** – optional, from XML `<param>` where available

### 5.3 RequestBody (Main Payload)

At most one logical request body is modeled per endpoint:

```jsonc
"RequestBody": {
  "ModelType": "Device",
  "IsCollection": false,
  "IsPrimitive": false,
  "ContentTypes": ["application/json"],
  "Description": "Device to create."
}
```

Fields:

- **ModelType** – underlying payload type name (e.g., `Device`, `CreateDeviceRequest`)
- **IsCollection** – true for `List<T>`, `T[]`, etc.
- **IsPrimitive** – true for scalar bodies (e.g., `string` or `Guid`)
- **ContentTypes** – effective content types (default `application/json`)
- **Description** – optional, from docs or synthesized

If there is no body, `RequestBody` is omitted in JSON (following IDX-012 null-omission rules).

## 6. Response Shape

Each endpoint has a `Responses` array with one entry per status code.

Example:

```jsonc
"Responses": [
  {
    "StatusCode": 201,
    "Description": "Created.",
    "ModelType": "Device",
    "IsCollection": false,
    "IsWrapped": true,
    "WrapperType": "InvokeResult<Device>",
    "ContentTypes": ["application/json"],
    "IsError": false
  },
  {
    "StatusCode": 400,
    "Description": "Validation error.",
    "ModelType": "InvokeResult",
    "IsCollection": false,
    "IsWrapped": false,
    "ContentTypes": ["application/json"],
    "IsError": true,
    "ErrorShape": "InvokeResult"
  }
]
```

Per-response fields:

- **StatusCode** – HTTP status (int)
- **Description** – optional, human description (from attributes or conventions)
- **ModelType** – logical payload model, unwrapped from framework results where possible
- **IsCollection** – whether payload is a list/array
- **IsWrapped** – whether a wrapper (e.g., `InvokeResult<T>`) is used
- **WrapperType** – wrapper type string
- **ContentTypes** – e.g., `application/json`
- **IsError** – typically true for 4xx/5xx
- **ErrorShape** – envelope type for error responses (e.g., `InvokeResult`)

## 7. Authorization & Access Control

Authorization is captured in an `Authorization` object:

```jsonc
"Authorization": {
  "RequiresAuthentication": true,
  "AllowAnonymous": false,
  "Roles": ["OrgAdmin"],
  "Policies": ["DeviceWrite"],
  "Scopes": [],
  "Tenancy": "OrgScoped"
}
```

Fields:

- **RequiresAuthentication** – true when authorization attributes demand auth
- **AllowAnonymous** – true when `[AllowAnonymous]` is present (in which case `RequiresAuthentication` must be false)
- **Roles** – roles from `[Authorize(Roles = "...")]`
- **Policies** – policies from `[Authorize(Policy = "...")]` or equivalent
- **Scopes** – OAuth-style scopes, reserved for future use
- **Tenancy** – high-level tenancy context, examples:
  - `OrgScoped`
  - `UserScoped`
  - `System`
  - `Public`

Tenancy is inferred from attributes, usage of `OrgEntityHeader`/`UserEntityHeader`, route patterns, and other conventions.

## 8. PrimaryEntity

`PrimaryEntity` describes the main entity the endpoint operates on (e.g., `Device`, `Customer`). Heuristics align with the broader system:

- From controller name (`DeviceController` → `Device`)
- From route segments (`api/devices` → `Device`)
- From request/response model types (entity or DTO types)
- From Manager metadata (IDX-039) via the `Handler` linkage

If no entity stands out, `PrimaryEntity` is omitted (null).

## 9. Ordering & PartIndex

For each controller source file (`DocId`):

- EndpointDescription chunks are ordered by controller and then by method declaration order.
- `PartIndex` / `PartTotal` follow this order across all chunks in that file.

### 9.1 Why Ordering Matters

- Stable `PartIndex` values when files do not change
- Predictable behavior when endpoints are added, removed, or reordered
- Simple navigation and reconstruction of controller context in RAG workflows

## 10. Rationale

- A single structured chunk per endpoint provides a clean unit for search, reasoning, client generation, and documentation.
- Handler linkage connects endpoints back to Managers and Models, tying into other DDRs (IDX-037, IDX-038, IDX-039, IDX-040).
- Explicit request/response and authorization structures permit static analysis, security review, and safer automation.
- The design is version-1: stable in shape, with heuristics that can be refined as the real code base is indexed.
