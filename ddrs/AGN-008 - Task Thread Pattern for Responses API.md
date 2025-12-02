# AGN-008 — Task Thread Pattern for Responses API
**ID:** AGN-008  
**Title:** Task Thread Pattern for Responses API  
**Status:** Draft  
**Owner:** Kevin Wolf & Aptix  
**Scope:** Defines how Aptix uses the Responses API with `previous_response_id` to create isolated, short-lived task threads for focused work such as DDR reconciliation, tool-assisted workflows, and structured multi-phase reasoning.
---
## 1. Problem Statement
The Aptix ecosystem frequently requires structured, multi-step reasoning tasks that are too large or too specialized to run in the main user conversation. Examples include DDR consolidation, metadata refinement, deep tool-driven analysis, and complex reasoning workflows. These tasks must maintain temporary conversational state, use tools and RAG within a contained workspace, progress through multiple reasoning steps, produce a durable summary or artifact, then dispose of their internal history. The Responses API provides necessary primitives via `previous_response_id`, enabling creation of Task Threads: short-lived, goal-oriented sub-conversations operating independently of the main session.
---
## 2. High-Level Concept: Task Threads
A Task Thread is a lightweight, isolated reasoning context created for a specific goal, implemented using an initial Responses call, a sequence of continuations via `previous_response_id`, tool/RAG access, a final summary or artifact, and teardown when complete.
---
## 3. Characteristics of Task Threads
1. Isolated context using `previous_response_id`.  
2. Short-lived and focused.  
3. Phased execution (Start → Work → Summarize → Terminate).  
4. Ability to spawn subthreads for deeper tasks.  
5. Efficient via periodic summary resets to manage context size.  
6. Artifact-oriented: only outputs persist; conversation history is ephemeral.
---
## 4. Lifecycle
### Start Phase
A thread begins with an initial Responses call defining the goal, scope, and operational constraints.  
### Work Phase
Thread performs iterative reasoning, tool usage, and RAG retrieval as needed through continuation calls. May spawn additional task threads.  
### Summarize Phase
Thread produces a compact, durable summary of decisions, findings, or generated artifacts.  
### Terminate Phase
No further calls are made with that `previous_response_id`; thread memory is discarded.
---
## 5. Summary Reset Pattern
Threads periodically request a compact summary of the entire working context and then replace all prior history with that summary on the next call. This reduces latency, maintains clarity, and prevents runaway context growth.
---
## 6. Integration with DDR Workflows
Task Threads are ideal for DDR-intensive workflows: inventorying DDRs, multi-stage reconciliation, targeted analysis, or generating new specs. Each task thread handles a narrow goal and delivers results back into the SYS-001 DDR lifecycle without polluting the long-term conversation.
---
## 7. Persistence Rules
Only final outputs (DDR drafts, consolidation plans, structured JSON outcomes) are persisted. All intermediate reasoning remains transient. Each completed thread may produce a ThreadOutcome object containing a short summary and any relevant artifacts.
---
## 8. Relationship to SYS-001
Task Threads operate *inside* the SYS-001 DDR governance model. They support DDR drafting, analysis, and validation, but all DDRs must still follow SYS-001's mandatory workflow: 50K-foot review, bullet-by-bullet refinement, approval with timestamp, and bundling.
---
## 9. Future Extensions
Future DDRs may define: unified ThreadOutcome schema, orchestration patterns for multi-thread workflows, TTL policies for thread cleanup, and integration with tool routing and agent oversight systems.