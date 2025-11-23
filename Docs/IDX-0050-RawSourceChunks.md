# IDX-0050 – Raw Source Chunk Architecture

**Status:** Proposed  
**Applies To:** All C# source files processed by the RoslynCSharpChunker  
**Primary Goal:** Define a consistent, symbol-aware chunking scheme for raw C# source code that complements structured SummarySections and provides high-fidelity implementation context for RAG.

---

## 1. Problem & Context

The indexing pipeline now emits **structured semantic descriptions** (e.g., ModelStructureDescription, ModelMetadataDescription, Manager/Repository/EndpointDescription) which are converted into human-readable SummarySections and embedded.

However, LLMs often need to:

- Inspect **actual implementation details** (code paths, parameters, conditionals).
- Map structured intent back to **real code** for verification.
- Answer questions like:
  - "Where is this method implemented?"
  - "What happens before we call the repository?"
  - "Which model properties are actually used here?"

To support this, we also index the **raw C# source** at both file and symbol levels. IDX-0050 defines how those raw-source chunks are shaped so they:

- Are **symbol-aware**, not arbitrary line blocks.
- Respect a **token budget** and use safe overlaps.
- Carry enough **semantic hints** (via header comments) to stay aligned with the structured summaries.

---

## 2. Core Concept – CodeSections → RagChunks

The Roslyn-based chunker produces intermediate **CodeSections**, which are then mapped to `RagChunk` instances.

Each CodeSection corresponds to either:

1. A **file-level** view (header comments + using directives), or
2. A **symbol-level** view (type or member: class, interface, method, property, etc.).

At the `RagChunk` layer, each section must include:

- `SectionKey` – what kind of section this is.
- `Symbol` – name of the symbol or file.
- `SymbolType` – classification of the symbol.
- `LineStart` / `LineEnd` – 1-based line range within the source file.
- `TextNormalized` – the actual text to embed (header comments + code where applicable).

---

## 3. SectionKey & SymbolType

### 3.1 Allowed SectionKey Values

For raw C# chunks, the following `SectionKey` values are defined:

| SectionKey | Meaning                               |
| ---------- | ------------------------------------- |
| `file`     | File-level summary (comments + usings) |
| `type`     | Type declaration (class/struct/record/interface) |
| `method`   | Method / constructor / local function / operator |
| `property` | Property declaration                  |
| `field`    | Field declaration (within a type)     |
| `event`    | Event declaration                     |

Additional keys (e.g., `enum`, `delegate`) may be added later using the same pattern.

### 3.2 Symbol & SymbolType

- **Symbol**
  - For `file` sections: the file name (e.g., `AgentContextTestManager.cs`).
  - For symbol sections: the symbol name (`AgentContextTestManager`, `AddAgentContextTestAsync`, etc.).

- **SymbolType**
  - Mirrors the `SectionKey` for raw source:
    - `file`, `type`, `method`, `property`, `field`, `event`.
  - This is intentionally aligned with the `RagVectorPayload.SymbolType` field so queries can filter by symbol kind.

---

## 4. File-Level Sections

Every C# file MAY produce **one file-level chunk** with:

- `SectionKey = "file"`
- `SymbolType = "file"`
- `Symbol = <file name>` (e.g., `AgentContextTestManager.cs`)

### 4.1 Contents

The file-level `TextNormalized` SHOULD contain:

1. Leading comments and documentation at the top of the file:
   - License header
   - File-level summary
   - XML documentation comments preceding the first type
2. All `using` directives in the file.

This chunk provides **high-level context** about what the file does and which namespaces it depends on. It is not intended to capture every detail, only the big picture.

### 4.2 Token Budget

- File-level text is trimmed if necessary to stay under the configured `maxTokensPerChunk`.
- Trimming occurs from the end and is rare in practice.

---

## 5. Symbol-Level Sections

Each relevant symbol (type or member) produces one or more symbol-level CodeSections. These are the primary units for **implementation-level reasoning**.

### 5.1 Included Symbols

The following Roslyn nodes produce symbol-level sections:

- `ClassDeclarationSyntax`, `StructDeclarationSyntax`, `RecordDeclarationSyntax`, `InterfaceDeclarationSyntax` → `SectionKey = "type"`
- `MethodDeclarationSyntax`, `ConstructorDeclarationSyntax`, `LocalFunctionStatementSyntax`, `OperatorDeclarationSyntax`, `ConversionOperatorDeclarationSyntax` → `SectionKey = "method"`
- `PropertyDeclarationSyntax` → `SectionKey = "property"`
- `FieldDeclarationSyntax` (when inside a type) → `SectionKey = "field"`
- `EventDeclarationSyntax`, `EventFieldDeclarationSyntax` → `SectionKey = "event"`

### 5.2 Line Ranges

For each symbol:

- Start with Roslyn’s mapped line span for the node.
- **Expand upward** to include leading XML doc comments and attributes.
- Clamp to file bounds.

The resulting inclusive 1-based line range is stored as:

```csharp
LineStart
LineEnd
```

This enables precise linking back to the original source in tools and UIs.

---

## 6. Method Header Comments (Natural-Language Summaries)

To improve retrieval and reasoning quality, every **method-level chunk** (`SectionKey = "method"`) includes a short, natural-language header comment before the raw code.

### 6.1 Mechanism

The chunker uses `MethodSummaryBuilder.BuildHeaderComment(MethodSummaryContext ctx)` to build a single-line summary:

```csharp
// Method AddAgentContextTestAsync. Signature: Task<InvokeResult> AddAgentContextTestAsync(AgentContextTest ctx, EntityHeader org, EntityHeader user).
```

Then the chunk text is:

```csharp
TextNormalized = headerComment + "\n" + rawCodeSlice
```

### 6.2 Context Fields

The `MethodSummaryContext` currently includes:

- `MethodName` – the method identifier (`AddAgentContextTestAsync`).
- `Signature` – return type, name, and parameter list.
- Future fields (optional, may be threaded in later):
  - `SubKind` (e.g., `ManagerMethod`, `RepositoryMethod`)
  - `DomainName` / `DomainTagLine`
  - `ModelName` / `ModelTagLine`

This allows the same header mechanism to evolve as more domain/model context is passed down, without changing the chunking contract.

---

## 7. Chunk Size & Overlap

### 7.1 Token Budget

Symbol chunks are bounded by a configurable maximum token budget:

- `maxTokensPerChunk` (default: ~4096–6500 tokens)
- `TokenEstimator.EstimateTokens(text)` is used to estimate token count.

### 7.2 Normal Multi-Line Symbols

For normal multi-line symbols:

- Lines are appended one by one until the next line would exceed `maxTokensPerChunk`.
- If a single line already exceeds the budget, we fall back to **single-line slicing** (see below).

### 7.3 Very Long Single Lines

If a single line (e.g., minified JSON/JS or huge string literal) exceeds the token budget:

- The line is split into multiple segments via `SliceVeryLongLine`.
- Each segment becomes a separate chunk with:
  - `LineStart = LineEnd = <original line number>`
  - `TextNormalized = segment`

This ensures we never drop content, but still respect token limits.

### 7.4 Overlap

To give the embedder continuity between symbol chunks:

- An **overlap in lines** is used between consecutive chunks for larger symbols.
- Overlap is configured via `overlapLines` (default: 6).
- Cursor advance logic ensures **forward progress** even with extreme settings (small budgets or large overlap).

---

## 8. Mapping to RagChunk & RagVectorPayload

Each CodeSection is directly mapped to a `RagChunk` instance:

- `SectionKey` → `RagChunk.SectionKey`
- `Symbol` → `RagChunk.Symbol`
- `SymbolType` → `RagChunk.SymbolType`
- `TextNormalized` → `RagChunk.TextNormalized`
- `LineStart` / `LineEnd` → `RagChunk.LineStart` / `RagChunk.LineEnd`
- `EstimatedTokens` → populated from `TokenEstimator`

The `RagPayloadFactory` then uses:

- `Symbol` / `SymbolType` for linking to higher-level assets (e.g., ManagerDescription, ModelStructureDescription).
- `SectionKey` for filtering and analysis.
- `LineStart` / `LineEnd` / `Path` to provide direct source navigation.

This raw-source layer **augments** structured SummarySections; both can be retrieved in the same query and presented together.

---

## 9. Design Rationale

### 9.1 Why Symbol-Level Instead of Arbitrary Slices?

Symbol boundaries (methods, types, properties) align with how developers and LLMs reason about code:

- "Show me how we create an agent context."
- "Where is the repository method that loads this model?"

Symbol-aligned chunks make it easier to:

- Answer these questions directly.
- Navigate from structured descriptions to implementation.

### 9.2 Why Add Natural-Language Headers?

Structured descriptions already give you rich semantics, but raw code:

- Often uses terse names.
- Lacks redundant context (domain/model/role).

A short header line:

- Boosts semantic recall.
- Helps the LLM understand *why* this code exists.
- Still keeps raw code unmodified beneath the comment.

### 9.3 Why Keep File-Level Chunks?

File-level chunks provide:

- A quick, high-level view of what the file is about.
- A list of using directives for understanding dependencies.

They help the LLM orient itself before diving into symbols.

---

## 10. Summary

- **IDX-0050** defines a consistent, symbol-aware **raw C# chunking** strategy.
- Each chunk has:
  - `SectionKey`, `Symbol`, `SymbolType`, line ranges, and `TextNormalized`.
- File-level chunks capture header comments and using directives.
- Symbol-level chunks capture types and members, with optional natural-language headers for methods.
- Token budgets and overlap ensure chunks are safely embeddable.
- This raw-source layer complements structured SummarySections and underpins rich, implementation-aware RAG scenarios.

This document should be kept in sync with the implementation of `RoslynCSharpChunker` and any future enhancements to method header summaries or symbol classification.
