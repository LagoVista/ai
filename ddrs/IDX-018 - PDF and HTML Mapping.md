# IDX-018 – PDF / HTML Mapping

**Status:** Proposed

## 1. Description
Provides cross-format navigation support for chunks extracted from PDFs or HTML documents using `PdfPages` and `HtmlAnchor`.

## 2. Decision
- `PdfPages` (array of 1-based page numbers) and `HtmlAnchor` (string fragment) are **optional**.
- Chunks spanning multiple PDF pages list all pages or a range.
- Fields remain null for content types where PDF/HTML mapping does not apply.
- If extraction fails, do not block ingestion; leave fields null.
- If both formats exist, either or both fields may be populated.

## 3. Rationale
- Provides deep-link navigation from vector results back to PDF/HTML.
- Improves usability for large documents.
- Keeps these fields optional for simplicity.

## 4. Resolved Questions
1. Require one of the two fields? → Deferred.
2. Use compact ranges? → Deferred.
3. Full URL or fragment only? → Deferred.
4. Prefer PDF or HTML when both exist? → Deferred.
