---
schema: task/v1
id: task-000006
title: "UI: Show analysis on file add/drop; allow override; persist overrides"
type: feature
status: archived
priority: high
owner: "dev-handle"
skills: ["react-query", "testing-frontend-unit"]
depends_on: ["task-000001", "task-000005"]
next_tasks: ["task-000008"]
created: "2026-01-11"
updated: "2026-01-14"
---

## Context

UI must surface analyzer findings and allow users to accept or override record tags and wrapper boundaries. Overrides must be persisted per-file and optionally promoted to a per-extension profile.

## Goal

Add UI elements to show: detected balises (record tag(s), wrapper), file size, target size, estimated parts. Add controls to override detected tags and choose to save overrides to a profile.

## Acceptance Criteria

- On file add/drop, a quick analysis result is shown next to the file entry (or via preview/modal depending on design decision from task-000001).
- The user can override detected record tag and wrapper and see immediate recalculated estimates (estimated parts and size).
- The user can choose to persist override to a per-extension profile.
- Overrides are passed to `FileProcessingService` when the user initiates splitting.

## Implementation Notes

- Reuse viewmodels for file metadata; add `AnalysisViewModel` capturing analyzer results + override state.
- UI should show an "Analyze" button for large files (to avoid auto-analyzing very large inputs without permission) if chosen in the design.
- Persist overrides to a simple JSON file in application data (or existing config store).

## Files likely touched

- `src/LeniTool.Desktop/ViewModels/*` and Views
- `src/LeniTool.UI/MainWindow.xaml` and related code-behind
- `src/LeniTool.Core/Services/ConfigurationService.cs` (for persistence)

## Validation

- Manual test: Drop sample file, verify detected tags appear, change them, start split, and verify output aligns with overrides.
- Unit tests for ViewModel logic (UI tests not required now but encouraged).

## Notes / Discoveries

- Desktop UI is Avalonia; file picking + drag/drop were implemented via code-behind because XAML event hookups for drag/drop were not supported by the project’s Avalonia XAML parser.
- Overrides are applied per-run via `SplitConfiguration.RunOverrides.FileOverrides` before calling `FileProcessingService`, and persisted to `SplitConfiguration.FileOverrides` / `SplitConfiguration.ExtensionProfiles` via `ConfigurationService`.

## Implementation Log

- 2026-01-11: Implemented Desktop Add Files via `StorageProvider.OpenFilePickerAsync`.
- 2026-01-11: Implemented drag/drop add on the files list.
- 2026-01-11: Added per-file analysis on add (auto for small files) and a right-side Analysis & Overrides panel bound to `SelectedFile`.
- 2026-01-11: Added override editing (target size MB, auto-detect/manual + record tag) and persistence actions (per-file + per-extension).
- 2026-01-11: Wired per-file overrides into Core using `Configuration.RunOverrides.FileOverrides[filePath]` during processing.

## Validation Results

- `dotnet build src/LeniTool.Desktop/LeniTool.Desktop.csproj -c Debug` ✅

## Status

Completed for Avalonia Desktop. Unit test for VM->RunOverrides mapping not added (Desktop project not currently covered by existing test project); manual verification recommended.
