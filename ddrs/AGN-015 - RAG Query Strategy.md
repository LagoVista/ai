# AGN-015 — RAG Query Strategy

**ID:** AGN-015  
**Title:** RAG Query Strategy  
**Status:** Draft (Tabled at outline stage)

## 1. Purpose

This DDR defines the high-level strategy for when and how the Aptix Agent should query vector databases (RAG) to retrieve contextual information for the LLM. It exists to prevent naive "query on every user message" behavior and instead establish deliberate, context-aware retrieval.

## 2. SYS-001 Workflow Status

- Step 1 — Initiation: Completed.  
- Step 2 — Preliminary metadata: Completed.  
- Step 3 — DDR goal & expected outputs: Completed and approved.  
- Step 4 — Section structure (50K-foot overview): Completed and approved.  
- Steps 5–10: Not yet started. This DDR has been tabled after outline approval.

## 3. 50K-Foot Summary

We want to define when the agent should query the vector database to identify content to feed to the LLM, including human-initiated and LLM-initiated RAG queries, and to describe the conceptual protocol that governs these decisions.

## 4. DDR Goal and Expected Outputs (from SYS-001 Step 3)

### 4.1 DDR Goal

Define a comprehensive strategy for when and how the Aptix Agent should query the vector database (RAG). This includes:
- Determining when RAG should be invoked.
- Defining how queries should be formed (LLM-assisted, transformed, or raw).
- Establishing RAG decision heuristics based on mode, user intent, and task type.
- Specifying whether and how humans can manually trigger RAG queries.
- Describing the protocol for LLM-initiated RAG requests at a conceptual (non-implementation) level.
- Providing a conceptual foundation for follow-on DDRs that will define concrete tools and code.

### 4.2 Expected Outputs

- A single markdown DDR file that defines:
  - RAG invocation strategy.
  - Human-trigger protocol.
  - LLM-trigger protocol.
  - Query shaping and preprocessing rules.
  - Relevance and constraint considerations.
  - A conceptual lifecycle of RAG queries in the agent pipeline.
  - Requirements for downstream implementation DDRs.
- No code, tool implementations, or tests are produced directly by this DDR. Those will be defined in future DDRs that build on AGN-015.

## 5. Proposed Section Structure (50K-Foot Outline)

1. Purpose  
2. Definitions & Core Concepts  
3. When to Query RAG  
4. When Not to Query RAG  
5. Query Planning & Query Shaping  
6. Human-Initiated RAG Queries  
7. LLM-Initiated RAG Queries  
8. RAG Result Processing & Integration  
9. RAG Error Handling & Fallback Rules  
10. Security, Safety, and Misuse Protections  
11. Extensibility Framework  
12. Out of Scope  
13. Requirements for Future Implementation DDRs  
14. Final Summary

## 6. Notes

This file is a placeholder capturing the agreed metadata, goals, and outline for AGN-015. Detailed section content will be authored later when this DDR is taken off the table and re-entered into the SYS-001 workflow at Step 5.
