# IDX-022 – How We Store Example Values in Specifications

**Status:** Proposed

## 1. Description
Defines how example values are included in Markdown DDR documents and JSON-L artifact records to improve clarity and developer comprehension.

## 2. Decision
- Provide example values **inline** within Markdown for fields that may be ambiguous or complex.
- Example JSON fragments should be fenced using triple-backtick syntax and valid JSON.
- Examples should reflect realistic patterns, and may be updated later with live data once the production vector database is populated.
- Keep examples concise; avoid overly large blocks that obscure the specification.

## 3. Rationale
- Concrete examples greatly reduce ambiguity.
- Increases clarity when onboarding new contributors.
- Markdown-inline examples remain close to the field definition for readability.

## 4. Notes
The specification will evolve; examples will be refreshed with real samples after initial ingestion cycles.
