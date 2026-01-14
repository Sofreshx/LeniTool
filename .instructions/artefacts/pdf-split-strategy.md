# PDF Split Strategy (Design Notes)

## Scope (current phase)

This document sketches how PDF support should integrate into the existing `ISplitterStrategy` extension point.
No PDF parsing/splitting is implemented yet.

## Goals

- Support `.pdf` selection via `ISplitterStrategy` and registry.
- Enable future splitting modes:
  - **Page splitting**: 1 page per output file, or N pages per output file.
  - **Record detection**: detect repeating “forms”/sections and split at semantic boundaries.
  - **Text extraction based**: split by extracted text markers (best-effort).
- Keep memory bounded for large PDFs (avoid reading whole files when possible).

## Non-goals (for first implementation)

- OCR for scanned documents.
- Perfect layout preservation across all PDFs.
- Reconstructing PDFs by “byte slicing” (generally not feasible for PDFs).

## Candidate libraries (C# / .NET)

### PdfSharp / PdfSharpCore

- **Pros**: Friendly API, common in .NET desktop apps, good for *page-level* splitting/merging.
- **Cons**: Feature coverage varies by fork/version; text extraction and advanced PDF features can be limited.
- **Best fit**: Page splitting/merging where fidelity matters more than extracted text.

### iText 7

- **Pros**: Powerful feature set, robust PDF manipulation, strong page split/merge.
- **Cons**: Licensing can be a blocker depending on project constraints.
- **Best fit**: If licensing is acceptable and we need advanced manipulation and reliability.

### UglyToad.PdfPig

- **Pros**: Great for **text extraction** and structural inspection; often used for parsing content.
- **Cons**: Not primarily designed for writing/manipulating PDFs into new PDFs.
- **Best fit**: Record detection heuristics based on extracted text (e.g., “Invoice #” boundaries), analysis UI previews.

## Proposed strategy shape

### `PdfSplitterService : ISplitterStrategy`

- `SupportedExtensions`: `.pdf`
- `AnalyzeAsync(filePath)`: 
  - Collect basic metadata (file size, page count if cheap to compute).
  - Optionally extract a sample of text from early pages to propose splitting options.
  - Return `AnalysisResult` with low confidence initially until heuristics exist.
- `SplitFileAsync(filePath, outputDirectory, progress, ct)`:
  - Choose a split mode based on configuration:
    - `mode = pages` (default)
    - `mode = text-markers` (future)
  - Write output PDFs to output directory using existing naming patterns.

## Configuration ideas

- `PdfSplitMode`: `Pages` | `TextMarkers` | `NotSupported`
- `PagesPerChunk`: integer (default 1)
- `MarkerRegex`: string (optional)
- `MaxPagesToAnalyze`: integer (limit for fast analysis)

## UX considerations

- Provide a clear message when PDF is not supported yet.
- Later: show analysis results such as:
  - page count
  - estimated output count for the current config
  - sample extracted text snippets for marker-based splitting

## Streaming / performance constraints

- Page splitting should avoid full-document load when possible.
- For analysis, cap expensive operations (page text extraction) to a small number of pages.
- For very large PDFs, prefer “page metadata only” analysis until user explicitly requests deeper analysis.

## OCR (future)

If OCR is required, treat it as a separate optional pipeline:
- detect whether pages are image-only
- run OCR per page (pluggable engine)
- use OCR text for marker-based splitting

## Open questions

- What licensing constraints apply to PDF libraries (especially iText)?
- Do we need the output to preserve the original PDF exactly (fonts, structure), or is a text-based re-render acceptable?
- Do we need to support encrypted/password-protected PDFs?
