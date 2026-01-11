---
schema: task/v1
id: task-000002
title: "Core: Introduce splitter strategy abstraction by file type (.html/.txt)"
type: feature
status: done
priority: high
owner: "dev-handle"
skills: ["csharp-expert", "planning-feature"]
depends_on: []
next_tasks: ["task-000003", "task-000004", "task-000009"]
created: "2026-01-11"
updated: "2026-01-11"
---

## Context

Currently `HtmlSplitterService` reads the entire file and finds split points by searching configured tags. We need a strategy abstraction to allow file-type-specific analysis and splitting (HTML, TXT, future: PDF).

## Goal

Add a `ISplitterStrategy` abstraction and plumb it into `FileProcessingService` so we can select strategies by extension and fall back to a default.

## Acceptance Criteria

- A well-reviewed `ISplitterStrategy` interface exists with methods for Analyze (metadata detection) and Split (streaming split execution).
- `HtmlSplitterService` is refactored to implement the new interface without changing external behavior.
- `FileProcessingService` chooses a strategy based on file extension and exposes a well-defined registration point for new strategies.
- Unit tests cover strategy selection and `HtmlSplitterService`'s behavior under the abstraction.

## Implementation Notes

- Keep API surface minimal: e.g. `Task<AnalysisResult> AnalyzeAsync(Stream input, CancellationToken ct)` and `IAsyncEnumerable<SplitResult> SplitAsync(Stream input, AnalysisResult analysis, CancellationToken ct)`.
- Ensure `SplitResult` carries byte ranges / encoding info for streaming writes.
- Add a simple factory or use DI registration to map extensions -> strategy.

## Files likely touched

- `src/LeniTool.Core/Services/HtmlSplitterService.cs`
- `src/LeniTool.Core/Services/FileProcessingService.cs`
- New files: `ISplitterStrategy.cs`, `SplitterStrategyFactory.cs`, `Models/AnalysisResult.cs`, `Models/SplitResult.cs`

## Validation

- Existing workflows with HTML files continue to succeed with identical outputs (run existing tests).
- New unit tests validate the interface contract and behavior.

## Notes / Discoveries

- Implemented a minimal, path-based strategy abstraction to keep refactor small and preserve current behavior.
- Added `AnalyzeAsync(filePath)` returning a lightweight `AnalysisResult`; richer streaming analysis is deferred to later tasks.

## Results

- Added `ISplitterStrategy` + `SplitterStrategyRegistry` for extension-based selection.
- Refactored `HtmlSplitterService` to implement `ISplitterStrategy` without changing split behavior.
- Updated `FileProcessingService` to use the registry for strategy selection and for `ValidateFiles`.
- Added unit tests for strategy selection + registry-driven validation.

## Attempts / Log

- 2026-01-11: Implemented strategy abstraction + registry; refactored core services; added tests; validated with `dotnet test`.
