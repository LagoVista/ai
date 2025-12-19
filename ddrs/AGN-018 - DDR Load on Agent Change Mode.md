# AGN-018 — DDR Load on Agent Change Mode

**ID:** AGN-018  
**Title:** DDR Load on Agent Change Mode  
**Status:** Approved

## Approval Metadata
- **Approved By:** Kevin D. Wolf  
- **Approval Timestamp:** 2025-12-19 13:00:00 EST (UTC-05:00)

---

## 1. Introduction

### 1.1 Purpose
AGN-018 defines the standard mechanism for priming an agent session when the agent mode changes. Its purpose is to ensure that, upon entering a new mode, the agent operates under the correct instructions, standards, constraints, and expectations as defined by existing Aptix DDRs.

This DDR formalizes how those DDRs are introduced into the session, not what they contain.

### 1.2 Relationship to Other DDRs
AGN-018 operates within the workflow defined by SYS-001 and does not replace or override any existing DDR content.

- SYS-* DDRs remain authoritative and persistent
- Mode-specific DDRs define behavior but are activated through this strategy
- AGN-018 defines the orchestration layer, not the behavioral rules themselves

This DDR is intentionally complementary and non-invasive.

### 1.3 Scope & Non-Goals
**In scope:**
- Agent mode transitions
- Deterministic injection of DDR-defined instructions
- Behavioral priming of an existing session

**Explicitly out of scope:**
- Tool changes
- DDR authoring rules
- Storage, retrieval, or indexing mechanisms
- Prompt formatting specifics
- RAG query mechanics

AGN-018 assumes DDRs already exist and are valid.

---

## 2. Conceptual Model

### 2.1 Agent Mode
An agent mode represents a distinct behavioral context that governs how the agent reasons, responds, and applies constraints. A mode does not define capabilities; it defines expectations and standards of operation.

Modes are declarative in intent and are realized through DDR-defined instructions.

### 2.2 Role of DDRs
DDRs are the authoritative source of truth for agent behavior within a mode. They encode:
- Operating rules
- Formatting and response standards
- Domain or workflow constraints
- Safety and consistency guarantees

AGN-018 treats DDRs as immutable inputs. It does not reinterpret or modify them.

### 2.3 Session Priming vs Session Reset
A mode change primes the session rather than resetting it.

- Priming: Introducing or reinforcing instructions relevant to the new mode
- Not a reset: Prior conversational state, memory, and context remain intact unless explicitly altered by other mechanisms

This distinction ensures continuity while allowing the agent’s behavior to shift deterministically.

---

## 3. DDR Injection Strategy

### 3.1 Meaning of “DDR Injection”
Injecting a DDR means making the DDR’s instructions actively effective within the agent session so that they influence reasoning and response behavior for the current mode.

Injection is a behavioral operation, not a persistence or retrieval concern.

### 3.2 Injection Timing
DDR injection occurs immediately after an agent mode change is detected and before the agent constructs its next response under the new mode.

This guarantees that the first response in the new mode reflects the correct standards.

### 3.3 Ordering Guarantees
Injected DDRs must be applied in a deterministic order.

At a minimum:
1. Persistent system-level DDRs
2. Mode-relevant DDRs

The exact ordering algorithm is an implementation detail, but the result must be stable and reproducible.

### 3.4 Idempotency
DDR injection must be idempotent.

Re-injecting the same DDR set for the same mode must not duplicate instructions, compound constraints, or alter effective behavior.

---

## 4. Service Contract: DDR Injection Provider

### 4.1 Responsibility
The DDR Injection Provider is responsible for deterministically injecting mode-relevant DDR instructions into the agent session when operating under a specific agent mode.

### 4.2 Inputs
The provider must accept:
- Agent Mode Identifier
- Ordered List of DDR Identifiers
- Invocation context indicating a Responses API request is about to be issued

DDR selection logic is explicitly out of scope.

### 4.3 Outputs
The provider must produce:
- Effective instruction text suitable for the Responses API `instructions` parameter

Optional but recommended:
- Instruction bundle identifier or hash for observability

### 4.4 Injection Rules
The provider must enforce:
1. Per-request injection via the `instructions` parameter
2. Deterministic output for identical inputs
3. Idempotency across repeated injections
4. Isolation from conversation history

### 4.5 Ordering Guarantees
System-level DDRs must precede mode-specific DDRs in a stable order.

### 4.6 Non-Responsibilities
The provider does not:
- Select DDRs
- Summarize or reinterpret DDRs
- Manage storage or RAG
- Modify session history
- Enforce instruction size constraints

### 4.7 Failure Behavior
If effective instruction text cannot be produced, the agent must fail fast. Silent degradation is not permitted.

---

## 5. Operational Guarantees

### 5.1 Determinism
Injected instructions must be identical for the same mode and DDR set.

### 5.2 Instruction Freshness
Critical behavioral rules must be replayed on every request and must not rely on conversation history.

### 5.3 Isolation from Conversation State
Mode priming must not reset or mutate conversation state.

### 5.4 Observability
Active mode and instruction bundle identifiers should be logged per request.

### 5.5 Fail-Safe Behavior
If correct instruction injection cannot be guaranteed, the agent must not generate a response.

---

## 6. Summary

AGN-018 establishes a deterministic and reliable strategy for priming agent behavior during agent mode execution.

When a mode is active:
- Its DDRs are guaranteed to be present
- Instructions are replayed per request via the Responses API
- Behavioral clarity is preserved regardless of session length

AGN-018 deliberately defers DDR summarization, storage, and retrieval concerns to future specifications.

Downstream systems may rely on this DDR as a behavioral contract: if a mode is active, its rules are fresh, authoritative, and enforced for every response.
