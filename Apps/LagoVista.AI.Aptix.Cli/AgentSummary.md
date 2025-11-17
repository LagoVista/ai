# Aptix Agent Execution & Session Architecture Spec

## 1. High-Level Flow & Components (DD-001)

**DD-001 — Layered Entry & Orchestration**

1. **REST Controllers (Entry Point Only)**
   - All clients (Browser, CLI, Thick client) call into REST controllers.
   - Controllers are intentionally thin:
     - Auth / basic validation / logging.
     - Map HTTP → client-specific DTO.
     - Delegate to `AIExecutionRequestHandler`.
   - No RAG, no sessions, no OpenAI logic here.

2. **AIExecutionRequestHandler (Client Adapter)**
   - Knows the three calling shapes:
     - Browser-style
     - Thin CLI
     - Thick client
   - Normalizes each into **one canonical orchestrator input**, conceptually:
     - Agent context identifiers (AgentContextId, ConversationContextId).
     - Instruction text.
     - Active file metadata.
     - Optional WorkspaceId / Repo / language hints.
   - After this point, the orchestrator does not care which client type called it.

3. **Agent Orchestrator (Core Brain)**
   - Accepts the **same input shape** for all client types.
   - Responsibilities:
     - Resolve `AgentContext` and `ConversationContext`.
     - Resolve or create `AgentSession`.
     - Create a new `AgentSessionTurn` for each human instruction.
     - Compute `ActiveFileRefs` (hashes, size, touched, etc.).
     - Query Vector DB (Qdrant) using workspace/project filters.
     - Retrieve and package chunks → `ChunkRefs`.
     - Persist full instruction/answer payloads to blob storage.
     - Persist session/turn metadata via `IAgentSessionManager`.
     - Call OpenAI **Responses API** (with `previous_response_id` when available).
     - Map OpenAI result into:
       - Stored history (turns, blobs, summaries).
       - A live `AgentExecuteResponse` for the client.

---

## 2. Session & Turn Data Model (DD-002A/B/C)

### 2.1 AgentSession (Durable Context)

**DD-002A — AgentSession Core**

- `AgentSession : EntityBase`
  - Inherits standard platform fields:
    - `Id`, `Name`, `Key`, `Description`, `IsPublic`, `OwnerOrganization`, `OwnerUser`,
      `CreationDate`, `LastUpdatedDate`, `IsDeleted`, `IsDraft`, `Category`, ratings, etc.
  - Session-specific fields:
    - `AgentContextId` — ID of the agent context used.
    - `ConversationContextId` — ID of the conversation context used.
    - `WorkspaceId` — logical workspace identifier for this session (e.g. `NuvOS.DesignPlayground.KevinsLaptop1`).
    - `Repo` — repository identifier used when querying RAG.
    - `DefaultLanguage` — default language hint for RAG (e.g. `csharp`).
    - `List<AgentSessionTurn> Turns` — ordered sequence of turns in the session.

> **Note:** `AgentSession` is the durable “thread” concept, similar to a ChatGPT conversation, owned by an org/user.

---

### 2.2 AgentSessionTurn (Per-Instruction History)

**DD-002B — AgentSessionTurn Core**

Each human instruction and its LLM answer maps to an `AgentSessionTurn`.

- Identity & ordering:
  - `Id` — string ID for the turn.
  - `SequenceNumber` — monotonically increasing integer per session, defines order.

- Ownership & time:
  - `CreatedByUser` — `EntityHeader` of the user who submitted the instruction.
  - `CreationDate` — ISO-8601 UTC string; when the turn was created/accepted.
  - `OpenAIResponseReceivedDate` — ISO-8601 UTC; when OpenAI responded (success or failure).
  - `OpenAIChainExpiresDate` — ISO-8601 UTC; last reliable date to use `previous_response_id` for this turn’s chain.
  - `StatusTimeStamp` — ISO-8601 UTC; last status change (Pending → Completed/Failed).

- Lifecycle status:
  - `Status : EntityHeader<AgentSessionTurnStatuses>`
    - Enum values:
      - `Pending` — instruction captured, LLM not yet completed.
      - `Completed` — LLM answer stored successfully.
      - `Failed` — LLM or pipeline failed irrecoverably for this turn.
    - Backed by label constants on `AgentSessionTurn`:
      - `AgentSessionTurnStatuses_Pending = "pending"`
      - `AgentSessionTurnStatuses_Completed = "completed"`
      - `AgentSessionTurnStatuses_Failed = "failed"`
    - Default: `Pending` at turn creation.

- Mode & IDs:
  - `Mode` — e.g. `"ask"` or `"edit"`.
  - `ConversationId` — logical conversation ID used to group related calls.
  - `OpenAIResponseId` — `id` returned by the OpenAI Responses API for this turn.
  - `PreviousOpenAIResponseId` — the `id` used as `previous_response_id` for this call (if any).
  - `OpenAIModel` — OpenAI model name used (e.g. `gpt-5`).

- Instruction:
  - `InstructionSummary` — first ~1024 chars or similar, human-readable.
  - `FullInstructionUrl` — blob URL to full instruction payload.

- Answer:
  - `AgentAnswerSummary` — first ~1024 chars of the LLM answer, only for Completed turns.
  - `FullAgentAnswerUrl` — blob URL to full LLM answer text.

- OpenAI raw payloads:
  - `OpenAIRequestPayloadUrl` — optional blob URL to raw request payload sent to OpenAI.
  - `OpenAIResponsePayloadUrl` — optional blob URL to raw response payload from OpenAI.

- Context references:
  - `List<AgentSessionChunkRef> ChunkRefs`
    - Each entry:
      - `ChunkId` — vector DB chunk ID.
      - `Path` — source path.
      - `StartLine`, `EndLine` — line range for the chunk.
      - `ContentHash` — hash of the chunk text (post-chunking).
  - `List<AgentSessionActiveFileRef> ActiveFileRefs`
    - Each entry:
      - `Path` — file path.
      - `ContentHash` — hash at the moment of call.
      - `SizeBytes` — size of the file.
      - `IsTouched` — true if file is modified locally.
      - `WasSentToLLM` — true if full file or excerpt was included in the request.
      - `WasTooLargeToSend` — true if file exceeded size limits and could not be sent.

- Diagnostics:
  - `List<string> Warnings` — warnings for this turn.
  - `List<string> Errors` — errors for this turn (for failed turns).

---

### 2.3 Turn Lifecycle Rules

**DD-002A — AddTurn (Pending)**

When a new instruction is submitted:

- Orchestrator:
  - Collects instruction text and context (active files, RAG chunk refs).
  - Stores full instruction payload to blob (`FullInstructionUrl`).
  - Builds `InstructionSummary` (truncate to max length).
  - Creates a new `AgentSessionTurn` with:
    - New `Id`.
    - `SequenceNumber = lastSequenceNumber + 1`.
    - `CreatedByUser`, `CreationDate`.
    - `Status = Pending`.
    - `StatusTimeStamp = CreationDate`.
    - Mode, ConversationId, OpenAIModel (if known), chunk/file refs, etc.
  - Calls `AddAgentSessionTurnAsync`.

- Manager must:
  - Ensure `SequenceNumber` is unique and monotonic per session.
  - Ensure `Status` is `Pending` on creation.
  - Persist the new turn inside the session.

---

**DD-002B — CompleteTurn (Success)**

On successful OpenAI call:

- Orchestrator:
  - Has the full LLM answer text and raw OpenAI response.
  - Stores full answer to blob (`FullAgentAnswerUrl`).
  - Stores raw response payload if desired (`OpenAIResponsePayloadUrl`).
  - Builds `AgentAnswerSummary` (truncate).
  - Determines:
    - `OpenAIResponseId` (from OpenAI).
    - `OpenAIResponseReceivedDate` (UTC).
    - `OpenAIChainExpiresDate` (UTC; optional).
  - Calls `CompleteAgentSessionTurnAsync` with:
    - `agentSessionId`, `turnId`.
    - `agentAnswer`, `openAiResponseId`.
    - Warnings.
    - `org`, `user`.
    - (Implementation fills in the extra metadata as per design.)

- Manager must:
  - Locate the turn and ensure `Status == Pending`.
  - Set:
    - `Status = Completed`.
    - `StatusTimeStamp = OpenAIResponseReceivedDate`.
    - `OpenAIResponseId`.
    - `OpenAIResponseReceivedDate`.
    - `OpenAIChainExpiresDate` (if provided).
    - `AgentAnswerSummary`, `FullAgentAnswerUrl`.
    - `OpenAIResponsePayloadUrl` if used.
    - Append any warnings.
  - Persist updated session/turn.
  - Never mutate a Completed turn again.

---

**DD-002C — FailTurn (Failure)**

On failure (OpenAI error or pipeline failure):

- Orchestrator:
  - Captures:
    - `OpenAIResponseId` if present.
    - Raw error payload URL if stored.
    - List of human-readable `errors`.
    - List of `warnings` (optional).
    - `OpenAIResponseReceivedDate` (or “failure observed” timestamp).
    - Optional `OpenAIChainExpiresDate`.
  - Calls `FailAgentSessionTurnAsync` with:
    - `agentSessionId`, `turnId`.
    - `openAiResponseId`.
    - `errors`, `warnings`.
    - `org`, `user`.

- Manager must:
  - Locate the turn and ensure `Status == Pending`.
  - Set:
    - `Status = Failed`.
    - `StatusTimeStamp = OpenAIResponseReceivedDate` (or now if not provided).
    - `OpenAIResponseId`.
    - `OpenAIResponseReceivedDate`.
    - `OpenAIChainExpiresDate` (optional).
    - Append `errors`, `warnings`.
    - `AgentAnswerSummary = null`.
    - `FullAgentAnswerUrl = null`.
  - Persist updated session/turn.
  - Never mutate a Failed turn again.

---

## 3. Session Listing & Reading Models (DD-003, DD-007)

### 3.1 AgentSessionSummary

**DD-003 — Session Summaries for UI**

`AgentSessionSummary : SummaryData`

- Inherits from `SummaryData` (Id, Name, Key, Description, IsPublic, Category, LastUpdatedDate, ratings, etc.).
- Adds session-specific fields (v1 intent):
  - `AgentContextId` — raw ID.
  - `ConversationContextId` — raw ID.
  - `AgentContextName` — human-readable name (resolved by orchestrator or metadata layer).
  - `ConversationContextName` — human-readable name.
  - `LastTurnStatus` — text representation of the last turn’s status.
  - `LastTurnDate` — timestamp of the last turn’s `StatusTimeStamp` or `CreationDate`.
  - `TurnCount` — number of turns in the session.

> `AgentSessionSummary` is used to power list views, “My sessions”, etc.

---

### 3.2 ConversationContext Summary Propagation

**DD-007 — ConversationContext in Summaries**

- `AgentSession` persists:
  - `AgentContextId`
  - `ConversationContextId`
- These are **opaque IDs** to session persistence.
- Orchestrator / metadata layer is responsible for resolving:
  - `AgentContextName`
  - `ConversationContextName`
- Session listing endpoints return summaries that include both IDs and resolved names so humans can immediately see *what kind of work* the session pertains to (e.g. “Design System Work”, “Backend Refactor”, etc.).

---

### 3.3 Manager Read APIs

**DD-003 — Read Semantics**

`IAgentSessionManager` supports:

- Detail:
  - `GetAgentSessionAsync(agentSessionId, org, user)`  
    → full session, including all turns.
  - `GetAgentSessionTurnSummaryAsync(agentSessionId, turnId, org, user)`  
    → lightweight turn summary (status, timestamps, summaries).
  - `GetFullAgentSessionTurnAsync(agentSessionId, turnId, org, user)`  
    → full turn object (including URLs, refs, diagnostics).
  - `GetLastAgentSessionTurnAsync(agentSessionId, org, user)`  
    → last turn by `SequenceNumber`, any status (Pending/Completed/Failed).

- Lists:
  - `GetAgentSessionsAsync(listRequest, org, user)`  
    → `ListResponse<AgentSessionSummary>` for sessions visible to the caller.
  - `GetAgentSessionsForUserAsync(userId, listRequest, org, user)`  
    → `ListResponse<AgentSessionSummary>` for sessions where:
      - `OwnerUser.Id == userId` **or**
      - Any turn’s `CreatedByUser.Id == userId` (participant).

Manager enforces org/user scoping per your platform’s rules.

---

## 4. Summaries, Blobs & Live Responses (DD-005)

**DD-005 — Summarization & Blob Strategy**

- **Purpose:**
  - Avoid loading “metric tons” of content into the client’s visible history.
  - Keep sessions light to render while preserving full fidelity for deep inspection.

### 4.1 What is stored

For each turn:

- **Inline in the turn:**
  - `InstructionSummary` — truncated first N chars of the instruction.
  - `AgentAnswerSummary` — truncated first N chars of the answer (Completed only).
- **In blob storage:**
  - Full instruction payload → `FullInstructionUrl`.
  - Full answer payload → `FullAgentAnswerUrl`.
  - Optional raw OpenAI request/response payloads:
    - `OpenAIRequestPayloadUrl`
    - `OpenAIResponsePayloadUrl`

### 4.2 Where summaries and blobs are created

- **Orchestrator is responsible** for:
  - Calling a small payload storage component to persist full payloads to blobs, receiving URLs.
  - Computing summaries (truncate to 1024 chars, e.g. `summary = text.Length <= 1024 ? text : text.Substring(0, 1024)`).
  - Populating:
    - `InstructionSummary`, `FullInstructionUrl` on turn creation.
    - `AgentAnswerSummary`, `FullAgentAnswerUrl` on completion.
    - Optional `OpenAIRequestPayloadUrl`, `OpenAIResponsePayloadUrl`.

- **Session Manager**:
  - Treats these fields as normal data.
  - Does **not** generate or manipulate summaries or blobs.
  - Enforces status/timestamp invariants and persists changes.

### 4.3 What the client sees

- `AgentExecuteResponse.Text`:
  - Always contains the **full answer text** for this call.
  - No truncation at the API boundary.

- Thick client / browser session views:
  - Use summaries for fast history views.
  - Load full payloads on demand via `FullInstructionUrl` / `FullAgentAnswerUrl`.

- Thin CLI:
  - Reads `AgentExecuteResponse.Text` and prints it.
  - Ignores blob URLs unless extended to do more.

---

## 5. Live Execution Contract (DD-004)

*(Implied, not yet fully rewritten but conceptually agreed.)*

**DD-004 — AgentExecuteRequest / AgentExecuteResponse**

- Clients call a single execute endpoint (e.g. `POST /api/ai/agent/execute`).
- All client types (Browser, CLI, Thick client) use the same logical contract:
  - Request (simplified view):
    - Agent/Conversation context IDs.
    - Instruction text.
    - WorkspaceId / Repo / language hints.
    - Active file info.
  - Response:
    - `Kind` (`answer` or `error`).
    - `Text` — full answer text for successful answers.
    - `Sources[]` — source refs used for the answer (from RAG).
    - `FileBundle` — optional Aptix file bundle for edits.
    - `Warnings[]`.
    - `ErrorCode`, `ErrorMessage` for errors.

- The thick client may also use separate history endpoints to retrieve session/turn summaries and details, but the **execute** contract stays common across client types.

---

## 6. Rerun Semantics (Deferred) (DD-006)

**DD-006 — Rerun with Updated Context (Experimental / Deferred)**

- Rerun is modeled as a **new turn**, not a mutation of an old one.
- New turn:
  - May reuse the previous instruction or a modified version.
  - Performs a fresh RAG retrieval and active file analysis.
  - Uses `PreviousOpenAIResponseId` to continue the OpenAI conversation chain when appropriate.
- Prior turn remains `Completed` and immutable.
- This behavior is powerful but optional; initially treated as experimental and may be surfaced as “rerun with updated context” in the UI later.

---
