# LeniTool CLI - Command Line Usage

## Overview

The CLI version of LeniTool is a simple, fast command-line tool for splitting large HTML files. **This is the recommended way to use LeniTool** as it requires no GUI dependencies and is perfect for automation and batch processing.

## Installation

### Option 1: Run with .NET SDK (Development)

No installation needed, just run:
```bash
dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- [arguments]
```

### Option 2: Build Standalone Executable (Recommended)

Create a single-file executable that includes everything:

**Windows:**
```powershell
dotnet publish src/LeniTool.Cli/LeniTool.Cli.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist
```

**Linux:**
```bash
dotnet publish src/LeniTool.Cli/LeniTool.Cli.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o dist
```

**macOS:**
```bash
dotnet publish src/LeniTool.Cli/LeniTool.Cli.csproj -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o dist
```

Then use the executable directly:
```bash
./dist/LeniTool.Cli [arguments]
```

## Usage

### Basic Syntax

```bash
LeniTool.Cli -i <input> [-o <output-dir>] [-p true|false]
```

### Arguments

| Argument | Alias | Required | Description | Default |
|----------|-------|----------|-------------|---------|
| `-i` | `--input` | ✅ Yes | Input file or directory path | - |
| `-o` | `--output` | ❌ No | Output directory for split files | `output` |
| `-p` | `--parallel` | ❌ No | Enable parallel processing (true/false) | `true` |

## Examples

### Process a Single File

```bash
# Using dotnet run
dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- -i myfile.html

# Using standalone executable
./LeniTool.Cli -i myfile.html
```

Output files will be in `./output/`:
- `myfile_part001.html`
- `myfile_part002.html`
- etc.

### Process Multiple Files in a Directory

```bash
# Process all HTML files in a directory
dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- -i "C:\RSP\Files" -o "C:\RSP\Output"

# Using standalone executable
./LeniTool.Cli -i /path/to/html/files -o /path/to/output
```

### Custom Output Directory

```bash
dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- -i myfile.html -o split_results
```

### Disable Parallel Processing

For systems with limited resources or to process files sequentially:

```bash
dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- -i files/ -o output/ -p false
```

## Configuration

The CLI uses the default configuration from `config.template.json`:
- **Max chunk size:** 4.5 MB
- **Segmentation tags:** `<section`, `<div class="page"`, `<div`, `<article>`
- **Opening tags:** `<html>`, `<body>`
- **Closing tags:** `</body>`, `</html>`

To customize, the CLI will look for `config.json` in the executable directory (planned feature).

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Error (invalid arguments, file not found, processing failed) |

## Output

The CLI provides real-time progress updates:

```
Processing 3 file(s) -> C:\output (parallel=true)
[file1.html] Starting split operation... 0%
[file1.html] Created chunk 1/3 33.33%
[file1.html] Created chunk 2/3 66.67%
[file1.html] Complete - 3 chunks created in 0.45s 100%
[file2.html] Starting split operation... 0%
...
--- Results ---
File: C:\file1.html => Success: True OutputCount: 3
File: C:\file2.html => Success: True OutputCount: 2
File: C:\file3.html => Success: False OutputCount: 0
  Error: Invalid HTML structure
```

## Common Use Cases

### Batch Processing RSP Files

```powershell
# Process all RSP files in a folder
LeniTool.Cli -i "C:\RSP\Monthly\2024-01" -o "C:\RSP\Split\2024-01"
```

### Automation / Scripting

**PowerShell script:**
```powershell
# Process multiple directories
$folders = @("Jan", "Feb", "Mar")
foreach ($folder in $folders) {
    LeniTool.Cli -i "C:\RSP\$folder" -o "C:\RSP\Split\$folder"
}
```

**Bash script:**
```bash
#!/bin/bash
# Process files and check for errors
./LeniTool.Cli -i input/ -o output/
if [ $? -eq 0 ]; then
    echo "Processing completed successfully"
else
    echo "Processing failed" >&2
    exit 1
fi
```

### CI/CD Integration

```yaml
# GitHub Actions example
- name: Split HTML files
  run: |
    dotnet run --project src/LeniTool.Cli/LeniTool.Cli.csproj -- \
      -i ${{ github.workspace }}/data \
      -o ${{ github.workspace }}/output
```

## Troubleshooting

### "Input is required"
You must specify an input file or directory with `-i`:
```bash
LeniTool.Cli -i yourfile.html
```

### "Path not found"
Ensure the file or directory exists and the path is correct:
```bash
# Use absolute paths if needed
LeniTool.Cli -i "C:\Full\Path\To\file.html"
```

### "No HTML files found"
The tool only processes `.html` and `.htm` files. Check:
- File extensions are correct
- Files exist in the specified directory
- You have read permissions

### "Validation errors"
The tool found issues with your files:
- Ensure they are valid HTML files
- Check file extensions

## Performance

- **Parallel mode** (default): Processes up to 4 files simultaneously
- **Sequential mode** (`-p false`): Processes one file at a time
- **Memory usage**: Loads entire file into memory (suitable for files <100MB)
- **Speed**: Typically processes ~10-50 MB/sec depending on file complexity

## Comparison: CLI vs GUI

| Feature | CLI | GUI (MAUI) |
|---------|-----|------------|
| **Ease of installation** | ✅ Simple | ❌ Requires MAUI workload |
| **Automation** | ✅ Perfect | ❌ Not suitable |
| **Batch processing** | ✅ Native | ✅ Supported |
| **Configuration** | ⚠️ File-based | ✅ Interactive |
| **Progress feedback** | ✅ Console output | ✅ Visual UI |
| **Use case** | Scripts, automation, servers | Interactive, desktop |

**Recommendation:** Use CLI for most scenarios. Use GUI only if you need interactive configuration and visual feedback.

## Advanced: Custom Configuration (Future)

The CLI will support custom configuration files:

```bash
# Use custom config (planned)
LeniTool.Cli -i files/ -o output/ --config myconfig.json
```

For now, you can modify `config.template.json` and rebuild the tool.
