# Example file generator

This folder contains a script to generate **large, spec-accurate** input files for LeniTool.

It creates:
- Markup-like `.txt` files with a wrapper element and many repeated record tags (for the `.txt` strategy)
- A large `.html` file with repeating `<div class="page">…</div>` (for the HTML strategy)

## Usage

From repo root:

- Generate all example files around ~10MB each:

`powershell -ExecutionPolicy Bypass -File .\Example\generate-examples.ps1 -TargetSizeMB 10 -Variant all`

- Generate only the UTF-8 markup `.txt` example (~10MB):

`powershell -ExecutionPolicy Bypass -File .\Example\generate-examples.ps1 -TargetSizeMB 10 -Variant txt-utf8`

- Generate only the UTF-16 LE + BOM markup `.txt` example (~10MB):

`powershell -ExecutionPolicy Bypass -File .\Example\generate-examples.ps1 -TargetSizeMB 10 -Variant txt-utf16`

Output files go to `Example/generated/` by default.

## Notes

- Generation is **streaming** (does not build the full content in memory).
- The `.txt` files are shaped like:
  - Wrapper: `<Example> ... </Example>` or `<Root> ... </Root>`
  - Records: many repeated `<Ficher>...</Ficher>` / `<Item>...</Item>` blocks
- The `txt-two-tags` variant is designed to test **auto-detect vs manual override**:
  - Lots of `<A>...</A>` (auto-detect likely picks this)
  - Some `<B>...</B>` (override to this in UI/config to test manual mode)
- The `txt-single-record` variant is designed to trigger the “single record larger than target” behavior.

