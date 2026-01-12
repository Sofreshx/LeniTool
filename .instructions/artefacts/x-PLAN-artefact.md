# x-PLAN: Flexible Splitter for LeniTool ✅

## Problem statement

Current splitter assumes well-formed HTML and a configured record tag. New requirement: support flexible text-based markup (e.g., `.txt`, pseudo-HTML, mixed markup) where record boundaries and wrappers are not known in advance. The system must: detect repeating record structures robustly (streaming-friendly), split by byte-targets without reserializing record contents, be extensible (future PDF/binary support), and provide an operator UX to review and override analysis before splitting.

---

## Glossary

- **Wrapper / Prefix**: Leading bytes appearing once at file start before first record (e.g., header, prolog).
- **Suffix**: Trailing bytes appearing once at end after last record (e.g., footer).
- **Record element**: The atomic repeating unit to be grouped into output parts.
- **Balise**: (alias) a tag-like marker that can denote record boundaries in markup-style text.
- **Candidate tags**: Tag names / markers discovered repeatedly during analysis and proposed as record selectors.
- **Profile / Override**: Saved per-extension or per-file configuration that selects strategies, record selectors, wrapper rules, and splitting options.

---

## Proposed architecture 🏗️

### Goals
- Minimal changes to existing HTML path; preserve current behavior for `.html` / `.htm`.
- Add a pluggable analyzer/strategy model so `.txt` and future formats can use specialized analyzers.

### Core abstractions
- **`ISplitStrategy`** (interface): Given an input stream + `SplitProfile`, yields a sequence of byte spans or complete `SplitPlan` describing part boundaries. Implementations are responsible for encoding-aware byte handling and not reserializing record bodies.
- **`IDocumentAnalyzer`** (interface): Scans a (streaming) sample to produce an `AnalysisResult` with candidate tags, wrapper spans, confidence scores, and suggested `SplitProfile` defaults.
- **`SplitPlan` / `AnalysisResult`**: Immutable DTOs carrying detection results: prefix span, list of record spans (start+end), suffix span, candidate tags, confidence metrics, and profile suggestion.
- **Per-extension `SplitProfile`**: Declarative settings: default target bytes, record selector (tag/matcher), wrapper behavior, encoding, fallback mode, and persistable name.

### Strategy selection
- Selection by file extension (initial design):
  - `.html` / `.htm` -> existing HTML strategy (unchanged behavior)
  - `.txt` -> `MarkupAnalyzer` + `RecordChunker` strategy
  - `pdf` -> `IPdfSplitStrategy` placeholder + NOT IMPLEMENTED (future extension point)

---

## Algorithms (high level) ⚙️

### Detection
- Streaming scan (single-pass when possible), sample-limited for very large files.
- Track candidate markers (balises/tags/text markers) and their frequency and span patterns.
- Identify wrapper (prefix) as the data before the first recurring record start; suffix as data after last recurring record end.
- Confidence scoring uses heuristics: repetition count, coverage (percent bytes/records covered by candidates), inter-record consistency (attribute patterns, similar length ranges), and alignment of start/end markers.
- Present top candidate tags sorted by confidence.

### Splitting
- Work in original byte-space (respecting encoding & BOM): splitter constructs parts by concatenating prefix + N whole record spans + suffix.
- Accumulate whole record spans until parts reach or slightly exceed target bytes (configurable bias) — do not split inside a record.
- Output is exact byte slices from the input (no reserialization) to preserve original formatting and encoding.
- For encodings that alter byte-lengths per character (UTF-8/UTF-16), calculations are done on bytes; analyzers must detect encoding early.

### Fallback modes
- If no tag-like candidates found or confidence low: fallback to line-based chunks (lines or paragraph blank-line heuristics).
- If single-record files: produce one part with original file; warn user if > target size.

---

## UX spec 🎛️

- On file drop/add: automatic (sample) analysis runs (unless file is huge—see safety rules). Show an analysis card with:
  - **File size**, **target size**, **estimated number of parts** (based on current target)
  - **Candidates**: tag/marker name, count, sample snippet, confidence score
  - **Wrapper**: detected prefix/suffix bytes preview (small snippet)
  - **Suggested profile** and quick actions: Preview, Accept & Split, Open Override UI
- Override UI: select one or more record selector(s), set wrapper handling (keep, trim, custom template), adjust target bytes, name & save profile.
- Preview: show first N parts (as text) or first/last bytes per part and expected file sizes before commit.
- Safety: large file (> configurable threshold) shows a prominent `Analyze` button (no auto-on-drop) and a progress bar; long runs offloaded to worker threads with cancel.
- Confidence warnings: if detection confidence < threshold, require explicit confirmation before splitting.

---

## Data model: config JSON (proposed additions)

Add top-level `splitProfiles` and `fileOverrides` sections:

- `splitProfiles` (array of profiles)
  - `name` (string)
  - `extensions` (array of strings)
  - `recordSelectors` (array of tag/marker descriptors or simple strings)
  - `wrapper` (object: how to detect or explicit prefix/suffix markers)
  - `encoding` (optional override, e.g., `utf-8`, `utf-16`)
  - `targetBytes` (number)
  - `fallback` (enum: `line`, `paragraph`, `fail`)
  - `confidenceThreshold` (0-1)

- `fileOverrides` (map pattern -> profileName / inline overrides)

Profiles are persisted in the repo or user config (choose per project scope).

---

## Validation & test matrix ✅

- **Canonical sample**: use the user's sample `\<Example\>/\<Ficher\>` (a file with a wrapper `<Example>` containing multiple `<Ficher>` records) and verify:
  - Records detected as `<Ficher>`/`<Fichier>`; wrapper recognized as surrounding `<Example>` container.
  - Splits produce parts: `prefix + N records + suffix` with target-based grouping.
- **Whitespace and attribute variance**: tags like `<Example id="1">` vs `<Example   >` — must match candidate tag names ignoring attribute differences.
- **Malformed tags**: missing close tags, stray `<` — analyzer should lower confidence and fall back to line-splitting if necessary.
- **Nested candidate tags**: ensure correct record granularity (outer vs inner record preference) and allow operator override.
- **Encoding tests**: `utf-8`, `utf-8 with BOM`, `utf-16 LE/BE` — verify byte-aware splitting and preserved encoding in outputs.
- **Large-file performance**: multi-GB test file — ensure streaming analysis, configurable sampling, progress reporting, and memory boundedness.
- **Binary or non-text files**: detect and skip with clear error.
- **Single-record file**: results in single part; confirm behavior remains consistent with UI warning when size > target.
- **Regression tests**: existing HTML behavior for `.html`/.`htm` must be identical to current outputs.

Test matrix matrix axes: format variants × size × encoding × malformation levels. For each case verify: detection accuracy, confidence score, output byte-exactness, and UI feedback.

---

## Risks, mitigations & rollback ⚠️

- Risk: **False positives / wrong detection** → Mitigation: conservative confidence scoring, preview + explicit accept, operator overrides, and ability to fallback to line splitting.
- Risk: **Lossy output / encoding corruption** → Mitigation: operate on byte spans, preserve original encoding, add unit tests for encodings, and integration tests validating checksums of outputs.
- Risk: **Performance on huge files** → Mitigation: streaming sampling, progress UI, async worker, and configurable sample sizes; set a runtime guard and fail-safe cancel.
- Risk: **User confusion** → Mitigation: clear UI wording, show sample snippets, and allow quick revert by keeping original files until user confirms; add audit logs.

Rollback strategy
- Keep default behavior unchanged for `.html`/.`htm` (backwards compatible).
- Feature toggle: gate the new `.txt` analyzer behind a config flag; default to `line` fallback until tests pass.
- Instrumentation: add metrics and logging to detect mis-splits in the field; ability to roll back profile defaults and remove the new analyzer if widespread issues occur.

---

## Acceptance criteria

- Analysis UI shows meaningful candidates & confidence for representative `.txt` and `.html` files.
- Operator can override and save a `SplitProfile` and use it to split reproducibly.
- Splits are byte-exact slices (no reserialization) and respect encoding.
- Large files analyze and split with bounded memory and visible progress.
- All test matrix items pass; HTML behavior remains unchanged.

---

## Next steps & low-effort milestones ✨

1. Implement `AnalysisResult` model and a simple `MarkupAnalyzer` that counts candidate tags in streaming sample.
2. Wire a `SplitProfile` config and an on-disk profile persistence path.
3. Add UI card for analysis results and basic override modal with preview.
4. Create test fixtures from the test matrix and add unit/integration tests.
5. Performance benchmarking with multi-GB synthetic files.

---

## Phase: UI modernization + file size limits + richer preview + verification tooling 🔧

**Summary:** Deliver a modern dark UI, robust file-size controls, richer inline preview and drag/drop reliability, example generator enhancements, and a `SplitOutputVerifier` to validate split outputs for TXT/markup strategies.

### UI requirements ✅
- Adopt dark/modern theme: **Avalonia Fluent Dark** as the primary theme, with accessible contrast and consistent typography.
- Improved layout: cleaner file list (sortable, filterable), persistent right-side analysis panel remains; add a compact toolbar for common actions (Analyze, Preview, Split).
- Visual feedback: per-file status badges (Analyzed | Pending | Too Large | Error), progress bars, and inline action affordances.

### File size controls ⚠️
- Two limits:
  - **Hard cap**: `MaxInputFileSize` (default example: 4 GB) — reject files over cap with a clear error and link to docs.
  - **Auto-analyze threshold**: `AutoAnalyzeThreshold` (default example: 50 MB) — files larger than threshold are added but analysis is paused; user must click **Analyze** to start.
- Behavior: when the auto-analyze threshold is exceeded show a clear warning and an "Analyze anyway" button; analysis runs in a cancellable worker with progress reporting.

### Drag & drop 📥
- Support both `DataFormats.Files` and `DataFormats.FileNames` to maximize platform compatibility.
- Allow drop target on the whole window and on the files panel; show a visual drop target overlay and animate accepted files.
- If a dropped file exceeds `MaxInputFileSize`, reject immediately with a helpful message.

### Preview & file list enhancements 🔍
- File list shows:
  - Estimated part count
  - Top recognized record tag candidate + confidence (if analyzed)
  - If not analyzed, show a fallback estimated part count based on size (size / `targetBytes`) and mark it as an "estimate".
- Quick inline actions: Analyze, Preview, Open Override UI.

### Examples & generator ✨
- Enhance example generator to produce:
  - Nested wrapper inputs (envelope/header/payload/footer) with multiple wrapper layers.
  - Edge cases: single-record files, zero-record wrappers, overlapping/malformed wrappers, and highly repetitive noise.
- Add generator flags to control nesting depth, noise level, and record size variance.

### Verification: `SplitOutputVerifier` algorithm 🧪
- **Purpose:** Validate outputs of TXT/markup `SplitStrategy` are byte-equivalent to the original middle content and preserve prefix/suffix.
- **Algorithm (concise):**
  1. Determine prefix and suffix boundaries and record-tag selection using the same detection rules as the splitter.
  2. For each output chunk:
     - Verify the chunk starts with the original prefix bytes and ends with the original suffix bytes.
     - Strip prefix/suffix from the chunk to obtain inner bytes for that chunk.
  3. Concatenate inner bytes from all chunks in order and assert the result equals the original middle bytes (the bytes between `prefixEnd` and `suffixStart`) exactly.
  4. On mismatch, report diagnostics: first differing offset, original vs reconstructed lengths, index of the failing chunk, and a small hex/snippet diff around the difference.
- Integrate verifier as an optional post-split UI step and as an automated test harness for split strategies.

### Validation checklist ✅
- **Manual UX checks:** theme applied, drop overlay works, files panel shows estimates and candidates, analyze workflow for large files, explicit warnings for hard cap.
- **Automated tests:**
  - Unit tests for UI logic (estimates, badges, drop handling), analyzer thresholds, and generator outputs.
  - Integration tests: run sample splits with `SplitOutputVerifier`, include nested wrappers and edge cases, assert verifier passes and mismatches produce expected diagnostics.
  - Performance tests around thresholds and cancellation behavior.

---

> Note: This plan is intentionally implementation-agnostic and focused on behaviour, UX, and validation.

---

*Document created for planning: `.instructions/artefacts/x-PLAN-artefact.md`*