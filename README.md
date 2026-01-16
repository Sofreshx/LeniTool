# LeniTool — Simple, focused README (condensed)

LeniTool is a small Windows desktop utility (Avalonia) for splitting large HTML or tagged TXT files into valid, size-limited chunks while preserving structure.

## Key points ✅
- Splits HTML near configurable segmentation tags and balances opening/closing tags.
- Splits tagged TXT files by a repeating "record" element (auto-detect or override).
- Default chunk size is configurable (example: 4.5 MB). Use `config.json` for overrides.
- No PDF support yet.

## Quick start (2 commands) ⚡
Prerequisite: .NET 8 SDK

Run in-place for development:
```powershell
    dotnet run --project src/LeniTool.Desktop/LeniTool.Desktop.csproj
```

Build a Windows single-file release:
```powershell
    dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Published exe: `src/LeniTool.Desktop/bin/Release/.../publish/`

## Usage (summary)
- Add files (Add Files / drag & drop)
- Optionally expand configuration (max chunk size, segmentation tags, output dir)
- Click "Process Files" and monitor progress and logs

## Configuration notes
- `config.json` is created next to the executable on first run and supports global, per-extension, and per-file overrides.
- Common settings: `maxChunkSizeMB`, `segmentationTags`, `openingTags`, `closingTags`, `outputDirectory`, `autoDetectRecordTag`, `recordTagName`.

## Development & tests
- Build: `dotnet build`
- Tests: `dotnet test` (core logic has unit tests)

## Support
Open an issue on GitHub if you hit a bug or want a feature.

---

This README consolidates the project’s essential usage and replaces separate long-form docs (QUICKSTART.md, BUILD.md, CLI_USAGE.md, PROJECT_SUMMARY.md, QUALITY_REVIEW.md).

Maintainers: keep this file short — move long how-tos into `docs/` if required.
