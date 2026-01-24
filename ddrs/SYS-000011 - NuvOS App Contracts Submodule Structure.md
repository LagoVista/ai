# Metadata
ID: SYS-000011
Title: NuvOS App Contracts Submodule Structure
Type: Policy / Rules / Governance
Summary: Defines the canonical directory structure and governance rules for the `nuvos-app-contracts` submodule and documents how to mount it into an Angular workspace as `/app/contracts` with stable import boundaries.
Status: Draft
Creator: Kevin D. Wolf
References: SYS-001, SYS-004, SYS-000010, COD-000005
Creation Date: 2026-01-23T00:00:00.000Z
Last Updated Date: 2026-01-23T00:00:00.000Z
Last Updated By: Kevin D. Wolf

# Body

## 1 - Overview
Summary: Locks down the long-lived “application boundary” contract repository layout so generated API clients/DTOs and runtime boundary interfaces remain discoverable, portable, and non-UI.
The `nuvos-app-contracts` repository exists to define the application boundary between NuvOS UI code and external systems: (1) the NuvOS server API surface (OpenAPI/Swagger) and (2) the host platform (browser/phone/framework runtime). This repo is consumed as a git submodule inside the Angular workspace so it behaves like first-class source code during active iteration.

## 2 - Decision
Summary: Mount `nuvos-app-contracts` into the Angular workspace at `/src/app/contracts`, using a two-tier internal layout of `runtime/` (hand-authored boundary contracts) and `generated/` (fully generated domain contracts).
Decisions:
1. Repo name: `nuvos-app-contracts`.
2. Angular mount point: `/app/contracts` (git submodule).
3. Repo internal top-level source folders:
   - `runtime/` — hand-authored, non-domain, application-boundary interfaces and utilities.
   - `generated/` — fully generated domain DTOs and API client proxies produced from partitioned OpenAPI.
4. The Angular `/app/ui` layer must not contain transport/platform boundary logic; it consumes contracts via imports only.
5. `ui-shared` is out of scope for this DDR.

## 3 - Directory Structure
Summary: Defines the required directory structure under `/src/app/contracts` and the allowed contents of each zone.
When mounted in the Angular workspace, the `nuvos-app-contracts` submodule MUST have this structure:

- `/src/app/contracts/`
  - `runtime/`
    - `transport/`
      - Transport interfaces and implementations that are not domain-specific.
      - Example: `nuviot-transport.ts`, `fetch-transport.ts`.
    - `hooks/`
      - Interfaces the host app implements (e.g., wait cursor, navigation, storage, logging), plus minimal shared helpers.
      - Must remain non-domain-specific.
    - `stream/`
      - Shared stream event types and helpers (e.g., `AgentStreamEvent`).
    - `index.ts`
      - Runtime barrel exports.
  - `generated/`
    - `{partition}/`
      - `models.ts` — generated DTO interfaces/types for the partition.
      - `client.ts` — generated client proxy for the partition.
      - `index.ts` — partition barrel.
    - `clients.ts` (optional)
      - Generated “TOC” factory (e.g., `createClients(transport)`) exporting all generated clients.
    - `index.ts`
      - Generated barrel exports.
  - `index.ts`
    - Root barrel exports that expose `runtime/*` and `generated/*` entrypoints.

Content rules:
- `runtime/` MUST NOT include domain buckets such as `Devices`, `Customers`, `Media`, etc. Cross-cutting identities like `Users` and `Orgs` MAY exist only if they are truly boundary-level and non-feature specific.
- `generated/` MUST be domain-specific and must only contain generated DTOs and generated API client proxies that call into the runtime transport. No UI logic.

## 4 - Governance Rules
Summary: Prevents this repo from becoming a second “services-shared” by strictly separating hand-authored runtime code from generated domain contracts.
Governance rules:
1. `generated/` is write-only by automation.
   - Humans MUST NOT hand-edit files under `generated/`.
   - Regeneration is authoritative.
2. `runtime/` is hand-authored and intentionally small.
   - Additions to `runtime/` MUST be boundary-level, non-UI, and non-domain-specific.
3. Platform-specific behavior is allowed only behind runtime interfaces.
   - Example: navigation, wait cursor, storage.
   - Generated clients MUST NOT contain platform branching.
4. Generated clients MUST depend only on:
   - DTOs in `generated/`
   - Transport/interface surfaces in `runtime/`
   - No Angular imports, no React imports, no browser globals.

## 5 - Submodule Installation
Summary: Documents the canonical steps to mount `nuvos-app-contracts` into the Angular workspace as `/src/app/contracts`.
The `nuvos-app-contracts` repository MUST be added to the Angular repo as a submodule mounted at:

- `src/app/contracts`

Steps (git):
1. From the Angular repo root:
   - `git submodule add https://github.com/nuviot/nuvos-app-contracts src/app/contracts`
2. Initialize and fetch:
   - `git submodule update --init --recursive`
3. When pulling updates later:
   - `git submodule update --remote --merge`

Operational guidance:
- The Angular workspace should treat `/app/contracts/**` as source files.
- Developers MUST run submodule init/update when cloning the repo.

## 6 - Import Boundaries
Summary: Defines stable import patterns for consuming runtime and generated artifacts from Angular and other clients.
During active churn, stable imports SHOULD be provided via TypeScript path aliases from the Angular workspace.

Recommended path alias prefix:
- `@nuvos/app-contracts/*`

Example consumer patterns:
- Generated partition client:
  - `import { MediaClient } from '@nuvos/app-contracts/generated/media';`
- Generated partition models (namespace-style usage):
  - `import * as MediaModels from '@nuvos/app-contracts/generated/media/models';`
- Runtime transport:
  - `import { NuviotTransport } from '@nuvos/app-contracts/runtime/transport';`

Consumers MUST NOT:
- Import from deep relative paths that escape the submodule boundary.
- Add UI components or UI styling dependencies into this repo.

## 7 - Expected Outputs
Summary: Defines the durable artifact produced by this DDR.
Expected outputs:
- One markdown DDR stored at `ddrs/SYS-000011 - NuvOS App Contracts Submodule Structure.md`.

# Approval
Approver:
Approval Timestamp:
