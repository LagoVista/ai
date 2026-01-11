# **AGN-002**  
**Title:** Aptix RAG Context Injection Pattern  
**Status:** Proposed  
**Domain:** Aptix Orchestrator  
**Owner:** Kevin Wolf  
**Tags:** rag, retrieval, context, prompts, responses-api, aptix  

---

## **Objective**

Define a standardized pattern for how Aptix retrieves, formats, and injects vector-database (RAG) context into `/responses` API calls.  
This DDR describes:

- how chunks are labeled  
- how metadata is represented  
- how code is fenced  
- how multiple chunks are combined  
- how the LLM is expected to interpret this context  

This pattern ensures consistency, readability, and predictable behavior for all Aptix workflows, including DDR creation, refinement, readiness checks, implementation planning, and code generation.

---

## **Context**

Aptix relies heavily on source-indexed chunks stored in a vector database. These chunks contain:

- extracted code  
- dependency information  
- structured model data  
- metadata such as file path, line ranges, and identities  

To support correct reasoning and safe code generation, the LLM must be provided with relevant context in a clear, labeled, and predictable format.

The term "RAG context" refers to the set of chunks retrieved based on the current task (DDR refinement, implementation, etc.) and the user’s workspace state.

Because Aptix uses the `/responses` API with multi-turn session state, RAG context must be injected at a **per-turn basis** inside the user message, never inside the system prompt or tool fields.

This DDR formally defines how that context must be represented.

---

## **Design**

### **1. RAG context belongs in the `user` role**

All retrieved context is included as part of the **user message** in the `/responses.input` array.

The structure is:

- first `content` item:  
  - `[MODE: …]`  
  - `[INSTRUCTION]`  
- second `content` item:  
  - `[CONTEXT]`  
  - all chunks formatted with the scheme defined below

### **2. All chunks appear in a single `content` block**

We combine all chunks into **one `text` item** for simplicity, readability, and predictable LLM behavior.

Example overall structure:

```text
[
  { "role": "user", "content": [
      { "type": "text", "text": "<mode + instruction>" },
      { "type": "text", "text": "<full context block>" }
  ]}
]
```

### **3. A single `[CONTEXT]` header**

The block begins with:

```text
[CONTEXT]
```

This tells the reasoner exactly where referenced material begins.

### **4. Each chunk is clearly delimited**

Each chunk begins with:

```text
=== CHUNK N ===
```

Where `N` is 1-based and increases sequentially.

This allows the LLM to discuss "Chunk 1", "Chunk 2", etc., and makes the structure obvious to both humans and the model.

### **5. Required metadata fields**

Under the chunk header, include:

```text
Id: <stable chunk id>
Path: <relative file path>
Lines: <start-end>
Language: <language>
```

These fields are always in this order.

Meaning:

- **Id**: internal chunk identifier, mapped to the agent’s `SourceRef`.
- **Path**: relative path within the repo or workspace.
- **Lines**: the exact line span included.
- **Language**: one of: `csharp`, `typescript`, `scss`, `json`, `xml`, etc.

### **6. Fenced code block with proper syntax highlighting**

The code or content of the chunk must follow within a fenced block.

Use the backtick fencing literally as:

```text
```<language>
<chunk content>
```
```

For example:

```text
```csharp
public class InvoiceManager {
    ...
}
```
```

The language tag significantly improves LLM parsing and correctness.

### **7. Blank line separation**

There is always:

- one blank line after the `[CONTEXT]` header  
- one blank line between chunks  
- one blank line after the closing code fence of each chunk

This spacing reduces hallucination rates and improves chunk readability.

### **8. Example complete context block**

Below is a canonical example (with fences written safely):

```text
[CONTEXT]

=== CHUNK 1 ===
Id: ctx_1
Path: Billing/Managers/InvoiceManager.cs
Lines: 40-85
Language: csharp
```csharp
public class InvoiceManager
{
    public void SendInvoiceEmail(Invoice invoice) {
        ...
    }
}
```

=== CHUNK 2 ===
Id: ctx_2
Path: Billing/Api/InvoiceController.cs
Lines: 10-55
Language: csharp
```csharp
[ApiController]
[Route("api/invoices")]
public class InvoiceController : ControllerBase
{
    ...
}
```
```

### **9. Expectations of the reasoner**

The Aptix Reasoner contract will include the following guidance:

- Use only the code/content provided in the `[CONTEXT]` section.
- Treat each chunk as authoritative for its line span.
- If multiple chunks come from the same file, do not merge them unless explicitly asked.
- Prefer incremental updates to full rewrites.
- Reference chunks by Id or chunk number when explaining reasoning.

This defines the model’s understanding of how to consume context safely.

---

## **Risks / Open Questions**

1. **Chunk explosion**  
   - If too many chunks are returned, prompts may become long.  
   - Future versions may impose a token or chunk count limit.

2. **MERGING behavior**  
   - Chunks from the same file may be adjacent or overlapping.  
   - Does the server merge them before sending, or preserve original boundaries?

3. **Structured vs free-text chunks**  
   - Some chunks may contain JSON, Markdown, or structured model metadata.  
   - Should language tags use generic forms (`json`, `markdown`) or specific ones (`ragmodel`)?

4. **Attention scoring heuristics**  
   - Later Aptix versions may adjust chunk ordering based on query type or Reasoner mode.

---

## **Testing Strategy**

- Confirm that the LLM recognizes and respects each chunk boundary.  
- Validate instructions referencing "Chunk 1", "Chunk 2", etc.  
- Check correctness of syntax highlighting using language tags.  
- Stress test with mixed chunk languages (C#, TS, SCSS).  
- Validate that the model does not hallucinate missing content outside line bounds.  
- Confirm that indentation and fenced content survive multiple refine passes.

---

## **Notes / Rationale**

A consistent chunking format is essential for:

- predictable LLM behavior  
- safe incremental code modification  
- correct reference to source files  
- robust multi-mode workflows (DDR → readiness → plan → implementation)  
- building a reusable prompting architecture across all Aptix agents

This DDR establishes the canonical RAG context format, ensuring the entire system—extension, server, orchestrator, and reasoner—can interoperate reliably.
