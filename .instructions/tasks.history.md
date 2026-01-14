# Tasks History

2026-01-11 — Added initial task graph for "Flexible Markup/TXT Splitter with Auto-Detected Balises + UI Override":

- task-000001 — Explore UI: UI drop/add flow & decide where analysis is surfaced
- task-000002 — Core: Introduce splitter strategy abstraction by file type (.html/.txt)
- task-000003 — Core: Implement markup/text analyzer that detects candidate record tags + wrapper offsets
- task-000004 — Core: Implement record-boundary chunking using byte sizes + streaming write
- task-000005 — Core: Update configuration model to support auto-detected + user overrides (per-extension profiles)
- task-000006 — UI: Show analysis on file add/drop; allow override; persist overrides
- task-000007 — Tests: Unit tests for analyzer + splitter using example markup and edge cases
- task-000008 — Docs: Update README/QUICKSTART/CLI usage to reflect txt support + detection/override behavior
- task-000009 — Future: Scaffold PDF strategy and define extension point (no full implementation)

Notes: Tasks are ordered and include dependencies and acceptance criteria; each task file contains Implementation Notes and Validation steps to make them actionable.

2026-01-12 — Added tasks for UI modernization, file size limits, improved drag/drop, richer preview, examples, verifier and tests:

- task-000010 — UI: Add input file size limits + thresholds
- task-000011 — UI: Modern dark theme + layout refresh
- task-000012 — UI: Drag-and-drop reliability + drop-zone UX
- task-000013 — UI/Core integration: Preview expected chunks + detected balises on add
- task-000014 — Examples: Make generator produce more complex nested markup inputs
- task-000015 — Core: Implement SplitOutputVerifier (equivalence checker)
- task-000016 — Tests: Add fixtures + integration tests for nested wrappers + verifier

Notes: `task-000016` is a test-focused chore; a detailed test task was also added at `.instructions/test-tasks/test-000001--add-fixtures-integration-verifier.md` to capture fixture generation and CI integration. These tasks are linked; e.g., `task-000014` feeds `task-000016`, and `task-000015` feeds `task-000016`.

2026-01-13 — Archived completed tasks to `.instructions/tasks.archive/`:

- task-000001 — Explore UI: UI drop/add flow & decide where analysis is surfaced
- task-000002 — Core: Introduce splitter strategy abstraction by file type (.html/.txt)
- task-000003 — Core: Implement markup/text analyzer that detects candidate record tags + wrapper offsets
- task-000004 — Core: Implement record-boundary chunking using byte sizes + streaming write
- task-000005 — Core: Update configuration model to support auto-detected + user overrides (per-extension profiles)
- task-000006 — UI: Show analysis on file add/drop; allow override; persist overrides
- task-000010 — UI: Add input file size limits + thresholds
- task-000011 — UI: Modern dark theme + layout refresh

2026-01-13 — Archived additional completed tasks to `.instructions/tasks.archive/`:

- task-000012 — UI: Drag-and-drop reliability + drop-zone UX
- task-000013 — UI/Core integration: Preview expected chunks + detected balises on add

2026-01-14 — Archived remaining completed tasks and normalized archive metadata:

- task-000007 — Tests: Unit tests for analyzer + splitter using example markup and edge cases
- task-000008 — Docs: Update README/QUICKSTART/CLI usage to reflect txt support + detection/override behavior
- task-000009 — Future: Scaffold PDF strategy and define extension point (no full implementation)
- task-000014 — Examples: Make generator produce more complex nested markup inputs
- task-000015 — Core: Implement SplitOutputVerifier (equivalence checker)
- task-000016 — Tests: Add fixtures + integration tests for nested wrappers + verifier

Notes: Updated archived task front matter to `status: archived` and bumped `updated` dates for consistency.
