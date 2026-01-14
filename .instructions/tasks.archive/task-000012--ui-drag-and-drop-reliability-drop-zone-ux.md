---
schema: task/v1
id: task-000012
title: "UI: Drag-and-drop reliability + drop-zone UX"
type: feature
status: archived
priority: medium
owner: "dev-handle"
skills: ["csharp-expert", "testing-frontend-unit"]
depends_on: ["task-000010"]
next_tasks: ["task-000013"]
created: "2026-01-12"
updated: "2026-01-14"
---

## Context

Drag-and-drop currently works inconsistently when dragging from Explorer to parts of the window; also feedback when dragging is minimal.

## Goal

Improve drag-and-drop so it reliably accepts files from Explorer across the whole window (or on a dedicated drop zone), supports both `DataFormats.Files` and `DataFormats.FileNames`, and shows clear visual affordance when the user is dragging files.

## Acceptance Criteria ✅

- Dragging .txt/.html files from Explorer onto the window or the drop zone works consistently.
- Both `DataFormats.Files` and `DataFormats.FileNames` are supported to maximize compatibility.
- Visual feedback (highlighted drop zone or overlay) appears while dragging and indicates allowed/rejected state.

## Implementation Notes 🔧

Files likely touched:
- `MainWindow.axaml` / `MainWindow.axaml.cs` — add drop handlers and visual overlays.
- `MainViewModel.cs` — reuse size threshold logic from `task-000010` for immediate rejection.
- Consider adding a small overlay control that centralizes the drag visual affordance.

## Validation / Testing Steps 🧪

1. Drag `.txt` and `.html` files from Explorer and drop them on several window areas — they should be accepted if under the Max size.
2. Drag a file above the Max size — overlay shows a denied state and drop results in a rejection log.
3. Test on Windows Explorer and verify both DataFormats are handled.

## Dependencies

- Must come after `task-000010` (size limits & thresholds) for consistent rejection logic.

## Notes / Next Steps

- Consider adding quick hints in the UI (e.g., "Drop files here or click Add") when UX changes are live.

## Implementation Log

- 2026-01-13: Enabled window-wide drop target (`DragDrop.AllowDrop` on `MainWindow`) and routed drag/drop handlers on the window so dropping works reliably across the whole UI.
- 2026-01-13: Added a centered overlay that appears during drag and indicates allowed vs rejected state; rejects when drag data isn’t files or when files exceed `MaxInputFileSizeBytes` (drop is still allowed so the rejection is logged consistently).
- 2026-01-13: Added support for both `DataFormats.Files` and `DataFormats.FileNames` and filters dropped items to supported extensions (`.txt`, `.html`, `.htm`).

## Validation

- `dotnet build src/LeniTool.Desktop/LeniTool.Desktop.csproj` (Windows) ✅