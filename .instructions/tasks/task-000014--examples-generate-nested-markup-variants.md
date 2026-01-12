---
schema: task/v1
id: task-000014
title: "Examples: Make generator produce more complex nested markup inputs"
type: feature
status: not-started
priority: medium
owner: "dev-handle"
skills: ["planning-feature"]
depends_on: []
next_tasks: ["task-000016"]
created: "2026-01-12"
updated: "2026-01-12"
---

## Context

Example input files are used by tests and by developers to exercise edge-cases. Current examples are useful but don't cover complex nested wrapper scenarios.

## Goal

Extend `Example/generate-examples.ps1` (and documentation `Example/README.md`) to produce additional variants that include nested wrappers (envelope/header/payload/footer), varied non-record tags, and nested records to exercise wrapper reconstruction and record detection.

## Acceptance Criteria ✅

- New generator variants include: nested envelope/header/payload/footer structures, multiple non-record tags before/after records (comments/prolog), and multiple nesting levels with records in different depths.
- `Example/README.md` is updated to document new variants and how they map to test cases.
- Generated files reliably exercise wrapper reconstruction and record detection in existing analyzer tests.

## Implementation Notes 🔧

Files to update:
- `Example/generate-examples.ps1` — add new generator flows and parameterized scenarios.
- `Example/README.md` — document new variants and their intended test coverage.

## Validation / Testing Steps 🧪

1. Run the generator and inspect new files for nested wrappers and expected complexity.
2. Use generated files in the analyzer and ensure they produce varied candidate tags and wrapper offsets.

## Dependencies

- Feeds into `task-000016` (tests / fixtures).

## Notes / Next Steps

- Keep the generator deterministic for CI runs but allow a random/large-data mode for stress tests.