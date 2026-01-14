---
schema: task/v1
id: task-000001
title: "Explore UI: UI drop/add flow & decide where analysis is surfaced"
type: research
status: archived
priority: medium
owner: "dev-handle"
skills: ["planning-feature", "react-query"]
depends_on: []
next_tasks: ["task-000006"]
created: "2026-01-11"
updated: "2026-01-14"
---

## Context

When users add or drop files into the UI we should analyze file contents (auto-detect record/wrapper balises), show size/estimated parts and allow overrides prior to processing. The current UI drop/add flow is implemented in the Desktop/UI projects (search for file open/drop handlers and drag/drop code paths).

## Goal

Decide where and how detected analysis (detected record tag(s), wrapper prefix/suffix offsets, file size, estimated parts) will be surfaced in the UI, and provide a small design and integration plan for implementation.

## Acceptance Criteria

- A short design doc (1 page) exists describing where the analysis will appear (file list row, modal, side panel, or preview), how overrides are exposed, and how overrides persist.
- A list of concrete UI files and components to modify is produced.
- A recommended UX for override (inline editable fields + confirm) is chosen.
- Dependencies and impacts on backend APIs and configuration model are identified.

## Implementation Notes

- Inspect `LeniTool.Desktop` and `LeniTool.UI` projects for the file drop/add handlers.
- Consider whether analysis should be synchronous on drop (quick heuristics) or asynchronous (spinner with results). For large files prefer async streaming analysis.
- Decide how to represent multi-file selections.

## Files likely touched

- `src/LeniTool.Desktop/*` (Views / ViewModels)
- `src/LeniTool.UI/*` (MainWindow, ViewModels)
- `src/LeniTool.Core/Services/FileProcessingService.cs`

## Validation

- Provide a short screen mock or wireframe showing the UI with detected tags and override controls.
- Walk through an example file (provided by core team) to show where the analysis appears and how a user overrides detected tags.

## Next Steps

After agreement, create work items to implement the UI changes and hook them to the underlying core changes (tasks listed next).

## Results

### Summary / key context

- Product UI is Avalonia Desktop in `src/LeniTool.Desktop` (included in the solution). The WPF project in `src/LeniTool.UI` exists but is not wired into the solution.
- Current “Add files” plumbing is stubbed in Desktop; there is no drag/drop implementation yet.
- Main Desktop VM: `src/LeniTool.Desktop/ViewModels/MainViewModel.cs`; Main view: `src/LeniTool.Desktop/Views/MainWindow.axaml`.

### UI design (1 page)

#### Where analysis appears (chosen)

Use a **right-side “Analysis & Overrides” panel** that reflects the currently selected file in the file list.

Rationale:
- Scales to multi-file selections without forcing a modal-per-file.
- Allows “review then split” workflow while keeping the file list visible.
- Keeps long/technical details (candidates, confidence, wrappers) out of the main list row while still being discoverable.

Proposed layout:
- Left: file list (rows show: file name, size, analysis status icon, estimated parts, warnings).
- Right: analysis panel (shown when a file is selected), containing:
	- File summary: size, detected encoding (if available), current target bytes, estimated parts.
	- Detection results: top candidate record markers/tags with counts and confidence.
	- Wrapper info: “prefix/suffix detected” with tiny preview snippets (first/last N bytes as safe text).
	- Actions: `Analyze` (if not done), `Re-analyze`, `Accept`, `Split Selected`, `Split All`, `Cancel` (during analysis).

#### How overrides are exposed

Overrides are edited in the same right panel via an **“Overrides” section** (no modal) with explicit “Apply”:
- `Record selector`: dropdown of detected candidates + “Custom…” input.
- `Target size`: numeric input (bytes/MB) affecting estimated parts.
- `Wrapper handling`:
	- Keep detected prefix/suffix (default)
	- Ignore wrapper (split only records)
	- Custom prefix/suffix (advanced; optional v1)
- `Save as profile` (optional v1): name + apply-to-extension toggle.

Persistence rules:
- **Per-file overrides** live in the UI state until app close (or until file removed).
- Optional follow-on: persist user defaults via config (`SplitProfile` concept in the plan artefact) keyed by extension.

#### Async behavior

Analysis must be **async and cancelable**:
- On drop/add: start analysis automatically for “reasonable” file sizes; for very large files show “Not analyzed (click Analyze)”.
- Run analysis on a background thread (`Task.Run`) and marshal results back to UI thread.
- Concurrency: allow parallel analysis with a small cap (e.g., 2) to keep UI responsive.
- UI feedback per file: `Pending` → `Analyzing` (spinner + progress if available) → `Analyzed` or `Failed`.
- Cancellation: one global cancel for “analyze all”, plus per-file cancel if feasible.

#### Huge-file safety

Safety goals: bounded memory, avoid reading full file into RAM, avoid UI lockups.

Rules:
- Hard threshold (configurable): above e.g. 250MB–1GB, do not auto-analyze on add; require explicit “Analyze”.
- Analyzer uses streaming/sample-based analysis:
	- Read first N MB and last N MB (or first N MB only in v1) instead of whole file.
	- Keep only small rolling buffers needed for tag/marker detection.
- For preview snippets: never render entire wrapper/records; cap to e.g. 2–8KB, escape non-printables.
- Never compute “estimated parts” by counting all records for huge files; estimate via sampled record size and total bytes.

### Files to modify (implementation plan)

- `src/LeniTool.Desktop/ViewModels/MainViewModel.cs`
	- Add `ObservableCollection<FileItemViewModel>` (or equivalent) with per-file analysis state.
	- Add commands: `AddFiles`, `AnalyzeSelected`, `AnalyzeAll`, `CancelAnalysis`, `SplitSelected`, `SplitAll`.
- `src/LeniTool.Desktop/Views/MainWindow.axaml`
	- Add drag/drop handlers, file list, and right-side analysis/overrides panel bindings.
- `src/LeniTool.Desktop/Views/MainWindow.axaml.cs` (or appropriate code-behind)
	- Wire Avalonia drag/drop events and forward to VM.
- `src/LeniTool.Desktop/Program.cs` / DI setup (if present)
	- Register core services into the VM (or construct them safely).

Optional (if we decide to persist profiles in config now):
- `src/LeniTool.Core/Models/SplitConfiguration.cs` (extend to include optional profile/default knobs)
- `src/LeniTool.Core/Services/ConfigurationService.cs` (load/save profile defaults)

### Backend/config dependencies

Minimum (v1: UI-only + reuse existing core splitting):
- Use existing `LeniTool.Core` services for actual splitting (e.g., `FileProcessingService`, `HtmlSplitterService`).
- Add a new “analysis” method/service only if none exists yet; should accept a `Stream` and return an `AnalysisResult`-like DTO.

If aligning with the plan artefact (recommended direction):
- Add/introduce an `AnalysisResult` DTO + analyzer service interface in Core.
- Extend config to support default target bytes + optional per-extension profile/thresholds.

### Validation checklist

- [ ] Drop a small `.html` file: analysis shows quickly; selector defaults make sense; split works.
- [ ] Drop multiple files: left list shows independent analysis state; selecting a row updates the right panel.
- [ ] Large file over threshold: no auto-analysis; user can click `Analyze`; UI stays responsive.
- [ ] Cancel analysis mid-way: state becomes `Canceled` (or returns to `Not analyzed`) without crashing.
- [ ] Override record selector + target size: estimated parts updates; split uses the override.
