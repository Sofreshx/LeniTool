---
schema: task/v1
id: task-000009
title: "Future: Scaffold PDF strategy and define extension point (no full impl)"
type: research
status: not-started
priority: low
owner: "dev-handle"
skills: ["planning-feature"]
depends_on: ["task-000002"]
next_tasks: []
created: "2026-01-11"
updated: "2026-01-11"
---

## Context

PDF splitting is a future requirement. We should define how a PDF splitter would plug into the new `ISplitterStrategy` abstraction and what capabilities (streaming pages, OCR, record detection heuristics) would be required.

## Goal

Produce a short design doc that sketches the PDF strategy (entry points, 3rd-party libs to evaluate, sample APIs) and the minimal scaffold code needed to register a PDF strategy for later implementation.

## Acceptance Criteria

- Design doc outlining candidate libraries (PDFSharp, iText, etc.), tradeoffs, and required features (page granularity, byte-aware streaming, OCR fallback).
- A project-level placeholder (e.g., `PdfSplitter.cs` implementing `ISplitterStrategy` with NotImplemented exceptions) and registration hook.

## Implementation Notes

- No full PDF parsing or OCR implementation now — only define the extension point and a safe placeholder.

## Files likely touched

- `src/LeniTool.Core/Services/PdfSplitter.cs` (scaffold)
- DI registration in `FileProcessingService` or Startup code

## Validation

- Team review of the design doc and acceptance of the scaffold.
