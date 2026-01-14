---
schema: task/v1
id: task-000011
title: "UI: Modern dark theme + layout refresh"
type: feature
status: archived
priority: medium
owner: "dev-handle"
skills: ["csharp-expert", "testing-frontend-unit"]
depends_on: []
next_tasks: []
created: "2026-01-12"
updated: "2026-01-14"
---

## Context

The current UI styling is functional but dated. A cohesive dark theme and refreshed layout will improve readability and first impressions.

## Goal

Adopt a modern dark theme (Avalonia Fluent dark or equivalent), refresh spacing/typography, and improve the file list presentation (modern columns/rows, clearer status badges).

## Acceptance Criteria ✅

- App uses a cohesive dark theme across all windows and components.
- Typography, spacing, and color choices improve contrast and readability at typical window sizes.
- File list uses an improved presentation (clear columns for Name, Size, Est Parts, Top Tag, Status) and modern visuals for status/badges.
- No functional regressions in behavior or accessibility (contrast ratios acceptable).

## Implementation Notes 🔧

Files likely touched:
- `App.axaml` / `App.xaml` — theme resource registration and global styles.
- `MainWindow.axaml` — update layout and data templates for file rows.
- `MainViewModel.cs` — ensure new row fields are exposed as needed.
- Styles resources (create `Styles/Theme.axaml` or similar).

## Validation / Testing Steps 🧪

1. Launch app in dark mode; verify consistent colors and readable text.
2. Check file rows render correctly at different widths and with long names.
3. Run a quick smoke test to ensure no functional changes (file add/analysis still works).

## Dependencies

- None specifically; this is a UI-only modernization task.

## Notes / Next Steps

- Consider adding a light-theme toggle in settings later if requested.

## Implementation Log

- 2026-01-12: Switched Desktop app default to dark theme via `RequestedThemeVariant="Dark"`.
- 2026-01-12: Added shared styling resources in `src/LeniTool.Desktop/Styles/Theme.axaml` and removed hard-coded section border colors.
- 2026-01-12: Refreshed `MainWindow.axaml` layout (header/action bar, cards, main content scrolling) and upgraded the file list to columns: Name, Size, Est Parts, Top Tag, Status.
- 2026-01-12: Implemented status “badge” visuals (background/foreground) using VM-provided brushes to avoid Avalonia XAML trigger / compiled-binding limitations.

## Validation

- `dotnet build src/LeniTool.Desktop/LeniTool.Desktop.csproj` (PASS)