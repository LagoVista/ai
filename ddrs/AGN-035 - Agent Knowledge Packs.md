# AGN-035 — Agent Knowledge Packs

**ID:** AGN-035  
**Title:** Agent Knowledge Packs  
**DDR Type:** Generation  
**Status:** Approved

## Approval Metadata
- **Approved By:** Kevin D. Wolf  
- **Approval Timestamp:** 2025-12-25 EST (UTC-05:00)

---

## 1. Purpose and Scope

### Purpose
Agent Knowledge Packs (AKPs) define a fast, deterministic, and reusable mechanism for assembling all session-relevant knowledge required by an agent into a known, stable structure. AKPs centralize knowledge assembly, reduce repeated traversal of agent configuration structures, and decouple knowledge preparation from prompt construction.

### Scope
This DDR defines:
- The conceptual model for Agent Knowledge Packs
- The AKP output contract
- Assembly rules across AgentContext, ConversationContext, and Mode
- A service responsible for efficient AKP creation

### Explicit Non-Scope
AKPs do not:
- Construct prompts or perform injection logic (see AGN-030)
- Execute tools
- Enforce security or entitlements (explicit V2)
- Mutate conversation state

---

## 2. Current System Context

### AgentContext Model
The system models agent behavior using:
- **AgentContext** (global agent behavior)
- **ConversationContexts** (role-oriented, owned by AgentContext)
- **Modes** (activity-oriented, owned by AgentContext and shared across ConversationContexts)

ConversationContexts and Modes are independent and composed at runtime.

### Lifecycle Triggers
Knowledge contributions are evaluated:
- On the initial turn
- On mode change

AgentContext and ConversationContext are immutable for a session. Mode may change.

### Knowledge Contribution Types
Each level (Agent, Conversation, Mode) may contribute:

**Bootstrap (initial + mode change):**
- Welcome Message
- Bootstrap Instructions
- Bootstrap Tools

**Every Turn:**
- Instructions
- Reference Notes
- Available Tools

All content is assembled, not authored, by AKPs.

### Tools
Tools are compiled and defined in code. They are referenced by `tool_name` and are not data-driven in V1.

### Downstream Consumption
AKPs are handed to the Prompt Knowledge Provider (PKP / AGN-030), which is responsible for prompt construction and LLM request assembly.

---

## 3. AKP Conceptual Model

An Agent Knowledge Pack is a materialized snapshot of all knowledge relevant to an agent execution context.

AKPs are:
- Assembled, not authored
- Deterministic given the same inputs
- Stable in structure but not immutable across turns
- Cheap to construct using cached inputs

AKPs are not prompts and do not perform rendering, injection, or execution.

### Content Lanes
AKPs expose two always-present knowledge lanes:
- **Session Knowledge** (longer-lived guidance)
- **Consumable Knowledge** (turn-scoped guidance)

AKPs may change between turns. They are not session-locked.

---

## 4. AKP Content Taxonomy

### Knowledge Kinds
AKPs group knowledge by semantic kind:
- **Instruction** — summarized DDR guidance
- **Reference** — lightweight DDR pointers
- **Tool** — available tools by `tool_name`

### Knowledge Items
Each item contains:
- Kind
- Identifier (DDR ID or tool_name)
- Resolved consumption content (for DDR-based kinds)

### Kind Catalog
Each AKP includes a Kind Catalog describing how each kind may be rendered:
- Title
- Begin Marker
- End Marker
- Instruction Line

This allows PKP to render generically without hardcoding kinds.

### Welcome Messages
AKPs surface optional plain-text welcome messages for:
- Agent
- Conversation
- Mode

Welcome messages are not part of knowledge lanes.

---

## 5. AKP Output Contract

### AgentKnowledgePack
An AKP contains:
- AgentContextId
- ConversationContextId
- Mode
- Welcome messages (Agent, Conversation, Mode)
- SessionKnowledge lane
- ConsumableKnowledge lane
- KindCatalog
- EnabledToolNames

### Knowledge Items
- Instruction / Reference items include resolved DDR consumption fields
- Tool items include tool_name (usage text optional)

Only DDR consumption fields are included. Full DDRs are never embedded.

---

## 6. Assembly Rules

### Inputs
AKP creation is parameterized by:
- OrgId
- AgentContextId
- ConversationContextId
- Mode

### Precedence
Baseline precedence is:
**Agent → Conversation → Mode**

### Deduplication
- Instructions / References deduped by DDR ID
- Tools deduped by tool_name

Higher-precedence entries win conflicts.

### Ordering
Ordering is deterministic based on traversal order and declaration order.

### DDR Resolution
DDR consumption fields are resolved during AKP creation, after deduplication.
PKP never performs DDR lookups.

---

## 7. DDR Reference and Retrieval Integration

Each DDR used by AKPs has a DDR Kind:
- **Instruction DDR** → `DetailDesignReview.AgentInstruction`
- **Reference DDR** → `DetailDesignReview.ReferentialSummary`

These are the only DDR fields ever used for prompt construction.

DDR resolution occurs inside AKP creation using OrgId + DDR ID. Resolution is cache-backed and fail-fast.

---

## 8. Caching and Performance Strategy

### Constraints
- Clustered environment
- No in-memory cache assumptions
- Shared CacheProvider available

### Strategy
V1 favors:
- Identifier-based AKP assembly
- Batched DDR consumption field hydration (multi-key fetch)
- Over-invalidation for correctness

### Invalidation
AKPs may be invalidated by:
- AgentContext updates (covers ConversationContexts and Modes)
- DDR updates (AgentInstruction / ReferentialSummary)

AKPs are evaluated per request and may change between turns.

---

## 9. Service Design

### Responsibility
The AKP service:
- Loads AgentContext tree
- Assembles and dedupes knowledge
- Resolves DDR consumption fields
- Produces a ready-to-render AKP

### Interface
`CreateAsync(orgId, agentContextId, conversationContextId, mode)` → `InvokeResult<AgentKnowledgePack>`

### Dependencies
- AgentContext provider
- DDR consumption provider
- Cache provider
- Tool schema provider (used indirectly by PKP)

### Non-Goals
The service does not:
- Render prompts
- Attach tool schemas
- Execute tools
- Enforce security

---

**End of AGN-035**