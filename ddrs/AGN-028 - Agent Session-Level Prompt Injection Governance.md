# AGN-028 — Agent Session-Level Prompt Injection Governance

**ID:** AGN-028  
**Title:** Agent Session-Level Prompt Injection Governance  
**Status:** Approved  
**Type:** Policy / Rules / Governance  

## Summary

AGN-028 defines the governance rules and provider responsibilities for **session-level prompt injection** within the Aptix agent pipeline. It establishes a provider-first model, authoritative roles for System Policy, AgentContext, and AgentMode, explicit lifecycle semantics for Instruction DDRs (persistent vs initialization), and a clear separation between session-stable identity injection and turn-level concerns. Turn-specific injection and token budgeting are intentionally out of scope.

## Approval Metadata

- **Approved By:** Kevin D. Wolf  
- **Approval Timestamp:** 2025-12-22 16:41:00 ET (UTC−05:00)

---

## 1. Purpose & Scope

AGN-028 formally specifies **session-level prompt injection governance** for Aptix agents.

It exists to:
- Prevent ad-hoc, scattered prompt injection logic
- Make prompt composition maintainable for decades
- Ensure every injected “session-level” artifact has an explicit responsible provider

This DDR governs **session-stable identity injection** only (policy + agent context + mode context).

Out of scope:
- Turn-level injection mechanics (user turn, RAG, tool outputs, etc.)
- Token budgeting, truncation, paging, or eviction strategies
- Request assembly algorithms and ordering
- Persistence, memory retention, or summarization strategies

---

## 2. Provider-First Framework

- **Providers are the unit of responsibility.**
- Each provider has clear scope/ownership and **contributes prompt artifacts intentionally**.
- Provider contributions are **additive**; providers do not override or suppress each other.
- The request builder is a **terminal assembler**. It does not invent/infer content; it assembles what providers have contributed.
- Content categories may be tracked, but **providers are authoritative**.

---

## 3. System Policy Provider

### Purpose
Supplies **non-negotiable constraints** that apply to every request.

### Scope / Source
- Burned-in platform policy and organizational overlays (authoritative sources)
- Always present

### Contributes
- Hard rules: prohibitions, required behaviors, compliance constraints

### Does Not Contribute
- Mode guidance, user content, session state, tools, or runtime artifacts

### Additive Rule
Always included; never overridden.

---

## 4. System Guidance Provider (AgentContext)

### Purpose
Supplies the **agent-wide baseline** for behavior, initialization, and tooling context across all sessions/modes under an AgentContext.

### Scope / Source
- Source of truth: **AgentContext**
- Always present (every request selects an AgentContext)

### Contributes (Agent-scoped artifacts)

#### A) Instruction DDR Contributions (Injected as System Instructions)
AgentContext selects **Instruction-type DDRs**, retrieves their **LLM instruction content**, and injects that content as **system instructions**.

Instruction DDRs supplied by AgentContext are classified into two lifecycle categories:

1) **Persistent Agent Instruction DDRs**
- Injected as system instructions **on every request**
- Define ongoing, agent-wide behavior and reasoning expectations
- Never conditional on session state or user input

2) **Initialization Agent Instruction DDRs**
- Injected as system instructions **once per session or conversation**
- Used exclusively for initialization/priming
- **Never re-injected** after initialization

#### B) Guidance Artifacts
- Agent Welcome Message
- Reference DDR Table of Contents

#### C) Tooling Artifacts (Agent-Scoped)
- Agent-scoped tool usage guidance
- Agent-scoped tool schemas

#### D) Bootstrap Configuration (Baseline Defaults)
- Baseline boot prompts
- Baseline model configuration defaults (model identity + core parameters)

### Additive Rule
All AgentContext contributions are additive and form the baseline layer.

### Governance Rule
AgentContext contribution categories are explicitly finite. Adding new categories requires revising this DDR.

---

## 5. Mode Guidance Provider (AgentMode)

### Purpose
Supplies mode-specific specialization layered on top of the AgentContext baseline.

### Scope / Source
- Source of truth: **AgentMode**
- Always present (exactly one AgentMode per request; default mode by convention)

### Contributes (Mode-scoped artifacts)

#### A) Instruction DDR Contributions (Injected as System Instructions)
AgentMode selects **Instruction-type DDRs**, retrieves their **LLM instruction content**, and injects that content as **system instructions**.

Instruction DDRs supplied by AgentMode are classified into two lifecycle categories:

1) **Persistent Mode Instruction DDRs**
- Injected as system instructions **on every request**
- Define ongoing, mode-specific reasoning and task behavior
- Additive with AgentContext persistent instructions

2) **Initialization Mode Instruction DDRs**
- Injected as system instructions **once per session or conversation**
- Used to specialize initialization behavior for the mode
- **Never re-injected** after initialization
- Layered on top of AgentContext initialization instructions

#### B) Guidance Artifacts
- Mode Welcome / Framing Message
- Mode Reference DDR Table of Contents

#### C) Tooling Artifacts (Mode-Scoped)
- Mode-specific tool usage guidance
- Mode-specific tool schemas

#### D) Bootstrap Configuration (Mode Deltas)
- Mode-specific boot prompts
- Mode-specific model configuration overrides

### Additive Rule
Mode contributions are always additive; they do not replace AgentContext contributions.

### Governance Rule
Mode contribution categories are explicitly finite. Adding new categories requires revising this DDR.

---

## 6. Governance Rules & Evolution Constraints

### Scope Enforcement
This DDR governs session-level identity injection only. Turn-level injection is deferred to future DDRs.

### Provider Authority
Providers are first-class authorities. Introducing new contributors without defining a provider is architectural drift.

### Instruction DDR Governance
- Only Instruction-type DDRs may contribute system instructions.
- Providers select DDRs; they do not author instruction text directly.
- Persistent vs Initialization semantics are intentional and must not be altered implicitly.

### Additive Composition Rule
All contributions are additive. Override behavior is prohibited unless explicitly defined by a future DDR.

### Stability Expectations
This DDR is structurally stable; frequent changes indicate a modeling failure.

### Evolution Rules
Revisions require a successor DDR or formal update under SYS-001, with explicit change notes and migration intent.

### Explicit Deferrals
Token budgeting/truncation, turn-level injection mechanics, ordering/assembly algorithms, persistence/memory strategies are out of scope.

### Final Authority Statement
AGN-028 is authoritative for session-level prompt injection governance and Instruction DDR lifecycle semantics.

---
