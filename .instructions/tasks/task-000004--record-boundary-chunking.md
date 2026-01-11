---
schema: task/v1
id: task-000004
title: "Core: Implement record-boundary chunking using byte sizes + streaming write"
type: feature
status: done
priority: high
owner: "dev-handle"
skills: ["csharp-expert", "planning-feature"]
depends_on: ["task-000002", "task-000003"]
next_tasks: ["task-000005", "task-000007"]
created: "2026-01-11"
updated: "2026-01-11"
---

## Context

After detecting record boundaries we need to produce output chunks that respect record boundaries and a target max chunk size in bytes while writing via streaming (avoid reading/writing entire file where possible).

## Goal

Implement a chunker that consumes an `AnalysisResult` and the input stream, and produces chunk files where each chunk contains whole record elements and overall chunk size is near the configured MaxChunkSizeMB (byte-aware and encoding-aware).

## Acceptance Criteria

- Chunking never breaks a record in half; each output file starts/ends with valid content relative to the input (using wrapper prefix/suffix when relevant).
- Writing is streaming: the input is read progressively, and output files are written in sequence without holding entire output in memory.
- Byte size calculations are correct for encoded content (UTF-8, UTF-16) and chunk sizes match target within reasonable tolerance (e.g., +/- 2%).
- Handles edge cases: very large single record (larger than target chunk) — either place alone or optionally split with a warning based on config.

## Implementation Notes

- Use `Stream` operations and buffered reads; careful with splitting at character boundaries for multibyte encodings.
- `SplitResult` should include output file metadata and final byte size.
- Consider temporary files / atomic rename pattern for safe writes.

## Files likely touched

- New: `src/LeniTool.Core/Services/RecordChunker.cs`
- New: `src/LeniTool.Core/Services/RecordSpan.cs`
- New: `src/LeniTool.Core/Services/RecordSpanScanner.cs`
- New: `src/LeniTool.Core/Services/TxtMarkupSplitterService.cs`
- `src/LeniTool.Core/Services/FileProcessingService.cs`
- Tests: `tests/LeniTool.Core.Tests/ServiceTests.cs`

## Validation

- Create unit tests with sample files to assert output files contain whole records and sizes are correct.
- Add an integration test run using the UI (manual) to ensure streaming works for large files without OOM.

## Results

- Implemented streaming record scanning + byte-based chunk writing (prefix + whole records + suffix) via `RecordSpanScanner` and `RecordChunker`.
- Added `.txt` end-to-end strategy `TxtMarkupSplitterService` using `MarkupAnalyzer` + record-boundary chunking.
- Updated default registry (`FileProcessingService.CreateDefaultRegistry`) to register both HTML and TXT strategies.
- Added unit tests asserting `.txt` is supported by default registry and that UTF-8 + UTF-16 wrapper+records split into chunks preserving wrapper and whole records.

## Validation Results

- `dotnet test "C:\Users\lolzi\Documents\GitHub\LeniTool\tests\LeniTool.Core.Tests\LeniTool.Core.Tests.csproj" -c Debug` ✅ (13 passed)
