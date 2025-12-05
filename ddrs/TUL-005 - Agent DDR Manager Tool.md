# TUL-005 — Agent DDR Manager Tool

**ID:** TUL-005  
**Title:** Agent DDR Manager Tool  
**Status:** Approved  
**Owner:** Kevin Wolf and Aptix  

## Approval Metadata

- **Approved By:** Kevin Wolf  
- **Approval Timestamp:** 2025-12-04 15:50:00 EST (UTC-05:00)

---

## 1. Purpose and Scope

TUL-005 defines the Agent DDR Manager Tool that exposes deterministic, LLM facing operations for managing Detailed Design Reviews.  
The tool sits on top of the existing persistence layer and does not embed agent reasoning or RAG behavior.

This DDR covers:

- LLM visible operations and payloads for DDR management
- Mapping between JSON payloads and C sharp models
- High level error and envelope patterns

This DDR does not cover:

- Agent reasoning and state machines
- RAG and indexing flows
- HTTP or user interface details
- Concrete implementation classes

## 2. Core Models

### 2.1 DetailedDesignReview

The DetailedDesignReview type extends EntityBase and participates in TUL-005 as the canonical DDR record.

Fields relevant to this DDR:

- Name  DDR title exposed to the LLM as title
- Description  DDR summary exposed to the LLM as summary
- Goal
- GoalApprovedTimestamp
- GoalApprovedBy
- Tla
- Index
- DdrIdentifier  computed as TLA dash index with three digit zero padding
- Status
- StatusTimestamp
- ApprovedTimestamp
- ApprovedBy
- Chapters  list of DdrChapter

### 2.2 DdrChapter

Fields:

- Id
- Title
- Summary
- Details
- ApprovedTimestamp
- ApprovedBy

### 2.3 DdrTla

Fields:

- Tla
- Title
- Summary
- CurrentIndex  internal only and never exposed to the LLM

## 3. Tool Envelope

All calls from the LLM use a simple JSON envelope.

Request:

```json
{
  operation: name,
  payload: { }
}
```

Response on success:

```json
{
  ok: true,
  result: { }
}
```

Response on failure:

```json
{
  ok: false,
  error: {
    code: ErrorCode,
    message: Human readable explanation
  }
}
```

TUL-005 does not fix the error code list but recommends short stable codes such as DdrNotFound or UnknownTla.

## 4. Operation Groups

TUL-005 defines the complete LLM facing surface of the Agent DDR Manager Tool.  
Operations are grouped for clarity; ordering does not imply execution flow.

### 4.1 TLA catalog

- get_tla_catalog  returns all TLAs with tla, title, summary
- add_tla  adds a new TLA after explicit user confirmation

The LLM never sees CurrentIndex or any raw index values.

### 4.2 DDR creation and metadata

- create_ddr  creates a new DDR under a TLA, setting name, description and initial status Draft, and assigning a unique identifier TLA dash index
- update_ddr_metadata  updates title and summary only
- move_ddr_tla  moves a DDR to a new TLA and assigns a new identifier using the allocator

### 4.3 Goal lifecycle

- set_goal  sets or refines the goal text before approval
- approve_goal  records goal approval with GoalApprovedBy and GoalApprovedTimestamp based on the authenticated user and local time zone

### 4.4 Chapter lifecycle

- add_chapter  adds a single chapter with title and summary
- add_chapters  bulk adds the initial chapter list after the fifty thousand foot overview is approved
- update_chapter_summary  refines a chapter summary
- update_chapter_details  writes or updates long form chapter content
- approve_chapter  records chapter approval with chapter level metadata
- list_chapters  returns lightweight chapter items id, title, summary, approved
- reorder_chapters  reorders chapters based on an explicit ordered list of chapter identifiers
- delete_chapter  removes a chapter from a DDR

### 4.5 DDR status and approval

- set_ddr_status  sets the DDR status field for values such as Draft, InProgress, ReadyForApproval, Approved, Rejected, ResearchDraft
- approve_ddr  records full DDR approval and sets ApprovedBy, ApprovedTimestamp and Status to Approved

### 4.6 Retrieval and navigation

- get_ddr  returns the full DDR including chapters and approval metadata
- list_ddrs  returns lightweight DDR items for navigation identifier, tla, title, summary, status

## 5. Relationship to SYS-001 and other DDRs

TUL-005 implements the persistence and tool facing aspects required by SYS-001 for DDR workflows:

- TLA allocation and catalog management
- Deterministic DDR identifier allocation using the TLA plus index pattern
- Goal and chapter lifecycle operations with explicit approvals
- DDR level approval metadata with time zone aware timestamps
- A closed set of JSON tool operations suitable for use by agents and orchestration layers

Implementation details such as the C sharp tool class, dependency injection wiring, and HTTP hosting are deferred to a follow up implementation DDR.
