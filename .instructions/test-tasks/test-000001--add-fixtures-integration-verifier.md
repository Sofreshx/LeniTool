---
schema: task/v1
id: test-000001
title: "Add integration tests: nested wrappers + SplitOutputVerifier"
type: chore
status: not-started
priority: high
owner: "dev-handle"
skills: ["testing-dotnet-unit"]
depends_on: ["task-000014","task-000015"]
next_tasks: []
created: "2026-01-12"
updated: "2026-01-12"
---

## Context

This test task captures the detailed work required to add deterministic fixtures and integration tests that exercise nested wrappers, ambiguous multi-tag cases, and the `SplitOutputVerifier`.

## Goal

Provide deterministic fixtures (committed or generated at test-time) and integration tests that run split operations and assert equivalence using the verifier, producing clear diagnostics on failure.

## Acceptance Criteria ✅

- Fixtures cover nested envelope/header/payload/footer cases, multi-layer nesting, and pre/post non-record tags (prolog/comments).
- Integration tests run in CI and locally and reliably fail with diagnostic output when verifier detects a mismatch.
- Tests include a failing case that demonstrates verifier diagnostics (first-diff offset, chunk id, context hexdump).

## Implementation Notes 🔧

- Add fixtures under `tests/Integration/Fixtures/` or generate them during test setup using `Example/generate-examples.ps1` variations.
- Add `tests/Integration/SplitterVerifierTests.cs` which:
  - Runs splitting on fixtures
  - Runs `SplitOutputVerifier` against the produced chunks
  - Asserts pass/fail and checks diagnostics for failing cases

## Validation / Testing Steps 🧪

1. Run the integration test suite locally — all new tests should pass.
2. Add a deliberate corruption to a chunk in a test to confirm the verifier reports expected details.

## Dependencies

- Implementation artifacts from `task-000014` (fixtures) and `task-000015` (verifier) must be available.

## Notes / Next Steps

- This file complements `task-000016` (a chore/task entry) and provides the detailed test plans and CI notes.