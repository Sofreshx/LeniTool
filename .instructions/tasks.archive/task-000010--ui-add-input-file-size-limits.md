---
schema: task/v1
id: task-000010
title: "UI: Add input file size limits + thresholds"
type: feature
status: archived
priority: medium
owner: "dev-handle"
skills: ["csharp-expert"]
depends_on: []
next_tasks: ["task-000012", "task-000013"]
created: "2026-01-12"
updated: "2026-01-14"
---

## Context

Users can add very large input files; some workflows must refuse extremely large files, while others should let users add large files but avoid expensive auto-analysis until the user explicitly requests it.

## Goal

Add UI-configurable settings and behaviors to control a hard maximum allowed input file size and an "Auto-analyze threshold" that disables automatic analysis for large-but-allowed files.

## Acceptance Criteria ✅

- A new settings section is available in the UI with controls: **Max input file size** (human-friendly MB/GB) and **Auto-analyze threshold**.
- Default values are sensible (e.g., Max = 5 GB, Auto-analyze threshold = 100 MB) but configurable in settings persisted across restarts.
- Adding a file larger than Max shows a log message, rejects the file, and marks it as rejected in the file list.
- Adding files between Threshold and Max is allowed but will not auto-trigger analysis (user can still manually analyze).
- Behavior is enforced consistently for drag/drop and file-add dialogs.

## Implementation Notes 🔧

Likely files / areas to touch:
- `MainViewModel.cs` — enforce size logic in add/drop path and expose settings to the UI.
- `MainWindow.axaml` / `MainWindow.axaml.cs` — add UI settings controls and inline guidance.
- `SplitConfiguration.cs` or equivalent settings model — add persisted settings with sensible defaults.
- Logging surface (existing app log) — add clear rejection messages.

Edge cases:
- Show sizes in human-readable format in lists and messages.
- Validate values in settings UI (no negative sizes).

## Validation / Testing Steps 🧪

1. Set Max to a small value, try to add a larger file → file is rejected and log shows a clear message.
2. Set Threshold below Max; add a file between threshold and max → file is added but analysis is not auto-triggered.
3. Ensure behavior is identical for drag/drop and Add File dialog.

## Dependencies

- Upstream tasks: none
- Downstream tasks: `task-000012`, `task-000013`

## Notes / Next Steps

- Decide exact default values with the team; defaults above are suggestions. Add unit tests for the enforcement logic in the view model or service layer.

## Assumptions

- `SplitConfiguration.MaxInputFileSize` and `SplitConfiguration.AutoAnalyzeThreshold` are persisted in **MB** (to match existing `MaxChunkSizeMB`), with computed `*Bytes` helpers.
- `MaxInputFileSize = 0` means **no hard cap** (disabled). `AutoAnalyzeThreshold = 0` means **never auto-analyze**.
- UI uses GB for Max (bound via `MainViewModel.MaxInputFileSizeGB`) and MB for threshold (bound via `MainViewModel.AutoAnalyzeThresholdMB`).

## Implementation Log

- Added persisted size-limit settings + validation in `SplitConfiguration`.
- Updated `MainViewModel.AddFilesFromPathsAsync` to reject over-max files, disable auto-analyze over-threshold, and auto-analyze under-threshold.
- Ensured rejected files are excluded from processing.
- Added configuration controls to the desktop UI.

## Validation

- `dotnet build src/LeniTool.Desktop/LeniTool.Desktop.csproj` ✅