# AGN-029 — Agent Turn-Level Contribution Providers & Injection Governance

**ID:** AGN-029  
**Title:** Agent Turn-Level Contribution Providers & Injection Governance  
**Status:** Approved  
**Type:** Policy / Rules / Governance  

## Summary

AGN-029 defines the provider-first governance model for **turn-level contribution providers** within the Aptix agent pipeline. It establishes authoritative, turn-scoped buckets for user input, client context, short-term memory (KFR), retrieved knowledge, and turn metadata, and defines how these contributions may be made available for inclusion in an LLM request. This DDR complements AGN-028 by governing *per-turn contributions only* and intentionally defers reasoner control flow, execution strategy, prioritization algorithms, and token budgeting concerns.

## Approval Metadata

- **Approved By:** Kevin D. Wolf  
- **Approval Timestamp:** 2025-12-22 ET (UTC−05:00)

---

## 1. Purpose & Scope

AGN-029 specifies governance rules for **turn-level contributions** supplied to an agent during a single request.

This DDR exists to:
- Establish explicit ownership for all turn-scoped artifacts
- Prevent ad-hoc or implicit turn injection
- Separate session identity (AGN-028) from per-turn state
- Ensure long-term maintainability and clarity

### In Scope
- Turn-scoped contribution providers
- Additive composition rules

### Out of Scope
- Request assembly or ordering algorithms
- Token budgeting, truncation, or eviction
- Reasoner control flow and looping

Session-level identity is assumed complete before this DDR applies.

---

## 2. Provider-First Framework (Turn Scope)

Turn-level contributions are governed using a **provider-first model**.

- Providers are the unit of responsibility
- Providers contribute artifacts intentionally
- Contributions are additive
- Providers do not override or suppress one another
- The request builder is a terminal assembler

Turn providers represent ephemeral, per-request state and must not redefine session identity.

---

## 3. User Turn Provider

### Purpose
Supplies what the user explicitly provided for the current turn.

### Contributes
- User instruction text
- User-explicit attachments (e.g., pasted text or images)

### Does Not Contribute
- Client-derived artifacts
- Memory, tools, or retrieved knowledge

### Governance Note
This provider is the sole authority for what the user said this turn.

---

## 4. Client Context Provider

### Purpose
Supplies environmental and structural context automatically provided by the client.

### Contributes
- Automatically attached workspace files
- Directory or project structure
- Editor or UI state

### Does Not Contribute
- User-authored input
- Memory or retrieved knowledge

### Examples (Non-Normative)
- Client auto-detects and sends relevant workspace files
- Client transmits directory structure for context

---

## 5. KFR (Short-Term Memory) Provider

### Purpose
Supplies short-term, mutable memory relevant to the current turn.

### Contributes
- Active Known Facts Registry (KFR) entries
- Temporary session facts

### Governance Note
Retention and eviction policies are out of scope.

---

## 6. RAG Provider (Optional)

### Purpose
Supplies retrieved, turn-scoped grounding content when retrieval occurs.

### Governance Clarification
Some external mechanism supplies retrieved content to this provider. Once supplied, the provider makes it available for inclusion in the request.

### Retrieval Source Neutrality
Retrieved content may originate from any mechanism, including tool execution. Origin does not affect governance.

This provider may be merged or removed without impacting other providers.

---

## 7. Tool Results & Continuation (Deferred)

Governance for tool outcomes and continuation artifacts is deferred pending reconciliation with Reasoner DDRs. This section may be reintroduced if these artifacts are determined to be apple-like contributors.

---

## 8. Turn Metadata Provider

### Purpose
Supplies optional metadata about the *shape or constraints of the current turn*.

### Contributes
- Notices about omitted, reduced, or unavailable context
- Other informational metadata relevant to interpretation

### Governance Note
This provider informs but does not instruct. Most turns will not use it.

---

## 9. Additive Composition Rules & Governance

### Additive Rule
All turn-level providers are additive. Absence implies no contribution.

### Isolation Rule
Providers must not inspect or modify other providers’ content.

### Session Boundary Rule
Turn providers must not redefine session identity (AGN-028).

### Prioritization & Ordering (Explicitly Deferred)
This DDR does not define a global prioritization algorithm.

Ordering emerges from:
1. The order in which providers are invoked
2. The order in which each provider supplies its own contributions

This deferral is intentional. Future prioritization must be defined via a successor DDR.

### Authority Statement
AGN-029 is authoritative for turn-level contribution governance.

---
