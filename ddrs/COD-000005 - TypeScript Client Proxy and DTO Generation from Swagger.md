# Metadata
ID: COD-000005
Title: TypeScript Client Proxy and DTO Generation from Swagger
Type: Generation
Summary: Defines how NuvOS generates TypeScript DTOs and client proxies from partitioned Swagger/OpenAPI documents, enforcing InvokeResult-based response envelopes and enabling shared consumption across Angular and React Native via a transport abstraction.
Status: Draft
Creator: Kevin D. Wolf
References: SYS-000010, SYS-001, SYS-004
Creation Date: 2026-01-23T00:00:00.000Z
Last Updated Date: 2026-01-23T00:00:00.000Z
Last Updated By: Kevin D. Wolf

# Body

## 1 - Overview
Summary: Establishes a generated “contract spine” for DTOs and client proxies to eliminate client/server drift, reduce hand-curation, and enable multi-client reuse (Angular + React Native) while keeping UI layers thin and error handling centralized.
NuvOS currently maintains hand-curated TypeScript DTOs and Angular service proxies that mostly map 1:1 to C# models and endpoints. Over time, older modules drifted and lack objective organization needed for reliable automation. Newer modules are tight but still hand-maintained.

This DDR defines a generation-based approach where Swagger/OpenAPI is the source of truth for DTO and proxy generation. Generated artifacts are consumed by multiple UI projects and future clients (including React Native), while preserving the “thin proxies / thin components” pattern by standardizing on InvokeResult envelopes.

## 2 - Decision
Summary: Adopt Swagger-partitioned generation of DTOs and client proxies into a dedicated generated module using ESM exports and import-path boundaries, with strict validation that all endpoints conform to InvokeResult envelope response types (or an explicit raw allowlist).
Decisions:
1. Source of truth: Server-side Swagger/OpenAPI partitioned by doc families (e.g., auth, business, devices).
2. Output: Generate TypeScript DTOs and client proxies per Swagger partition into a dedicated generated module (package-shaped), not hand-edited.
3. Imports: Prefer ESM exports + folder boundaries (e.g., `@nuvos/contracts-generated/auth`) over TypeScript namespaces.
4. Bedrock envelope: Enforce InvokeResult / InvokeResultEx / ListResponse / FormResult response shapes as required. If an endpoint response does not conform, generation fails and the server must be fixed (except explicitly allowlisted raw endpoints).
5. Multi-platform: Generated clients are framework-agnostic and depend on a transport abstraction; Angular and React Native provide separate transport implementations.
6. Migration: Legacy client code may remain temporarily, but all new endpoints and migrations must use the generated contract spine.

## 3 - Goals
Summary: Specifies what success looks like for generation, portability, and maintainability.
Goals:
- Eliminate long-term drift between client DTOs/proxies and server models/endpoints.
- Make generation deterministic, repeatable, and diagnosable.
- Keep UI layers thin by centralizing common error/redirect handling in the bedrock transport/client.
- Support multiple front ends (Angular web + React Native) using the same generated clients and DTOs.
- Enable incremental migration: legacy code remains functional while generated contracts become the default.
- Preserve a path to future NPM publishing using stable `@nuvos/*` import specifiers (via TS path aliases during churn).

## 4 - Non-Goals
Summary: Clarifies what this DDR intentionally does not attempt to solve.
Non-goals:
- Rewriting all existing legacy Angular components immediately.
- Implementing UI facelift work or new primitive components (separate effort).
- Defining domain workflows (e.g., accounting/bookkeeping) beyond the contract layer.
- Requiring RxJS-based generated APIs as a dependency (Promise-based default for portability).

## 5 - Contract Envelope Requirements
Summary: Defines the required response envelope patterns that enable centralized client-side error handling and thin UI layers.
All generated endpoints MUST return one of the following envelope shapes:
- `Core.InvokeResult`
- `Core.InvokeResultEx<T>`
- `Core.ListResponse<T>`
- `Core.FormResult<TModel, TView>`

If an endpoint response schema is not one of the above, the generation process MUST fail (see Chapter 9). The server-side endpoint must be updated to conform, unless the endpoint is explicitly allowlisted as a raw response exception.

## 6 - Swagger Partitioning and Output Structure
Summary: Defines how Swagger/OpenAPI documents are partitioned and how generated artifacts are organized and exported.
Partitioning:
- Generation boundaries are based on Swagger/OpenAPI doc families (e.g., `auth`, `business`, `devices`, `messaging`, `sys`).

Output structure (package-shaped):
- A dedicated generated module is produced (e.g., `@nuvos/contracts-generated`).
- Each partition is emitted into its own folder with stable entrypoints:
  - `auth/models.ts`
  - `auth/client.ts`
  - `auth/index.ts`
  - `index.ts` (root barrel exports partition entrypoints)

Export style:
- Use ESM exports.
- Prefer import-path scoping for name collision avoidance rather than long TypeScript namespaces.
- Recommended consumer pattern:
  - `import { AuthClient } from '@nuvos/contracts-generated/auth';`
  - `import * as AuthModels from '@nuvos/contracts-generated/auth/models';`

## 7 - Client Generation and Transport Abstraction
Summary: Defines how generated clients call the network without binding to Angular, enabling React Native reuse.
Generated clients MUST be framework-agnostic:
- Generated clients are plain TypeScript classes (no Angular decorators).
- Generated clients do not depend on Angular `HttpClient`, Angular DI, or browser-only APIs.

Transport abstraction:
- Generated clients depend on a transport interface that exposes NuvOS-shaped methods aligned to envelope responses (InvokeResult-centric).
- Implementations:
  - Angular: an adapter that delegates to the existing Angular bedrock client (e.g., `NuviotClientService`) to preserve centralized error handling, wait cursor behavior, and 401 redirect handling.
  - React Native: a `fetch`-based transport implementation with equivalent envelope parsing and 401 behavior wired to a platform-appropriate handler.

Behavior responsibilities:
- Bedrock transport/client handles: 401 behavior, error translation, optional redirect URL handling, and consistent envelope-shaped failures.
- Generated clients handle: route/path + HTTP verb selection and typed DTO binding only.

## 8 - Import Specifiers and Packaging Strategy
Summary: Defines how imports remain stable during churn (git-submodule) and later transition cleanly to NPM.
During active churn:
- Generated artifacts may be consumed via git-submodule “pulled files” into Angular projects.
- TypeScript path aliases SHOULD be used to enable stable `@nuvos/*` imports without requiring NPM publishing.

Future:
- The generated module may be published as an NPM package without changing consumer import statements.

Guidance:
- Avoid relative imports from consuming apps into the generated module.
- Prefer `@nuvos/contracts-generated/*` style imports everywhere.

## 9 - Generation Validation and Hard-Fail Rules
Summary: Defines the strict validation behavior that prevents drift and forces contract correctness at the server.
Validation rules:
- For each endpoint discovered from Swagger/OpenAPI, determine the response shape kind:
  - `InvokeResult`, `InvokeResultEx`, `ListResponse`, `FormResult`, `RawAllowed`, or `Unsupported`.
- If any endpoint is `Unsupported`, generation MUST halt and emit diagnostics listing each offending endpoint (method + route + response schema) and return a non-zero exit.

Raw allowlist:
- A small allowlist MAY exist for truly raw endpoints (e.g., GUID generation utilities).
- Any non-enveloped endpoint MUST be explicitly listed; otherwise it is treated as unsupported.

Diagnostics outputs (minimum):
- `unsupported-endpoints-server.txt` (method, route, server response schema)
- `unsupported-endpoints-client.txt` (method, route, client expected shape)
- Optional: `contracts-report.json` with counts by partition and response shape.

## 10 - Legacy Coexistence and Migration Policy
Summary: Defines how old and new client layers coexist and how migration occurs safely without a big-bang rewrite.
Coexistence:
- Legacy DTOs/services may remain temporarily to support existing screens.
- Generated contracts become the default for all new endpoints and new UI work.

Migration approach:
- Migrate incrementally by touch points:
  - When a feature area is modified, its DTO/service dependencies should be switched to generated clients.
- Where needed, legacy Angular services MAY act as thin wrappers over generated clients to preserve component call sites during transition.

Drift handling:
- If a legacy DTO differs from generated DTOs, the generated version is authoritative.
- Any required UI changes due to corrected DTO shapes should be treated as bug fixes (legacy drift correction).

## 11 - Expected Outputs
Summary: Lists the artifacts produced by implementing this DDR.
Expected outputs:
- A generated contracts module shaped for future NPM publishing and immediate submodule consumption.
- Partitioned DTOs and generated clients per Swagger doc family.
- A transport interface and a portable `fetch` transport implementation.
- An Angular adapter transport delegating to the existing bedrock client.
- Generation-time validation outputs and diagnostics for unsupported endpoints.

# Approval
Approver: 
Approval Timestamp: 
