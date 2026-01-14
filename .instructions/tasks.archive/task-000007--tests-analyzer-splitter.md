---
schema: task/v1
id: task-000007
title: "Tests: Unit tests for analyzer + splitter using example markup and edge cases"
type: chore
status: archived
priority: high
owner: "dev-handle"
skills: ["testing-dotnet-unit", "csharp-expert"]
depends_on: ["task-000003", "task-000004"]
next_tasks: ["task-000008"]
created: "2026-01-11"
updated: "2026-01-14"
---

## Context

Robust unit tests are required to validate analyzer heuristics, chunking behavior, and encoding/streaming edge cases.

## Goal

Add unit tests that cover:
- Analyzer detection on small/large/edge-case files
- Chunker behavior: boundaries preserved, size targets met, large-single-record handling
- Integration-style tests that run analyze + split on sample files and check outputs

## Acceptance Criteria

- Tests added to `tests/LeniTool.Core.Tests/` cover the cases listed and pass reliably in CI.
- Test data (sample files) are added under `tests/fixtures/` and used by tests.
- Tests assert both content correctness (no broken records) and byte-size correctness for multiple encodings.

## Implementation Notes

- Use xUnit and existing test conventions in the repo.
- Add fixtures: simple XML-like `.txt` files, nested wrapper files, badly-formed files (missing end tags), very large single-record file.

## Files likely touched

- `tests/LeniTool.Core.Tests/ServiceTests.cs` (add new test classes)
- `tests/fixtures/*` (add sample input files)

## Validation

- All new tests pass locally and in CI pipeline.
- Tests simulate both buffered analysis (sampling) and full streaming for large files.

## Notes / Discoveries

- The analyzer sampling behavior is driven by the `sampleLimitBytes` parameter: when `fileSize > sampleLimitBytes`, it first samples top tag names from the leading bytes and then scans the full file restricted to those tag names.
- `TxtMarkupSplitBoundaryResolver` behavior differs depending on whether the configured tag is detected in analysis; when it is not detected and offsets are missing, it falls back to scanning the full file (0..Length) but does not preserve wrapper bytes.

## Attempts / Log

- 2026-01-14: Added `tests/fixtures/` with deterministic TXT markup inputs (nested wrapper, malformed tags, multi-tag ambiguity) and wired them into the test project as copied content.
- 2026-01-14: Added unit tests extending coverage for: analyzer candidate ordering + sampling behavior, boundary resolver fallback behavior (via reflection), record span scanning invariants, and chunker size/large-record behavior.
