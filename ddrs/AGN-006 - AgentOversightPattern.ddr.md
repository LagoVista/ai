### AGN-006  
**Title:** Agent Oversight Pattern  
**Status:** Draft  

## Preamble
This decision record captures a high-level pattern for adding an independent oversight agent on top of the existing Aptix orchestrator and implementer agents. It is intentionally high level and will be refined later.

## Problem
We want stronger confidence that AI-driven changes to a large code base (and related specs) follow the agreed instructions, coding standards, and testing expectations, without relying solely on manual review.

## Forces
- Implementer agents can generate large amounts of code and tests quickly.
- Human reviewers focus on direction and 'smell' rather than every small detail.
- Most work already produces structured artifacts (DDRs, AI bundles, test runs, RAG metadata).
- There is diminishing return in adding infinite layers of review, but clear value in a single strong independent reviewer.

## Decision
Introduce a three-role pattern for future work:

1. Spec Agent – helps define machine-checkable instructions and acceptance criteria for a unit of work.
2. Implementer Agent – applies changes to the code base and specs, producing bundles, diffs, and tests.
3. Oversight Agent – independently verifies whether the Implementer Agent's work satisfies the instructions and reports any gaps.

The Oversight Agent is read-only with respect to source control. It never silently fixes issues; instead it produces a structured critique that can be acted on by humans or the Implementer Agent.

## Responsibilities

### Spec Agent
- Help humans turn informal goals into a structured instruction contract for a unit of work.
- Capture requirements as explicit items (e.g., code, tests, docs) that can be checked later.
- Provide evidence hints (paths, symbols, test names) that point to where each requirement should show up.

### Implementer Agent
- Consume the instruction contract plus relevant context (DDRs, RAG context, code).
- Produce an AI bundle / change set including:
  - Changed or new files.
  - Test artifacts (new tests, test commands, and results).
  - Any updates to specs / DDRs.
- Preserve enough metadata for traceability back to the instruction contract.

### Oversight Agent
- Consume the instruction contract, change bundle, and test results.
- For each requirement:
  - Locate evidence (files, symbols, tests, documentation).
  - Assign a status (Satisfied, PartiallySatisfied, NotSatisfied) with a short rationale.
- Run cross-cutting checks where possible (e.g., tests exist for new public methods, naming and folder conventions are followed).
- Produce a structured review result (e.g., JSON) with:
  - Overall verdict.
  - Per-requirement status.
  - Suggested corrective actions.

## Workflow (future target)
1. Human and Spec Agent define or update a DDR and instruction contract for a unit of work.
2. Implementer Agent executes the work and returns a bundle that references the instruction contract id.
3. Oversight Agent runs in a separate turn, using only read-style tools, and evaluates the bundle against the contract.
4. Human reviews the Oversight Agent's report and either:
   - Accepts the work, or
   - Sends the report back to the Implementer Agent as input for follow-up changes.

## Non-goals
- This pattern does not attempt to eliminate human approval.
- The Oversight Agent is not responsible for deep architectural decisions; it focuses on compliance with the agreed instructions and standards.
- This DDR does not specify concrete data contracts or orchestrator APIs; those will be defined in future AGN DDRs.

## Status and Next Steps
- Status: Draft, not yet implemented.
- Future work (separate DDRs):
  - Define the concrete instruction contract schema.
  - Define the oversight result schema.
  - Extend the orchestrator to support review turns and attach artifacts to turns.
  - Define minimal tool surface for the Oversight Agent (file read, diff inspect, test summary access).
