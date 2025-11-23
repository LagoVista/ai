# IDX-042 – Interface Overview Chunks

**Status:** Accepted

## 1. Description
This DDR defines the **InterfaceOverview** chunk format for C# interfaces. Each InterfaceOverview chunk gives a contract-focused view of an interface:

- Identity and classification
- Relationship to a primary entity (if any)
- High-level role (Manager/Repository/Service/Other)
- Method surface (names, parameters, return types, async flag)
- Linkage to implementing classes and dependent controllers

InterfaceOverview chunks are intended to pair with:

- Manager chunks (IDX-039)
- Repository chunks (IDX-040)
- Controller EndpointDescription chunks (IDX-041)
- Model structure/metadata chunks (IDX-037 / IDX-038)

## 2. Scope

- `Kind = "SourceCode"`
- `SubKind = "Interface"`
- `SymbolType = "Interface"`
- `ChunkFlavor = "Overview"`
- Language: C#
- Applies to all C# `interface` declarations discovered by the indexer.

### 2.1 Chunk Cardinality

- Exactly one InterfaceOverview chunk per interface type.
- No per-method chunks in v1; methods are summarized inside the overview.

### 2.2 Shared Metadata

Each InterfaceOverview chunk includes:

- `Kind = "SourceCode"`
- `SubKind = "Interface"`
- `SymbolType = "Interface"`
- `ChunkFlavor = "Overview"`
- `DocId` – associated document ID (IDX-001)
- `PartIndex`, `PartTotal` – ordering within the file (IDX-019)
- `LineStart`, `LineEnd` – inclusive 1-based range for the interface in the file (IDX-020)
- `CharStart`, `CharEnd` – optional 0-based offsets (IDX-021)

InterfaceOverview chunks do **not** embed raw source; they are structured summaries.

## 3. Interface Identity & Classification

These fields answer “**What interface is this, and where does it sit architecturally?**”.

Example structure:

```jsonc
{
  "InterfaceName": "IDeviceManager",
  "Namespace": "LagoVista.IoT.DeviceAdmin.Managers",
  "FullName": "LagoVista.IoT.DeviceAdmin.Managers.IDeviceManager",
  "IsGeneric": false,
  "GenericArity": 0,
  "BaseInterfaces": [
    "System.IDisposable"
  ],
  "PrimaryEntity": "Device",
  "Role": "ManagerContract"
}
```

### 3.1 Identity Fields

- **InterfaceName** – simple type name (e.g., `IDeviceManager`).
- **Namespace** – fully qualified namespace.
- **FullName** – `Namespace + "." + InterfaceName`.
- **IsGeneric** – true if the interface is generic (`IRepository<T>`), otherwise false.
- **GenericArity** – number of generic type parameters.
- **BaseInterfaces** – list of fully qualified base interface names (optional).

### 3.2 PrimaryEntity

- **PrimaryEntity** – simple name of the primary entity the interface is concerned with, when applicable (e.g., `Device`).

Heuristics (aligned with the rest of the system):

1. Naming pattern – strip common prefixes/suffixes (`I`, `Manager`, `Repository`, `Service`) and match against known model names.
2. Method signatures – look for parameters and return types referencing known model types or DTOs.
3. Reuse values from Manager/Repository metadata that implement this interface, when available.

If no clear mapping can be found, `PrimaryEntity` is omitted.

### 3.3 Role Classification

- **Role** – coarse contract role for the interface. Typical values:
  - `ManagerContract`
  - `RepositoryContract`
  - `ServiceContract`
  - `OtherContract`

Heuristics:

- Interface names ending in `Manager` → `ManagerContract`
- Names ending in `Repository` → `RepositoryContract`
- Names ending in `Service` → `ServiceContract`
- Otherwise → `OtherContract` (or omitted)

Global chunk-level fields for `Domain`, `Layer`, and `Role` still apply; this `Role` field is specifically about the interface as a **contract**.

## 4. Method Summary

Methods are summarized in a `Methods` array that captures surface shape without full AST detail.

Example:

```jsonc
"Methods": [
  {
    "Name": "CreateDeviceAsync",
    "ReturnType": "Task<InvokeResult<Device>>",
    "IsAsync": true,
    "Parameters": [
      {
        "Name": "device",
        "Type": "Device",
        "IsOptional": false,
        "DefaultValue": null
      }
    ],
    "Summary": "Creates a new device."
  }
]
```

### 4.1 Method Fields

For each method:

- **Name** – method name (e.g., `CreateDeviceAsync`).
- **ReturnType** – raw C# return type string.
- **IsAsync** – true for `Task`/`Task<T>` returns, otherwise false.
- **Parameters** – array of parameter descriptors:
  - `Name` – parameter name
  - `Type` – type name
  - `IsOptional` – true when a default value is provided or syntax indicates optionality
  - `DefaultValue` – string representation of the default value, when present
- **Summary** – optional short description of the method, usually from XML documentation. When absent, it may be synthesized later by tooling or LLMs.

The intention is to provide enough information for navigation, reasoning, and contract analysis without recreating full implementation or UI concerns.

## 5. Usage & Linkage

InterfaceOverview describes how the interface fits into the rest of the system.

Example fields:

```jsonc
"ImplementedBy": [
  "LagoVista.IoT.DeviceAdmin.Managers.DeviceManager"
],
"UsedByControllers": [
  "DeviceController.CreateDevice",
  "DeviceController.GetDevice"
]
```

### 5.1 ImplementedBy

- **ImplementedBy** – array of full type names for classes implementing this interface.

Source of truth: Roslyn analysis where classes have this interface in their base list (e.g., `class DeviceManager : IDeviceManager`). This field is optional and may be omitted if implementation relationships are not yet computed.

### 5.2 UsedByControllers

- **UsedByControllers** – array of `EndpointKey` values (from IDX-041) for controller endpoints that depend on this interface via DI.

Population model:

- Controllers record `Handler.Interface` in their EndpointDescription.
- These can be reverse-joined to construct `UsedByControllers`.
- Field is optional and present only when such relationships have been calculated.

These linkages support queries like:

- "Which controllers rely on `IDeviceManager`?"
- "Which implementations satisfy `IUserManager`?"

## 6. Chunking & Ordering

For each file (`DocId`):

- There is one InterfaceOverview chunk per interface declaration.
- `LineStart` / `LineEnd` span the entire interface block, including all method signatures.
- `PartIndex` / `PartTotal` are assigned according to the global rule (IDX-019): walk the file in source order and increment as chunks are emitted.

No additional chunk flavors for interfaces are defined in this DDR. If a raw-interface chunk is introduced later, it will be specified in a separate DDR.

## 7. Rationale

- Interfaces define contracts that bind Managers, Repositories, and Controllers together.
- A single, compact InterfaceOverview chunk provides an architectural view of these contracts without drowning in implementation details.
- Linkage fields (`ImplementedBy`, `UsedByControllers`) create a simple graph of contracts ↔ implementations ↔ endpoints.
- This structure enables:
  - Contract-level navigation and refactoring
  - Reasoning over which endpoints depend on which interfaces
  - Aligning interface contracts with model and metadata DDRs (IDX-037 / IDX-038)

The design is intentionally lightweight and extensible: identity, method surfaces, and relationship hints, leaving detailed behavior to other specialized DDRs.
