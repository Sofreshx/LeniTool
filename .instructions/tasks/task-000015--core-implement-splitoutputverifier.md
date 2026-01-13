---
schema: task/v1
id: task-000015
title: "Core: Implement SplitOutputVerifier (equivalence checker)"
type: feature
status: done
priority: high
owner: "dev-handle"
skills: ["csharp-expert", "testing-dotnet-unit"]
depends_on: []
next_tasks: ["task-000016"]
created: "2026-01-12"
updated: "2026-01-13"
---

## Context

When splitting files into chunks and then reassembling or comparing outputs, we need a robust verifier that can assert the produced chunks are equivalent to the original input with clear diagnostic info on first differences.

## Goal

Add a `SplitOutputVerifier` that, for TXT strategy, does the following checks:
- Verifies prefix/suffix bytes match per chunk where applicable.
- Verifies concatenated inner bytes from chunks match the original file's inner bytes exactly.
- Reports first-diff byte offset and which chunk fails.

Decide an initial exposure: either a small console tool project to run locally, or a test-only helper with a future UI button to invoke verification.

## Acceptance Criteria ✅

- A verifier implementation exists and can be run locally to inspect split output vs source.
- Output is clear: pass/fail, failing chunk id, first diff offset, short hexdump around the diff.
- Tests exist that validate verifier behavior on known-good and known-bad split outputs.

## Implementation Notes 🔧

Files / areas likely touched:
- New `SplitOutputVerifier` service under Core (e.g., `Services/SplitOutputVerifier.cs`).
- Console tool project (optional) like `LeniTool.Verifier/` with a simple CLI to point at source + output directory.
- Unit tests in `tests/` validating behavior.

Design decisions:
- Start with TXT strategy only; add markup-specific expectations (prefix/suffix offsets) as an enhancement.
- Prefer clear, actionable error messages for developers.

## Validation / Testing Steps 🧪

1. Create a known-good split output and run verifier → pass.
2. Mutate a chunk to introduce a small byte change and verify the verifier reports the first-diff offset and failing chunk.
3. Evaluate running the verifier from CI or locally as a dev tool.

## Dependencies

- Feeds `task-000016` (tests that run split then verify).

## Notes / Next Steps

- Decide whether to include a minimal CLI wrapper in the same repo or keep verifier as an internal test utility for now.

## Implementation / Validation Log

- Implemented `SplitOutputVerifier` in Core with streaming byte-compare:
	- Verifies per-chunk prefix/suffix equality vs source.
	- Verifies concatenated chunk-inner bytes equals source-inner bytes.
	- On failure reports: kind, chunk index/path, first diff offset (inner), expected/actual inner lengths, and small hex snippets.
- Added `TxtMarkupSplitBoundaryResolver` and refactored `TxtMarkupSplitterService` to use it so splitter/verifier share identical prefix/suffix boundary logic.
- Added unit tests covering known-good splits (UTF-8 + UTF-16 BOM) and a known-bad mutated chunk producing an inner mismatch diagnostic.

Validation:
- `dotnet test tests/LeniTool.Core.Tests/LeniTool.Core.Tests.csproj` (pass)
