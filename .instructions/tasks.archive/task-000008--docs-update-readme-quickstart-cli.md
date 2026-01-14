---
schema: task/v1
id: task-000008
title: "Docs: Update README/QUICKSTART/CLI usage to reflect txt support + detection/override behavior"
type: docs
status: archived
priority: medium
owner: "dev-handle"
skills: ["planning-feature"]
depends_on: ["task-000006", "task-000005", "task-000007"]
next_tasks: []
created: "2026-01-11"
updated: "2026-01-14"
---

## Context

Documentation must reflect `.txt` support, how the analyzer detects record/wrapper balises, and how users can override detected values via the UI or CLI.

## Goal

Update `README.md`, `QUICKSTART.md`, and `CLI_USAGE.md` with examples showing:
- How detection works (what analyzer looks for)
- How to override detected tags in the UI and via CLI flags (if supported)
- Example of expected output for a sample `.txt` file

## Acceptance Criteria

- Updated docs exist and clearly explain detection and override behavior with examples.
- CLI usage is updated if new flags are introduced (e.g., `--record-tag`, `--wrapper`), or documentation explains how to use the UI override.

## Implementation Notes

- Link to unit tests / fixtures as examples.
- Include screenshots for the UI override once UI is implemented (or placeholder images + TODO).

## Files likely touched

- `README.md`
- `QUICKSTART.md`
- `CLI_USAGE.md`

## Validation

- Docs reviewed and accepted by the team.
- Example commands are tested locally to ensure accuracy.

## Implementation Log

- 2026-01-14
	- Updated README/Quickstart to document the Avalonia Desktop app as the primary UX.
	- Documented supported extensions: `.html/.htm` and `.txt` (markup), clarified `.pdf` is a placeholder and not implemented.
	- Added `.txt` example snippet and config examples showing `autoDetectRecordTag` / `recordTagName` plus `extensionProfiles` and `fileOverrides`.
	- Rewrote CLI_USAGE to reflect that no CLI project exists currently and to point users to Desktop/config automation.
