---
schema: task/v1
id: task-000003
title: "Core: Implement markup/text analyzer that detects candidate record tags + wrapper offsets"
type: feature
status: archived
priority: high
owner: "dev-handle"
skills: ["csharp-expert", "marten-linq-querying"]
depends_on: ["task-000002"]
next_tasks: ["task-000004", "task-000005", "task-000007"]
created: "2026-01-11"
updated: "2026-01-14"
---

## Context

We need an analyzer that can detect likely "record" tags (the repeating element that represents a single logical chunk) and optional wrapper/preamble/suffix (e.g., <Example> ... </Example>) in plain text markup (XML-like) and HTML. The analyzer should not reformat the file or require validation; it must be tolerant of imperfect markup.

## Goal

Create an analyzer component that inspects the file (prefer streaming / sampling for large files) and returns candidate record tags (single or pair), wrapper start/end offsets, encoding, file size, and estimated split counts for a target max chunk size.

## Acceptance Criteria

- Analyzer detects candidate record element names and wrapper boundaries with high accuracy on the provided sample files.
- Analyzer provides confidence metrics and at least 2-3 alternative candidate tags when ambiguous.
- Analyzer supports ASCII/UTF-8/UTF-16 encodings and reports estimated byte sizes for split decisions.
- Analyzer runs in streaming mode for large files (reads buffered chunks) and avoids loading the entire file into memory where possible.

## Implementation Notes

- Use byte-aware scanning; for markup detection look for patterns like `<Tag` and `</Tag>` within a parse window, but avoid full DOM parsing.
- When tags are nested (e.g., wrapper + record), prefer the smallest repeating sibling element for record boundaries.
- Provide offsets for wrapper prefix/suffix (first occurrence of wrapper open and last closing) to ensure output files start and end correctly.
- Return `AnalysisResult` that includes: `Encoding`, `FileSizeBytes`, `CandidateRecords: List<(tagName, openOffset, closeOffsetSample, countEstimate)>`, `WrapperRange: (prefixEndOffset, suffixStartOffset) | null`, `Confidence`.

## Files likely touched

- New: `src/LeniTool.Core/Services/MarkupAnalyzer.cs`
- `src/LeniTool.Core/Services/HtmlSplitterService.cs` (to reuse logic)
- `src/LeniTool.Core/Models/AnalysisResult.cs`

## Validation

- Add test inputs (small to large) with clear record elements, nested wrapper, missing closing tags, and mixed whitespace; run analyzer to verify candidate tags match expectations.
- Build metrics for confidence and sample offsets and validate via unit tests (see test task).

## Attempts / Log

- 2026-01-11: Implemented `MarkupAnalyzer` (streaming byte-scan with UTF-8/UTF-16 BOM detection), extended `AnalysisResult` with encoding/candidates/wrapper/confidence, and added 2 unit tests.
