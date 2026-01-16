# Fix Summary - LeniTool Execution

## Issues Fixed ✅

### 1. System.Text.Json Vulnerability (NU1903)
**Problem:** Package version 8.0.3 had a known high severity vulnerability  
**Solution:** Updated to version 9.0.1 (latest stable)  
**File:** [LeniTool.Core.csproj](src/LeniTool.Core/LeniTool.Core.csproj)

### 2. Legacy MAUI UI & Replacement
**Problem:** The legacy MAUI UI added platform complexity and required extra workloads which aren't needed for this project  
**Solution:** Replaced with a focused Avalonia Desktop UI and a CLI version (already created)  
**Benefit:** No platform dependencies, simpler to build and distribute, and suitable for automation

## How to Run Now

### ✅ CLI Version (Recommended)

```powershell
# Option 1: Run directly (development)
dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- -i sample.html -o output

# Option 2: Build standalone executable
dotnet publish src/LeniTool.Cli/LeniTool.Cli.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist

# Then use it:
./dist/LeniTool.Cli.exe -i sample.html -o output
```

### Examples

```bash
# Process one file
dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- -i yourfile.html

# Process all files in a directory
dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- -i "C:\RSP\Files" -o output

# Sequential processing (no parallel)
dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- -i files/ -o output/ -p false
```

## Test It Now

A sample HTML file was created for testing:

```powershell
# Test the CLI with the sample file
dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- -i sample.html -o test-output
```

This should:
1. Build successfully (no more vulnerability warnings)
2. Process the sample.html file
3. Create output in `test-output/` directory

## Why CLI Instead of MAUI?

| Aspect | CLI | MAUI GUI |
|--------|-----|----------|
| **Installation** | Just .NET SDK | Requires MAUI workload + platform SDKs |
| **Complexity** | Simple | Complex (Android, iOS, Windows setup) |
| **Your Use Case** | ✅ Perfect for RSP batch processing | ❌ Overkill |
| **Automation** | ✅ Easy to script | ❌ Not designed for it |
| **Performance** | ✅ Lightweight | ❌ Heavier |

## Updated Documentation

- **[CLI_USAGE.md](CLI_USAGE.md)** - Complete CLI documentation with examples
- **[QUICKSTART.md](QUICKSTART.md)** - Updated to recommend CLI first
- **[README.md](README.md)** - Now features CLI prominently

## If You Still Want the GUI

The GUI is available as an Avalonia Desktop app in `src/LeniTool.Desktop/`.

```powershell
# Run the desktop GUI (development)
dotnet run --project src/LeniTool.Desktop/LeniTool.Desktop.csproj
```

**Recommendation:** Start with CLI or use the Desktop GUI for interactive use.

## Next Steps

1. ✅ Test CLI with sample file
2. ✅ Process your real RSP files
3. ✅ Adjust configuration in code if needed (segmentation tags)
4. ✅ Build standalone executable for distribution

## Quick Test Command

Run this now to verify everything works:

```powershell
dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- -i sample.html -o test-output
```

Expected output:
```
Processing 1 file(s) -> test-output (parallel=true)
[sample.html] Starting split operation... 0%
[sample.html] Complete - 1 chunks created in 0.XX s 100%
--- Results ---
File: C:\...\sample.html => Success: True OutputCount: 1
```

(The sample is small, so it won't be split. Try with a larger file to see splitting in action!)
