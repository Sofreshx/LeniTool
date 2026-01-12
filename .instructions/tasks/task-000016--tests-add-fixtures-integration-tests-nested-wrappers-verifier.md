---
schema: task/v1
id: task-000016
title: "Tests: Add fixtures + integration tests for nested wrappers + verifier"
type: chore
status: not-started
priority: high
owner: "dev-handle"
skills: ["testing-dotnet-unit", "testing-frontend-unit"]
depends_on: ["task-000014","task-000015"]
next_tasks: []
created: "2026-01-12"
updated: "2026-01-12"
---

## Context

Robust fixtures and integration tests are required to validate nested-wrapper detection, ambiguous multi-tag scenarios, splitting correctness, and the new verifier service.

## Goal

Add fixtures covering nested wrappers and ambiguous tag scenarios and integration tests that run the splitter on those fixtures and then run the `SplitOutputVerifier` to validate outputs.

## Acceptance Criteria ✅

- Fixtures (either committed small files or generated in test setup) cover nested envelope/header/payload/footer structures and multi-tag ambiguity.
- Integration tests run the splitting end-to-end on fixtures and exercise the `SplitOutputVerifier` on the outputs, failing with clear diagnostics if mismatch occurs.
- The tests are deterministic and pass in CI.

## Implementation Notes 🔧

Files to add/update:
- `tests/Integration/Fixtures/` — add fixture inputs or a generator helper.
- `tests/Integration/SplitterVerifierTests.cs` — new integration test class to run splitter then verifier.
- Consider reusing or invoking `Example/generate-examples.ps1` to produce fixtures at test-time.

## Validation / Testing Steps 🧪

1. Run the new integration tests locally — they should pass.
2. Intentionally corrupt a chunk within a test to confirm the verifier reports the expected failing chunk and offset.

## Dependencies

- Depends on `task-000014` (new example fixtures) and `task-000015` (verifier).

## Notes / Next Steps

- Because this is test-heavy, also add a test task file in `.instructions/test-tasks/` describing fixture-generation details and CI integration.