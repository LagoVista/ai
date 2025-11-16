# README.md — Aptix AI Execution System

Aptix is a developer-centric AI execution layer for your LagoVista ecosystem.
It provides:

- A modern CLI (`aptix`)
- Strongly typed C# APIs
- Backend agent execution pipeline
- Conversation-aware LLM interactions
- Deterministic RAG workflows
- Zero-secret-in-repo security model

Aptix gives your engineering team a reliable, testable, repeatable interface for building
AI-powered development workflows.

---------------------------------------------------------------------

PROJECT STRUCTURE

/Aptix-Solution/
│
├── apps/
│   └── LagoVista.AI.Aptix.Cli/
│       ├── Program.cs
│       ├── aptix.config.json
│
├── src/
│   ├── LagoVista.Core.AI/
│   │   ├── Interfaces/
│   │   │   └── IAgentExecutionClient.cs
│   │   ├── Models/
│   │   │   ├── AgentExecuteRequest.cs
│   │   │   ├── AgentExecuteResponse.cs
│   │   │   └── (other shared models)
│   │
│   ├── LagoVista.AI.AgentClient/
│   │   └── AgentExecutionClient.cs
│   │
│   ├── LagoVista.AI/
│   │   ├── Controllers/
│   │   │   └── AgentExecutionController.cs
│   │   ├── Services/
│   │   │   └── RagAnswerService.cs
│   │   ├── Interfaces/
│   │   │   └── IAgentExecutionService.cs
│   │   ├── Models/
│   │   │   ├── AgentContext.cs
│   │   │   ├── ConversationContext.cs
│   │   │   └── AgentContextSummary.cs
│   │
│   └── LagoVista.AI.Rag/
│       └── (RAG indexing & utilities)
│
└── tests/
    └── LagoVista.AI.Tests/

---------------------------------------------------------------------

APTIX CLI OVERVIEW

Commands:
- aptix ask "your question"
- aptix ping

Environment selection:
- --local → https://localhost:5001
- --dev   → https://dev-api.nuviot.com
- (none)  → https://api.nuviot.com

Authentication:
- --token <value>
- OR environment variable APTIX_AI_TOKEN
- No secrets in config files

Final header:
Authorization: APIToken <clientId>:<token>

Flags:
--clientid, --verbose, -v

---------------------------------------------------------------------

EXECUTION FLOW

aptix ask "question"
      ↓
CLI builds AgentExecuteRequest
      ↓
POST /api/ai/agent/execute
      ↓
AgentExecutionService
      ↓
RAG: embed → search → snippet selection
      ↓
LLM call
      ↓
AgentExecuteResponse
      ↓
CLI prints answer + sources

---------------------------------------------------------------------

CORE CONTRACTS (LagoVista.Core.AI)

IAgentExecutionClient:
- ExecuteAsync
- AskAsync
- EditAsync

AgentExecuteRequest:
- AgentContext (EntityHeader)
- ConversationContext (EntityHeader)
- Instruction
- ConversationId
- WorkspaceId, Repo, Language, RagScope
- Mode: ask/edit
- ActiveFiles

AgentExecuteResponse:
- Kind: success/error
- Text
- Sources[]
- ErrorMessage, ErrorCode

---------------------------------------------------------------------

SERVER COMPONENTS

AgentExecutionController:
- POST /api/ai/agent/execute
- GET /api/ai/agent/ping
- Uses APIToken scheme
- Passes OrgEntityHeader & UserEntityHeader

IAgentExecutionService:
- Core orchestration layer
- Validates & routes requests
- Calls RAG + LLM
- Builds final response

RagAnswerService:
- Embedding
- Qdrant retrieval
- Snippet packaging
- Prompt assembly
- LLM call (chat/completions)
- Error normalization

---------------------------------------------------------------------

CONTEXT MODELS

AgentContext:
- Vector DB settings
- Embedding model
- LLM key
- Default ConversationContext
- Azure/Qdrant configs
- Temperature
- Provider

ConversationContext:
- ModelName
- Temperature
- System prompt
- Defaults per agent

---------------------------------------------------------------------

LOGGING STANDARDS

Trace:
[Class_Method] <message text>
    correlationId=...
    mode=...
    agentContextId=...

Errors:
AddError("[Class_Method]", "Human readable message", kvp arguments...)

Rules:
- The first parameter = tag
- Second parameter = human-readable message (not structured)
- KVPs for IDs, enums, contextual data
- Keep parameters aligned & under 120 chars

---------------------------------------------------------------------

ROADMAP / NEXT STEPS

A. Implement AgentExecutionService.cs  (next step)
   - Resolve agent & conversation context
   - Validate request
   - Build RAG call
   - Handle LLM temperature constraints
   - Return structured AgentExecuteResponse

B. Temperature Validation + Normalization
   - Validate range per model
   - Prevent LLM 400 errors
   - Automatic fallback mapping

C. Edit Mode
   - Multi-file structured patches
   - Context-aware hunks
   - Undo/redo friendly structure

D. Patch Engine V1
   - Line-level and token-aware patching
   - Regex / fuzzy match safety
   - AST-aware (future)

E. Developer Standards Document
   - Logging patterns
   - Naming conventions
   - Prompt standards
   - File layout

Future Enhancement Ideas:
1) In our server side, code, if our AI ever asks for a chunk of code, automatically pull it.
2) Establish the idea of an APTIX session, this will be tied to time tracking and potentially ticketing

---------------------------------------------------------------------

NEW SESSION BOOTSTRAP GUIDE

To resume the project in a fresh ChatGPT session:

1. Paste this entire README into the model.
2. Say: “Restore Aptix project context.”
3. Then say: “Proceed to Step A: Implement AgentExecutionService.”

I will then restore all file references and continue immediately.

---------------------------------------------------------------------



END OF README
