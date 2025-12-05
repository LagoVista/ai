# TUL-006 — Agent Workflow Registry Tool

**ID:** TUL-006  
**Title:** Agent Workflow Registry Tool  
**Status:** Approved  
**Owner:** Kevin Wolf & Aptix  

---

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-05 09:45:00 EST (UTC-05:00)

---

## 1. Purpose & Role

TUL-006 defines a lightweight, server-side tool that serves as the authoritative registry of all workflows an agent can perform.

Its responsibility is to let the LLM:
- Discover what workflows exist  
- Retrieve the instructions, rules, and required steps for any workflow  
- Understand the conditions under which each workflow should be used  
- Identify the user-facing phrases, intents, or triggers that signal the user is trying to start that workflow

The tool does not execute workflows itself; instead, it provides structured, machine-readable metadata that enables the LLM to choose and run workflows consistently.

A key function of TUL-006 is exposing, for each workflow:
- A short description  
- Preconditions and expected outputs  
- Required user confirmations  
- Tooling the workflow depends on  
- User intent patterns (keywords, phrases, or example queries) that help the LLM determine: “The user is asking to begin workflow X.”

This ensures the agent responds to the right types of requests with the correct workflow, without guessing, improvising, or inventing new flows.

---

## 2. What “Workflow” Means in This Context

In TUL-006, a workflow is a named, declarative instruction package that the agent sends to the LLM to tell it exactly how to accomplish a multi-step task.

The workflow does not run on the server; instead:

> The LLM is the driver.  
> The user provides direction.  
> The agent provides tools.  
> The workflow provides the LLM with the script.

Each workflow includes the precise text and procedural guidance that the LLM should follow when executing that task. This ensures the LLM does not invent steps, improvise behavior, or perform unsupported actions.

A workflow, as returned by TUL-006, must therefore include:

- **WorkflowId** — stable identifier  
- **Title & Description** — what the workflow accomplishes  
- **LLM Instruction Text** — the exact guidance the agent sends to the LLM that explains:
  - how to run the workflow  
  - what questions to ask the user  
  - what decisions to make  
  - how to move step-by-step through the task  
- **Required Inputs** — what the LLM must collect before continuing  
- **Permitted Tools** — explicit list of IAgentTool names the LLM is allowed to call during this workflow  
- **User Intent Patterns** — phrases that imply “The user is asking to begin this workflow”  
- **Completion Conditions** — how the LLM determines the workflow is finished  
- **Optional Follow-up Guidance** — what the LLM might suggest after completing the workflow  

The core purpose is to make workflows deterministic and LLM-executable: TUL-006 supplies the script, the LLM executes the script, and the agent watches and provides tools as needed.

---

## 3. How Workflows Are Recognized / Triggered by the LLM

The primary goal of TUL-006 is to ensure that the LLM reliably identifies when the user is requesting a workflow and knows exactly which workflow to start.

Workflow recognition relies on three layers of metadata returned by the tool.

### 3.1 User Intent Patterns (Primary Trigger)

Each workflow includes a small set of intent patterns — example phrases, keywords, or request structures — that tell the LLM:

> “When the user asks something that looks like this, you should consider initiating Workflow X.”

Examples:
- “Create a new DDR”  
- “Start an indexing run”  
- “Refine this model description”  
- “Generate code from this spec”

Patterns may include:
- direct commands  
- goal statements  
- questions implying the workflow  
- variants or synonyms  

The LLM uses semantic similarity and pattern matching to identify when the user’s message aligns with a workflow’s intents.

### 3.2 Workflow Eligibility Rules

Some workflows have conditions that must be satisfied before they can be used:

- The user must already be inside another workflow  
- A DDR must be approved first  
- A patch or asset must exist  
- The agent must have state from a prior step  

The LLM should check these rules before selecting a workflow.

Eligibility rules prevent misfires such as:

> User: “What time is it?”  
> LLM mistakenly trying to initiate “Create DDR” workflow.

### 3.3 Explicit Workflow Initiation by the User

Even if no intent patterns match, users can start a workflow explicitly:

- “Use workflow: create_ddr”  
- “Start the code-refinement workflow.”

An explicit workflow request always takes precedence over pattern-based inference.

### 3.4 LLM Selection Behavior

When a user message appears to match a workflow:

1. The LLM maps the request to one or more candidate workflows.  
2. The LLM selects the best match.  
3. The LLM confirms with the user (unless workflow metadata marks it as “auto-start”).  
4. The LLM requests the full workflow instructions from TUL-006.  
5. The LLM begins executing the workflow script provided.

The LLM must never invent a workflow that is not defined in TUL-006.

### 3.5 When the LLM Must Call TUL-006 Automatically

The LLM should invoke TUL-006 when:

- The user requests something that sounds like a multi-step process.  
- The user asks for help with a task the LLM expects to be workflow-governed.  
- The LLM is unsure which workflow applies.  
- The LLM believes the user has started a new task without completing the previous one.  
- The user explicitly asks for a list of capabilities (for example, “What can you do?”).

This ensures predictable, deterministic behavior and keeps the agent compliant with all workflow definitions.

---

## 4. What the Tool Returns to the LLM

TUL-006 provides a single, authoritative source of workflow metadata. The LLM calls this tool with one of a small number of operations, and the tool responds with structured JSON describing the workflows.

The tool returns two main categories of responses.

### 4.1 Workflow Catalog (List View)

Returned when the LLM asks for:

- “list all workflows”  
- “what can you do?”  
- situations where the LLM is unsure which workflow fits a user request  

The response includes, for each workflow:

- **WorkflowId** — stable identifier  
- **Title** — short human-readable name  
- **Description** — one- or two-sentence summary  
- **UserIntentPatterns** — examples of phrases that initiate it  

The purpose is to give the LLM enough context to choose a workflow but not the full execution script.

### 4.2 Workflow Manifest (Detail View)

Returned when the LLM asks for details on a specific workflow or when it is about to start executing one.

This is the full instruction package the agent delivers to the LLM. It contains everything the LLM needs to run the workflow deterministically.

Required manifest fields:

- **WorkflowId** — unique name  
- **Title**  
- **Description**  
- **UserIntentPatterns** — to help the LLM recognize mid-session triggers  
- **RequiredInputs** — what the LLM must ask the user for before proceeding  
- **InstructionText** — the exact text the agent sends to the LLM describing how to run the workflow, step-by-step  
- **PermittedTools** — explicit list of IAgentTool names the LLM may call while in this workflow  
- **CompletionCriteria** — how the LLM knows the workflow is complete  
- **FollowUpOptions** (optional) — recommended next steps or related workflows  
- **Preconditions** (optional) — when this workflow is allowed to run  
- **Notes** (optional) — additional constraints, warnings, or hints  

The purpose of the manifest is to give the LLM a deterministic script to follow.

### 4.3 Error / Guardrail Responses

If a workflow cannot be returned (invalid ID, blocked, incomplete, deprecated, or disabled), the tool returns an InvokeResult<string> error with a message describing the issue, such as:

- “Unknown workflow ‘xyz’.”  
- “Workflow ‘xyz’ is deprecated.”  
- “Workflow requires approval before use.”  

This ensures the LLM never proceeds with undefined workflows.

### 4.4 Consistency Requirement

Every workflow manifest returned must be:

- complete  
- authoritative  
- stable in structure (changes require updating the DDR)  
- free of ambiguity  

The LLM must not proceed unless the manifest satisfies these requirements.

### 4.5 Purpose of the Return Schema

The return schema exists to:

- tell the LLM exactly what workflows exist  
- provide a machine-executable script for those workflows  
- ensure that the LLM behaves predictably and never invents workflow logic  
- allow the agent to evolve workflows over time without retraining the model  

---

## 5. Tool Contract & IAgentTool Semantics

TUL-006 defines a single server-side tool that implements IAgentTool. Its behavior must be simple, predictable, and consistent with the conventions established by other tools (for example, CalculatorTool), while adding workflow-specific capabilities.

The tool must provide three core operations through its JSON arguments:

1. **list_workflows** — returns the workflow catalog.  
2. **get_workflow_manifest** — returns the full instruction package for one workflow.  
3. **match_workflow** (optional but recommended) — the LLM provides a user message, and the tool returns likely workflow candidates based on intent patterns.

All three operations share a single tool schema and a single tool name.

### 5.1 Required IAgentTool Members

The tool must include the following members:

- **Name** (string)  
  - Stable name used by the LLM and the agent registry.  
  - Must follow the same naming conventions as all other tools.  
  - Example: `agent_workflow_registry`.

- **IsToolFullyExecutedOnServer**  
  - Must be `true`.  
  - The LLM never executes this tool client-side; it only queries workflow metadata stored on the server.

- **ToolUsageMetadata**  
  - A concise instructional string describing how this tool is used.  
  - Example: “This tool provides workflow metadata to the LLM, including workflow lists, workflow manifests, and intent-matching assistance.”

- **GetSchema()**  
  - Returns the OpenAI-style function schema describing the supported operations and their argument structures.  
  - Ensures the LLM knows exactly how to invoke the tool.

- **ExecuteAsync(...)**  
  - Parses `argumentsJson`.  
  - Executes one of the supported operations.  
  - Returns an InvokeResult<string> containing:
    - workflow catalog JSON  
    - workflow manifest JSON  
    - or workflow match results  
  - Must validate:
    - missing operation  
    - unknown operation  
    - missing workflowId (when required)  
    - workflow not found  
    - manifest incomplete  

Errors must be returned using the standard `InvokeResult<string>.FromError(...)` pattern.

### 5.2 Required JSON Arguments Schema

The tool must accept a JSON object with:

- **operation** — `list_workflows | get_workflow_manifest | match_workflow`  
- **workflowId** — string (required when operation = `get_workflow_manifest`).  
- **userMessage** — string (required when operation = `match_workflow`).

Example:

```json
{
  "operation": "get_workflow_manifest",
  "workflowId": "create_ddr"
}
```

### 5.3 Required Output Shapes

All successful outputs must follow a stable structure:

- **List View**  
  Contains `workflows`: array of lightweight workflow rows.

- **Manifest View**  
  Contains `workflow`: full manifest object.

- **Match View**  
  Contains `matches`: array with `workflowId` and `matchScore`.

These output shapes are intentionally simple to maximize reliability and minimize LLM misinterpretation.

### 5.4 Prohibited Behaviors

The tool must not:

- attempt to run workflows  
- call other tools  
- infer or generate workflow instructions dynamically  
- return freeform text without structure  
- modify agent state  
- include instructions that cause the LLM to bypass TUL-006 metadata  

The tool’s sole job is to return metadata, not perform operations.

### 5.5 Stability & Extensibility

- Workflow metadata returned by this tool must remain stable unless explicitly changed under a DDR.  
- New workflows or updated workflows require renewing TUL-006’s data source but not the tool’s code.  
- The tool should be able to support future operations (for example, version history queries) without structural redesign.

---

## 6. Agent / LLM Interaction Patterns

TUL-006 establishes predictable interaction rules so the LLM always knows when to call the tool, how to interpret its responses, and how to execute workflows in a deterministic, user-guided manner.

### 6.1 When the LLM Should Call TUL-006

The LLM must call the Workflow Registry Tool when:

- The user asks for a task that appears multi-step.  
- The LLM detects that user intent matches a workflow’s intent pattern.  
- The LLM is unsure which workflow applies.  
- The user directly asks what the agent can do.  
- The user explicitly asks for a workflow by name.  
- The LLM is in the middle of a workflow and needs clarification.

### 6.2 How the LLM Should Initiate a Workflow

When the LLM identifies a likely workflow:

1. Confirm with the user unless the workflow is marked as “auto-start”:  
   - “It looks like you want to begin the Create DDR workflow – should I proceed?”  
2. Once confirmed, call TUL-006 using `get_workflow_manifest`.  
3. Read `InstructionText` fully, and then operate strictly within that guidance.  
4. Ask the user for any `RequiredInputs`.  
5. Begin executing the workflow step-by-step.

### 6.3 How the LLM Should Use Permitted Tools

Each workflow manifest specifies a list:

```json
"PermittedTools": ["agent_ddr_manager", "request_user_approval", "..."]
```

Rules for tool usage:

- The LLM must only call tools listed in the active workflow’s manifest.  
- If a necessary tool is not listed, the LLM must pause and ask the user for clarification.  
- The LLM must follow the tool invocation contract established by the tool’s schema.  
- The LLM must use tool results to advance the next step of the workflow script.

This ensures the LLM never improvises or calls tools outside the intended scope.

### 6.4 How the LLM Should Maintain Workflow Context

While executing a workflow:

- The LLM should treat the workflow as the current session context.  
- The LLM should not start another workflow unless:
  - the current workflow is explicitly completed,  
  - the user cancels or switches, or  
  - the workflow manifest specifies a transition rule.  

The LLM should always tell the user which workflow is active if confusion arises.

### 6.5 How the LLM Should Communicate with the User

The LLM must follow these communication rules:

- Explain what step comes next when appropriate.  
- Ask the user before making irreversible actions.  
- Reflect back the user’s choices and important data.  
- Respect workflow-specific confirmation rules.  
- Ask for missing information rather than assuming it.

The workflow script governs how the LLM interacts; these are baseline behaviors.

### 6.6 Workflow Completion Behavior

When completion criteria are met:

1. The LLM announces workflow completion.  
2. The LLM summarizes what was achieved.  
3. The LLM offers any `FollowUpOptions` defined in the manifest.  
4. Workflow context is cleared.  
5. The LLM returns to normal conversational mode.  

The LLM must not declare a workflow complete unless the manifest’s `CompletionCriteria` is satisfied.

### 6.7 Error Handling Behavior

If:

- a required input is missing,  
- a tool call fails,  
- workflow preconditions are invalid, or  
- data is inconsistent,  

the LLM must:

1. Inform the user.  
2. Request clarification or correction.  
3. Only cancel the workflow if the user indicates they want to stop or if the manifest requires cancellation.

### 6.8 Boot-Time Workflow Initialization

When a new LLM session begins, the agent must provide a boot message that includes:

1. A list of all supported workflows.  
2. Each workflow’s `WorkflowId`, title, and summary.  
3. Each workflow’s `UserIntentPatterns` (trigger words / phrases).  
4. A short instruction telling the LLM how to interpret these patterns.

This allows the LLM to:

- know immediately which workflows exist,  
- know the valid trigger phrases for each workflow,  
- know that it must not invent workflows outside this list,  
- know when to call TUL-006 to retrieve full workflow manifests, and  
- immediately classify user intent from the very first message.

The boot message must include language such as:

> “These are the workflows available to you. If user input matches these patterns, call the workflow registry tool (TUL-006) to get the workflow manifest.”

The boot message is the baseline capability contract between agent and LLM.

---

## 7. Safety, Scope, and Guardrails

TUL-006 must enforce strict safety and behavioral guarantees so the LLM executes workflows predictably, does not exceed authority, and never improvises new behaviors outside what the agent intends.

### 7.1 The LLM Must Never Invent Workflows

The LLM is prohibited from:

- creating new workflows not defined in TUL-006,  
- modifying steps of an existing workflow,  
- merging workflows, or  
- hallucinating capabilities that TUL-006 did not declare.  

If the LLM receives ambiguous user input, it must consult TUL-006 using `list_workflows` or `match_workflow`, not guess or proceed with its own interpretation.

### 7.2 Only Permitted Tools May Be Used During a Workflow

Workflow manifests include:

```json
"PermittedTools": ["tool_a", "tool_b", "..."]
```

Rules:

- The LLM must not call any tool not listed.  
- The LLM must not call the same tool in a mode not described in its schema.  
- The LLM must pause and ask the user if it thinks a tool outside the list is needed.  
- If the LLM attempts to violate this rule, the agent should treat it as a workflow error.

This ensures workflows remain deterministic and auditable.

### 7.3 User Approval Rules

Some workflows require explicit user approval for specific actions, such as:

- modifying code,  
- generating files,  
- publishing changes,  
- altering domain models, or  
- writing patches.  

If a workflow requires approval:

- It must explicitly state so in the manifest.  
- The LLM must call the `request_user_approval` tool before proceeding.  
- The LLM must not bypass this step for any reason.

### 7.4 Workflow Preconditions Must Be Enforced

If a workflow lists preconditions such as:

- approval status,  
- required existing objects,  
- agent state, or  
- prior steps completed,  

the LLM must stop if those preconditions are not satisfied.

If preconditions fail:

- The LLM explains the reason.  
- It asks the user whether to correct input, choose another workflow, or cancel.  
- It must not force execution past the invalid state.

### 7.5 Workflow Isolation

While in a workflow:

- The LLM must not begin another workflow unless:
  - the current workflow completes,  
  - the user cancels it, or  
  - the manifest explicitly allows a transition.  
- All tool calls must be interpreted within the context of the current workflow.  
- The LLM must preserve continuity across turns.

This prevents contamination of state and contradictory behavior.

### 7.6 Safe Handling of Errors

If a tool returns an error:

- The LLM must stop advancing the workflow.  
- Summarize the error to the user.  
- Ask the user whether to retry, correct data, or cancel.  
- The LLM must never silently continue as if the tool succeeded.

Errors must be treated as first-class workflow events.

### 7.7 No Destructive Actions Without Explicit Consent

If any workflow step could:

- delete,  
- overwrite,  
- replace,  
- publish, or  
- modify user assets,  

the manifest must declare this, and:

- The LLM must use `request_user_approval`.  
- The LLM must show the exact action to be taken.  
- The user’s response governs the next step.

### 7.8 Workflows May Be Marked as Experimental or Hidden

Workflows may be labeled:

- `visibility = public`  
- `visibility = hidden`  
- `visibility = experimental`  

Rules:

- The boot message must not include hidden workflows.  
- Hidden workflows must only activate on explicit workflow ID invocation.  
- Experimental workflows may include warnings requiring user confirmation.

### 7.9 LLM Must Respect Workflow Boundaries Even Against User Pressure

If the user asks the LLM to perform something outside the workflow’s rules:

- The LLM must decline.  
- Explain that the workflow prohibits the action.  
- Suggest an allowed alternative or a different workflow.

### 7.10 Workflow Metadata Is Authoritative and Immutable During Execution

Once the LLM loads a workflow manifest:

- It must treat the manifest as immutable.  
- Any change requires re-requesting the manifest.  
- The LLM must not reinterpret or modify instruction text.

---

## 8. Workflow Lifecycle & Governance

TUL-006 establishes the rules for how workflows are introduced, updated, deprecated, and managed across time. These rules ensure that workflows remain stable, auditable, and aligned with the overall Aptix development methodology (SYS-001).

### 8.1 Workflows Must Be Declared, Not Inferred

All workflows consumed by the LLM must be:

- explicitly declared,  
- stored in a server-managed data source, and  
- validated at startup.  

The agent must never dynamically synthesize workflows or allow the LLM to create new ones. Workflows come into existence only through DDR-governed definition.

### 8.2 All New Workflows Require a DDR

Introducing a new workflow requires:

1. A new or updated DDR describing the workflow at a high level.  
2. User review and approval.  
3. Updating the workflow registry’s data source.

Workflows cannot be added ad hoc.

### 8.3 Workflow Updates Also Require a DDR

Changes that require DDR involvement include:

- modifying workflow steps,  
- changing required inputs,  
- altering triggers or intent patterns,  
- adding or removing permitted tools,  
- tightening or loosening approval rules, or  
- changing completion conditions.  

Minor cosmetic label adjustments may be exempt, but functional changes always require DDR review.

### 8.4 Workflow Versioning

Each workflow must include a Version field in its metadata to support:

- backward compatibility,  
- debugging,  
- agent behavior audits, and  
- future RAG indexing of workflow histories.  

Version increments should follow a simple rule:

- Major — behavior changes.  
- Minor — non-breaking additions.  
- Patch — correction of metadata errors.

The LLM should always use the latest version unless explicitly instructed otherwise.

### 8.5 Workflow Deprecation Rules

A workflow may be marked:

- `status = active`  
- `status = deprecated`  
- `status = disabled`  

Rules:

- Deprecated workflows remain callable but include warnings in the manifest.  
- Disabled workflows must not be returned to the LLM as runnable options, except in match results with a note explaining why they cannot be run.  
- The boot message must not include disabled workflows.

### 8.6 Hidden / Internal Workflows

Some workflows may be internal or used for testing:

- Marked `visibility = hidden`.  
- Excluded from the boot message.  
- Only activated if the user explicitly names the workflow ID.

This supports specialized workflows without cluttering the public surface area.

### 8.7 Workflow Storage Backend Is Replaceable

TUL-006 assumes workflow metadata is stored in a registry. The actual backend may evolve over time, including:

- embedded JSON,  
- a database,  
- a configuration provider, or  
- a future RAG-driven workflow catalog.  

The tool must not depend on a specific storage implementation; it only returns the metadata.

### 8.8 Drift Detection Between Workflow Metadata and DDRs

If workflow definitions in the registry diverge from the authoritative DDRs:

- This must be treated as a critical governance error.  
- Automated workflows should halt.  
- Agent-side validation should fail fast.  
- A corrective action must be initiated by the human operator.

### 8.9 Workflow Removal Must Be Explicit

A workflow may only be removed when:

- It is marked `status = disabled` first.  
- A DDR authorizes removal.  
- The agent code and metadata are updated.  
- The change is captured in version history.

The LLM must never “lose” a workflow accidentally.

### 8.10 Extensibility Rules

The workflow system must support safe addition of:

- new fields,  
- optional metadata,  
- additional classification patterns,  
- future tool compatibility matrices, and  
- nested workflow references (in future DDRs).  

These extensions must not break existing workflows or implicitly alter behavior.

---

## 9. Out-of-Scope / Deferred Items

TUL-006 defines what workflows are, how they are discovered, and how the LLM should use them. However, several related concerns are intentionally excluded and must be addressed in future DDRs or implementation phases.

### 9.1 No Workflow Implementation Logic

TUL-006 does not specify:

- how workflows are executed internally,  
- how workflow steps interact with domain logic,  
- how the agent orchestrates complex multi-step operations, or  
- any implementation strategy for workflow state machines.  

Workflow execution is driven by the LLM following the manifest, not by this tool.

### 9.2 No Definitions for Tool Behavior

Other tools (for example, DDR Manager Tool, User Approval Tool, Code Patch Tools) have their own DDRs.

TUL-006 does not redefine:

- tool contracts,  
- tool behavior,  
- tool-side safety rules (beyond listing permitted tools), or  
- tool-side error semantics.  

The Workflow Registry simply names the tools a workflow is allowed to use.

### 9.3 No UI or Human-Facing Design Specification

TUL-006 does not govern:

- how workflows appear in UI,  
- how agents display workflows to users, or  
- documentation presentation format.  

Any human-facing UI or experience design belongs to UIX-series DDRs.

### 9.4 No Storage Format Standardization

TUL-006 intentionally avoids specifying:

- where workflow metadata is stored,  
- how the storage should be structured, or  
- how metadata is updated or deployed.  

Only the requirements for what metadata must be surfaced to the LLM are defined.

### 9.5 No Runtime Mutability

TUL-006 does not allow:

- workflows to be created dynamically,  
- workflows to be edited mid-session, or  
- live updates to workflow manifests.  

It also does not define mechanisms for hot-reload of workflow sets or version negotiation between agent and LLM. These belong to operational DDRs.

### 9.6 No Definitions for Audit Logging or Analytics

Out of scope:

- workflow usage tracking,  
- telemetry,  
- analytics,  
- audit trails, and  
- compliance reporting.  

These will require a governance or telemetry DDR in the future.

### 9.7 No Governance for Workflow Boot Messaging Format

Although TUL-006 requires workflows and triggers to be delivered in the boot message, it does not define:

- the exact JSON structure,  
- how multiple agent components contribute to the boot message, or  
- versioning of boot instruction formats.  

A future DDR will define the contract used to assemble and emit boot messages.

### 9.8 No Guarantees About Backward Compatibility

TUL-006 does not attempt to define:

- compatibility rules between workflow versions,  
- migration strategies, or  
- how older LLM sessions handle newer workflows.  

This will be addressed in a future versioning DDR.

### 9.9 No Expectations About Multi-Agent Workflow Delegation

TUL-006 does not specify:

- how agents coordinate when multiple agents collaborate in a workflow,  
- how workflows transfer between agent components, or  
- multi-step distributed orchestration.  

This is a possible future AGN-series DDR topic.

### 9.10 No Test Standards Defined

TUL-006 does not establish:

- how workflow manifests are tested,  
- coverage expectations, or  
- LLM-behavior validation frameworks.  

These belong to test-governance DDRs and implementation DDRs.

---

## 10. Workflow Authoring Tools (Shape, Inputs, Outputs)

To support LLM-assisted workflow creation and maintenance, the agent may expose dedicated authoring tools. These tools let the LLM draft, revise, and validate workflow metadata while ensuring that workflows remain structured, governed, safe, and compliant with this DDR.

TUL-006 does not define the storage mechanism, but it does define the tool interfaces the LLM may call.

### 10.1 Authoring Tool Overview

The agent may provide one or more server-executed IAgentTool implementations that enable:

1. Drafting a new workflow.  
2. Retrieving an existing workflow for editing.  
3. Updating workflow metadata fields.  
4. Validating workflows for completeness and correctness.  
5. Publishing finalized workflow definitions.  
6. Listing all workflows in the authoring registry.

LLM-driven authoring is strictly declarative: the LLM prepares structured metadata, and the tool validates and stores it, always under user direction.

### 10.2 Required Authoring Operations

An authoring tool should support the following operations:

- **draft_workflow** — creates an unpublished workflow draft.  
- **get_workflow** — retrieves an existing workflow (draft or published).  
- **update_workflow** — updates metadata of an existing workflow.  
- **validate_workflow** — validates a workflow’s completeness and correctness.  
- **publish_workflow** — moves a workflow from draft to active registry.  
- **list_authoring_workflows** — lists all workflows visible to the authoring system (drafts and published).

The specific tool name and storage details are implementation concerns, but the operations and their contracts must remain consistent.

#### draft_workflow

Inputs:

```json
{
  "operation": "draft_workflow",
  "workflowId": "example_id",
  "title": "string",
  "description": "string"
}
```

Outputs:

- A draft workflow with minimal required fields initialized.

#### get_workflow

Inputs:

```json
{
  "operation": "get_workflow",
  "workflowId": "example_id"
}
```

Outputs:

- The full workflow manifest (active or draft).

#### update_workflow

Inputs:

```json
{
  "operation": "update_workflow",
  "workflowId": "example_id",
  "fields": {
    "...": "..."
  }
}
```

Allowed `fields` keys include:

- Title  
- Description  
- UserIntentPatterns  
- RequiredInputs  
- InstructionText  
- PermittedTools  
- CompletionCriteria  
- FollowUpOptions  
- Preconditions  
- Version  
- Status  
- Visibility  

The tool must validate types, prevent removal of mandatory fields, and enforce constraints defined in this DDR.

#### validate_workflow

Inputs:

```json
{
  "operation": "validate_workflow",
  "workflowId": "example_id"
}
```

Outputs:

- Success or typed validation errors, including:
  - missing fields,  
  - invalid patterns,  
  - unrecognized tools, or  
  - inconsistent preconditions.

Validation is required before publication.

#### publish_workflow

Inputs:

```json
{
  "operation": "publish_workflow",
  "workflowId": "example_id"
}
```

Outputs:

- Success or a reason for rejection.

Publication requires all required fields to be present, validation to succeed, and workflow version to be appropriately incremented.

#### list_authoring_workflows

Inputs:

```json
{
  "operation": "list_authoring_workflows"
}
```

Outputs:

```json
{
  "ok": true,
  "workflows": [
    {
      "workflowId": "create_ddr",
      "title": "Create a New DDR",
      "status": "published",
      "version": "1.0.0",
      "visibility": "public"
    },
    {
      "workflowId": "refine_domain_model",
      "title": "Refine Domain Model",
      "status": "draft",
      "version": "0.3.0",
      "visibility": "hidden"
    }
  ]
}
```

Each row should include:

- workflowId  
- title  
- status (`draft`, `published`, `deprecated`, `disabled`)  
- version  
- visibility (for example, `public`, `hidden`, `experimental`).

The list is intentionally shallow; the LLM must call `get_workflow` for full detail.

### 10.3 Authoring Tool Output Structure

All authoring responses must follow a unified structure:

Success:

```json
{
  "ok": true,
  "workflow": { "...optional manifest..." },
  "workflows": [ "...optional list..." ],
  "messages": ["optional guidance messages"]
}
```

Error:

```json
{
  "ok": false,
  "errors": [
    { "field": "InstructionText", "message": "InstructionText is missing." }
  ]
}
```

The LLM must treat errors as authoritative and prompt the user accordingly.

### 10.4 LLM Behavior When Using Authoring Tools

The LLM must:

- Ask the user before drafting new workflows.  
- Confirm changes before applying updates.  
- Treat validation errors as guidance, not hard failure.  
- Use only structured updates via `update_workflow`.  
- Avoid constructing raw JSON outside the defined schemas.  

The LLM must not:

- bypass validation,  
- invent workflow IDs,  
- overwrite workflows without user confirmation,  
- modify published workflows without a formal user request, or  
- attempt to publish incomplete workflows.

### 10.5 Relationship to TUL-006

Authoring tools:

- Allow the LLM to manage workflows.  
- Are separate from the TUL-006 registry tool that reads workflows at runtime.  
- Must be listed in the `PermittedTools` for any workflow that involves authoring behavior.

TUL-006 establishes the registry and runtime contract; authoring tools allow LLM-assisted creation and maintenance of the workflows governed by this DDR.
