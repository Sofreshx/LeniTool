---
schema: task/v1
id: task-000005
title: "Core: Update configuration model to support auto-detected + user overrides (per-extension profiles)"
type: feature
status: archived
priority: medium
owner: "dev-handle"
skills: ["csharp-expert"]
depends_on: ["task-000002", "task-000003"]
next_tasks: ["task-000006", "task-000008"]
created: "2026-01-11"
updated: "2026-01-14"
---

## Context

`SplitConfiguration` currently contains fixed fields (MaxChunkSizeMB, SegmentationTags, OpeningTags, ClosingTags...). We need to support auto-detected settings and user-overrides per file and per-extension profile (e.g., `.txt` profile defaults).

## Goal

Extend the configuration to represent:
- Profiles keyed by extension (default for .html, .txt)
- A representation for auto-detected values vs user-overrides
- Per-file override persistence and precedence rules (override > profile > global default)

## Acceptance Criteria

- `SplitConfiguration` (or new config model) supports per-extension profiles and flags for "auto-detect" vs "manual override".
- Persistence (local settings file or user profile) stores overrides and is respected for subsequent runs.
- FileProcessing honors per-file overrides passed by the UI (and persists if user chooses to save override as profile).

## Implementation Notes

- Backwards compatibility is important; existing configuration files should still be valid.
- Consider adding a `Profile` object: `{ extension: ".txt", maxChunkMB: 10, autoDetect: true, overrideTags: { recordTag: null, wrapper: null } }`.
- Wiring into DI and the UI is necessary (see UI task).

## Files likely touched

- `src/LeniTool.Core/Models/SplitConfiguration.cs`
- `src/LeniTool.Core/Services/FileProcessingService.cs`
- Possibly new persistence helpers under `src/LeniTool.Core/Services/ConfigurationService.cs`

## Validation

- Add unit tests asserting override precedence and backward compatibility with existing config files.
- Manual validation: save an override via the UI and restart app to ensure persisted settings apply.

## Notes / Discoveries

- Implemented a lightweight override system without breaking existing `config.json` files: new fields are optional and omitted unless used.
- Precedence implemented as: transient (per-run) > persisted file override > extension profile > global defaults.
- `.txt` splitter now resolves config per file and honors `recordTagName` + `maxChunkSizeMB` when configured; `.html` behavior unchanged.

## Implementation Log

- Updated Core configuration model:
	- Added `recordTagName` + `autoDetectRecordTag` to the global config.
	- Added optional `extensionProfiles` and `fileOverrides` (persisted).
	- Added transient `runOverrides` (non-persisted) for per-run overrides.
	- Added `ResolveForFile(filePath)` to compute effective settings by precedence.
- Updated `.txt` strategy to use resolved config per file (record tag selection + chunk size).
- Added unit tests for precedence resolution and JSON backwards compatibility.

## Validation Results

- `dotnet test tests/LeniTool.Core.Tests/LeniTool.Core.Tests.csproj -c Debug`
	- Passed: 16
	- Failed: 0
