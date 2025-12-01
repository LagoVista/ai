# IDX-068 — Codebase Description & Finder Snippet Strategy

ID: IDX-068
Title: Codebase Description & Finder Snippet Strategy
Status: Approved
Owner: Kevin Wolf & Aptix
Scope: How source artifacts are turned into RAG-ready finder snippets and backing artifacts across the LagoVista ecosystem.

Approval Metadata:
- Approved By: Kevin Wolf
- Approval Timestamp: 2025-12-01 08:15:00 EST (UTC-05:00)

---

## 1. Purpose

This DDR defines how LagoVista source code and related artifacts are transformed into:

1) Finder Snippets: short, highly structured text used for embeddings and vector search.
2) Backing Artifacts: full-fidelity descriptions stored in the content repository and loaded only after retrieval.

Goals:
- Provide reliable hooks into the codebase and specs for agents.
- Keep finder snippets sparse, canonical, and high-signal for vector search.
- Keep detailed content in backing artifacts where the LLM reasons after retrieval.

IDX-068 builds on existing SubtypeKind and description infrastructure and specifies how each description type participates in RAG.

---

## 2. Core Concepts

### 2.1 SubtypeKind

Each file or symbol is classified into a CodeSubKind, for example:
- DomainDescription
- Ddr
- Model
- SummaryListModel
- Manager
- Repository
- Controller
- Service
- Interface
- ResourceFile
- Test
- Other

IDX-068 assumes SubtypeKind detection exists and defines how each relevant kind maps into:
- Exactly one Finder Snippet (or none), and
- One or more Backing Artifacts.

### 2.2 Finder Snippet vs Backing Artifact

Finder Snippet:
- Very short.
- Highly structured and canonical.
- Optimized for semantic search and embeddings.
- Contains identifiers and topology such as Domain, Kind, Artifact, PrimaryEntity, Purpose.
- Stored as a SummarySection or equivalent and embedded.

Backing Artifact:
- Full descriptive text, lists, code excerpts, URLs, and detailed narrative.
- Stored in the content repository.
- Linked from the Finder Snippet via payload fields such as DocId and SectionKey.
- Never used directly for embeddings.

The same description type may serve as a Backing Artifact even if it was previously used directly for embeddings.

### 2.3 Canonical Finder Snippet Header

All Finder Snippets share a common header pattern:

Domain: DomainName
DomainSummary: one sentence domain description

Kind: TypeName

Where:
- Domain clusters related artifacts, for example Business, Devices, AI Services, Platform.
- DomainSummary is a one sentence tagline to help semantic clustering. If missing, the indexer may generate it using a small LLM call.
- Kind identifies the snippet type such as Model, Manager, Repository, Endpoint, Service, Domain, DDR.

### 2.4 Artifact and PrimaryEntity

Most Finder Snippets also include:

Artifact: QualifiedName or SyntheticId
PrimaryEntity: EntityName (where applicable)

- Artifact is the concrete identifier such as class name, fully qualified type name, or a deterministic synthetic id.
- PrimaryEntity is the business entity the artifact is mainly about, such as Customer, Device, Agent Context.
- PrimaryEntity is required where it is meaningful such as for Model, Manager, Repository, and many Endpoints.

---

## 3. Global Rules

1) Each interesting unit produces one Finder Snippet:
   - One per Domain
   - One per DDR
   - One per Interface
   - One per Manager
   - One per Repository implementation plus synthetic EF entries
   - One per Entity Model
   - One per Endpoint method
   - One per Service

2) Some kinds produce no Finder Snippets (or are grouped coarsely) such as tests and many configuration artifacts.

3) Backbone fields Domain, Kind, Artifact, and PrimaryEntity must be consistently populated across snippet types.

4) Field ordering in snippet text is fixed:
   - Domain and DomainSummary
   - Kind
   - Artifact and PrimaryEntity and type specific fields
   - Purpose style line at the end

---

## 4. Description Types

### 4.1 DomainDescription

SubtypeKind: DomainDescription

Purpose:
Defines the high level domain or area (for example Business, Devices, AI Services, Platform).

Finder Snippet:

Domain: DomainName
DomainSummary: one sentence domain summary

Kind: Domain

Artifact: DomainName

Purpose: Describes the scope and responsibilities of the DomainName domain.

Backing Artifact:
Contains the full domain description text, including scope, responsibilities, and any narrative context or key entities.

---

### 4.2 DdrDescription (Design Decision Records)

SubtypeKind: Ddr

Purpose:
Represents individual DDRs such as SYS-001, IDX-066, and this document. These are governance and specification artifacts rather than code.

Finder Snippet:

Domain: Architecture
DomainSummary: Cross cutting design and governance specifications.

Kind: DDR

Artifact: DdrId
PrimaryEntity: OptionalEntityName

SectionPurpose: Short description of what this DDR governs

Purpose: Defines design and governance rules for SectionPurpose.

Notes:
- SectionPurpose or an equivalent one sentence summary must be present so the DDR can be retrieved by intent.
- If missing, the indexer may call an LLM to generate a one sentence summary.

Backing Artifact:
The full DDR markdown, including title, status, constraints, decisions, rationale, and detailed sections.

---

### 4.3 InterfaceDescription

SubtypeKind: Interface

Purpose:
Defines what operations exist for an entity or service. Interfaces act as the canonical contract layer.

Finder Snippet:

Domain: DomainName
DomainSummary: one sentence domain description

Kind: Interface

Artifact: InterfaceName
PrimaryEntity: EntityName (where applicable)
InterfaceRole: Manager, Repository, Service, or Other

InterfacePurpose: short contract summary
OperationsSummary:
- normalized operation phrase 1
- normalized operation phrase 2
- etc

Notes:
- OperationsSummary is a small list of verb rich phrases such as onboard customers or update customer details.
- These phrases are derived from method names and or summaries, optionally normalized via an LLM helper.

Backing Artifact:
Contains full interface details such as method signatures, XML summaries, parameters, return types, and relevant attributes.

---

### 4.4 ManagerDescription

SubtypeKind: Manager

Purpose:
Defines how an interface contract is implemented in code and which dependencies it coordinates.

Finder Snippet:

Domain: DomainName
DomainSummary: domain tagline

Kind: Manager

Artifact: ManagerClassName
PrimaryEntity: EntityName
ImplementsInterface: InterfaceName

ImplementationPurpose: short description of the manager implementation
ImplementationSummary: one or two sentences about key dependencies and orchestration

Notes:
- Manager snippets do not repeat the full operation list. That list lives on the interface snippet.

Backing Artifact:
Contains the full ManagerDescription, including dependencies such as repositories and services, important branching behavior, key method summaries, and error handling patterns.

---

### 4.5 RepositoryDescription (Explicit Repositories)

SubtypeKind: Repository

Purpose:
Defines where and how an entity is persisted when a concrete repository class exists.

Finder Snippet:

Domain: DomainName
DomainSummary: domain tagline

Kind: Repository

Artifact: RepositoryClassName
PrimaryEntity: EntityName
ImplementsInterface: RepositoryInterfaceName

StorageBackend: SqlServer, PostgreSql, CosmosDb, Redis, InMemory, ExternalApi, Mixed, or Unknown

RepositoryPurpose: one sentence description
PersistenceSummary: short phrase describing whether this is CRUD only or includes custom queries or projections

Backing Artifact:
Contains method level details, query shapes, filters, sorting, relationships to EF or external stores, and any important error handling or transaction patterns.

---

### 4.6 EfDataContext Repositories (Synthetic)

SubtypeKind: EfDataContext (new)

Purpose:
Covers entities persisted via Entity Framework DbContext where no explicit repository class exists. Each DbContext acts as a repository container and emits synthetic repositories per DbSet.

Detection:
- Any class inheriting from DbContext is classified as EfDataContext.
- DI registrations using AddDbContext must be scanned to ensure coverage.

Synthetic Finder Snippet per DbSet:

Domain: DomainName
DomainSummary: domain tagline

Kind: Repository

Artifact: EfContext:ContextName.DbSetName
PrimaryEntity: EntityName or DTO name
ImplementsInterface: RepositoryInterfaceName

StorageBackend: Sql

RepositoryPurpose: Provides EF based persistence for PrimaryEntity.
PersistenceSummary: CRUD operations via ContextName.DbSetName.

Guardrail:
- For every persisted entity, the indexer must verify either an explicit RepositoryDescription exists or a synthetic EF entry exists.
- For each DbContext registered via AddDbContext, synthetic repositories must be generated for all DbSet properties.
- Missing coverage must be logged as a high severity error and may cause the indexing run to fail depending on configuration.

Backing Artifact:
May reference DbContext configuration, DbSet definitions, and relevant EF configuration extracted from the code.

---

### 4.7 EntityModelDescription (Unified Model Finder Snippet)

Inputs:
- ModelStructureDescription (existing)
- ModelMetadataDescription (existing, if present)

Purpose:
Provides a single strong anchor per entity model for model related questions such as what is this entity, what are its properties, and how is it presented in the UI. Structure and UI metadata become aspects behind one unified Model finder snippet.

Finder Snippet (one per model):

Domain: DomainName
DomainSummary: domain tagline

Kind: Model

Artifact: QualifiedTypeName
PrimaryEntity: ModelName

Aspects: comma separated aspects such as Structure, UIMetadata

Purpose: Defines the PrimaryEntity entity, including its structural shape and any available UI metadata and interaction rules.

Notes:
- Aspects is derived from which backing descriptions exist.
  - Structure when ModelStructureDescription is present.
  - UIMetadata when ModelMetadataDescription is present.
- Only one snippet exists per entity model. Old per description model finder snippets are disabled in unified mode.

Backing Artifacts:
- Structure backing artifact from ModelStructureDescription, including properties, relationships, child objects, and identity rules.
- UI metadata backing artifact from ModelMetadataDescription, including field layout, validation rules, lookups, navigation routes, and other UI semantics.

---

### 4.8 EndpointDescription

Subtypes:
- Controller classes that expose HTTP endpoints via attributes such as HttpGet, HttpPost, HttpPut, HttpDelete or LagoVista equivalents.

Purpose:
Represents REST style API endpoints that expose operations on entities or domain capabilities.

Finder Snippet (one per endpoint method):

Domain: DomainName
DomainSummary: domain tagline

Kind: Endpoint

Artifact: ControllerName/MethodName
PrimaryEntity: EntityName (if reliably inferred)
HttpMethod: GET, POST, PUT, DELETE, or other supported verb
Route: normalized logical route such as /customers or /customers/{id}/activate

Purpose: short natural language description of the endpoint such as creates a new customer or returns invoice history.

Notes:
- Route should be normalized to remove noise such as api or version prefixes.
- PrimaryEntity is inferred from route patterns, DTO types, controller naming, and calls into managers. If inference is ambiguous, PrimaryEntity may be omitted.

Backing Artifact:
Contains full controller excerpts, DTO structures, request and response shapes, validation and authorization attributes, and relationships to managers and repositories.

---

### 4.9 ServiceDescription

SubtypeKind: Service

Purpose:
Captures cross cutting capabilities provided by service classes. Examples include notification delivery, audit logging, identity and access helpers, messaging, and integration with external systems.

Finder Snippet (one per service class):

Domain: DomainName or Platform
DomainSummary: domain or platform tagline

Kind: Service

Artifact: QualifiedTypeName
ServiceName: friendly service name

PrimaryCapability: short capability phrase

Purpose: Provides PrimaryCapability functionality within the DomainName domain.

Notes:
- PrimaryCapability is derived from public methods using verb object extraction and may be normalized using an LLM helper.
- Cross domain services, such as audit and settings, map to the Platform domain with a Platform domain summary describing cross cutting functionality.

Backing Artifact:
Contains public method lists and summaries, dependency chains, DI lifetime, external integration details, configuration usage, and important behavior notes.

---

### 4.10 Other Kinds (Tests, Config, Misc)

Tests, configuration files, exceptions, and other utility types generally do not produce primary Finder Snippets. They may still produce Backing Artifacts that are reachable through relationships or future tooling, but they are not considered primary retrieval anchors in IDX-068.

---

## 5. Non Goals

- IDX-068 does not define chunking granularity such as line based versus symbol based chunking.
- IDX-068 does not define how embeddings are written to the vector database or specific collection naming.
- IDX-068 does not define tool usage or agent behavior beyond the Finder Snippet and Backing Artifact contracts.

These topics are covered or will be covered in separate DDRs, for example indexing orchestration and agent design DDRs.

---

## 6. Status and Next Steps

Status: Approved

Next Steps:
1) Implement a Finder Snippet model in code that corresponds to this DDR and maps to SummarySection constructs.
2) Introduce a unified RAG indexing mode that uses EntityModelDescription as the canonical model snippet type.
3) Implement ManagerDescription, RepositoryDescription, and EfDataContext synthetic repository snippet builders.
4) Implement InterfaceDescription, EndpointDescription, and ServiceDescription snippet builders following this DDR.
5) Add guardrails to ensure that persisted entities, DbContexts, and entity models cannot silently skip snippet generation.
6) Use real world indexing runs and qualitative query tests to refine Purpose, PrimaryCapability, and Aspect fields as needed.

Once the behavior is validated on representative domains, this DDR should remain the authoritative reference for how codebase descriptions are transformed into Finder Snippets and Backing Artifacts for RAG across the LagoVista ecosystem.
