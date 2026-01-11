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
