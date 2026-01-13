---
schema: task/v1
id: task-000013
title: "UI/Core integration: Preview expected chunks + detected balises on add"
type: feature
status: done
priority: high
owner: "dev-handle"
skills: ["csharp-expert"]
depends_on: ["task-000010","task-000012"]
next_tasks: []
created: "2026-01-12"
updated: "2026-01-13"
---

## Context

Users benefit from immediate, glanceable information after adding a file: an estimated number of chunks and the top detected record tag ("balise") with confidence scores.

## Goal

Surface an estimated chunk count immediately on file add (before the full analysis completes) and, after analysis, display the top detected tag and candidate tags in the file list row so users don't need to open the details panel to see key info.

## Acceptance Criteria ✅

- On add, an *estimated* chunk count is shown (based on file size and current split target size) in the file list row.
- After the analyzer runs, the file row displays the top detected record tag and a confidence metric; a small popup or detail view can show candidates.
- File list rows include: Size, Est Parts, Top Tag (if any), Status.
- No additional clicks required to see the quick preview; detailed information still available in the right panel.

## Implementation Notes 🔧

Files likely touched:
- `MainViewModel.cs` — compute and expose est parts; hook analyzer results to update row.
- Row templates in `MainWindow.axaml` — add columns for Est Parts and Top Tag.
- Analyzer output contracts — ensure analyzer returns candidate tag list + confidence.

## Validation / Testing Steps 🧪

1. Add several files of different sizes; verify the est parts appear immediately and update after analysis.
2. Run analyzer on nested / ambiguous inputs (see `Example/`) and verify top tag + candidates are displayed appropriately.
3. Ensure the UI remains responsive while the analysis runs.

## Dependencies

- Requires `task-000010` (size limits) and `task-000012` (drag/drop UX) to be in place for consistent add behavior.

## Notes / Next Steps

- Keep the UI compact: prioritize key info in the row, hide low-confidence items behind details.
- Consider adding telemetry or logs for analyzer confidence for future improvements.

## Implementation / Verification Log

- Verified file list row includes Size, Est Parts, Top Tag, Status columns and binds to `EstimatedPartCount` and `TopCandidateDisplay`.
- Verified est parts appears immediately on add (computed from file size + per-file effective target size via `ApplyDefaultsFrom`).
- Verified top candidate tag + confidence appears after `Analysis` is assigned; candidate list remains visible in the right panel.
- Small UI wording tweak: changed "Re-analyze" action label/status copy to "Analyze" for first-time analysis clarity.

## Validation

- `dotnet build src/LeniTool.Desktop/LeniTool.Desktop.csproj` (Windows) — success.