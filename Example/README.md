# Example file generator

This folder contains a script to generate **large, spec-accurate** input files for LeniTool.

It creates:
- Markup-like `.txt` files with a wrapper element and many repeated record tags (for the `.txt` strategy)
- A large `.html` file with repeating `<div class="page">…</div>` (for the HTML strategy)

## Usage

From repo root:

- Generate all example files around ~10MB each:

`powershell -File .\Example\generate-examples.ps1 -TargetSizeMB 10 -Variant all`

- Generate only the UTF-8 markup `.txt` example (~10MB):

`powershell -File .\Example\generate-examples.ps1 -TargetSizeMB 10 -Variant txt-utf8`

- Generate only the UTF-16 LE + BOM markup `.txt` example (~10MB):

`powershell -File .\Example\generate-examples.ps1 -TargetSizeMB 10 -Variant txt-utf16`

- Generate a nested envelope markup `.txt` example (~1MB):

`powershell -File .\Example\generate-examples.ps1 -TargetSizeMB 1 -Variant txt-nested-envelope-basic`

Output files go to `Example/generated/` by default.

If PowerShell blocks local scripts in your environment, you may need to allow local script execution (e.g., set the current user policy to `RemoteSigned`) according to your organization’s security guidance.

## Notes

- Generation is **streaming** (does not build the full content in memory).
- The `.txt` files are shaped like:
  - Wrapper: `<Example> ... </Example>` or `<Root> ... </Root>`
  - Records: many repeated `<Ficher>...</Ficher>` / `<Item>...</Item>` blocks
- The `txt-two-tags` variant is designed to test **auto-detect vs manual override**:
  - Lots of `<A>...</A>` (auto-detect likely picks this)
  - Some `<B>...</B>` (override to this in UI/config to test manual mode)
- The `txt-single-record` variant is designed to trigger the “single record larger than target” behavior.

## Nested markup variants

These variants are designed to exercise wrapper reconstruction and record detection when wrappers and records are nested or when non-record “noise” appears between records.

- `txt-nested-envelope-basic` -> `example-nested-envelope-utf8-{N}mb.txt`
  - Envelope + header/payload/footer with repeated record tags inside payload.
- `txt-nested-envelope-with-noise-outside-records` -> `example-nested-envelope-noise-utf8-{N}mb.txt`
  - Same envelope shape, but includes deterministic non-record tags/comments mixed alongside records.
- `txt-multi-depth-wrappers-depth3` -> `example-nested-depth3-utf8-{N}mb.txt`
  - Three nested wrapper layers around a repeated record tag.
- `txt-interleaved-nonrecord-tags-between-records` -> `example-interleaved-noise-utf8-{N}mb.txt`
  - Inserts non-record tags between records to test “ignore noise” behavior.
- `txt-nested-records-inner-repeats` -> `example-nested-records-lines-utf8-{N}mb.txt`
  - Records contain repeated inner tags (e.g., multiple `<Line>` entries) to test outer-vs-inner record selection.
- `txt-nested-records-same-tag-name` -> `example-nested-same-tag-utf8-{N}mb.txt`
  - Outer record tag name is the same as nested inner tag name (intentionally confusing for auto-detect).
- `txt-records-at-mixed-depths` -> `example-mixed-depth-records-utf8-{N}mb.txt`
  - Some records are top-level while others are nested inside wrapper groups.

