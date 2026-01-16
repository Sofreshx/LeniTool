# Quick Start Guide

## Getting Started in 5 Minutes

### 1. Prerequisites

Install the .NET 8 SDK:

- https://dotnet.microsoft.com/download

Notes:
- The primary app is the Avalonia Desktop UI (`src/LeniTool.Desktop/`).

### 2. Build the Application

Open a terminal in the LeniTool directory and run:

```powershell
dotnet build
```

### 3. Run the Application

```powershell
dotnet run --project src/LeniTool.Desktop/LeniTool.Desktop.csproj
```

### 4. Create Standalone Executable

```powershell
dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The executable will be in: `src/LeniTool.Desktop/bin/Release/net8.0-windows/win-x64/publish/`

Copy this file anywhere and run it - no installation needed!

### 5. Use the Application

1. **Expand Configuration** (optional)
   - Click "Expand ▼" to see configuration options
   - Adjust settings as needed
   - Default: 4.5 MB chunks, splits at `<section>`, `<div>` tags

2. **Add Files**
   - Click "Add Files" button
   - Select one or more `.html/.htm` or `.txt` files
   - Files appear in the file list

3. **Process**
   - Click "🚀 Process Files"
   - Watch progress in real-time
   - Check the log for results

4. **Find Output**
   - By default: `Documents/LeniTool/Output`
   - Or check your configured output directory
   - Files named like: `yourfile_part001.html`, `yourfile_part002.html`, etc.

## Configuration Tips

### For RSP Files

If your RSP files have specific structure, update the segmentation tags:

```
<div class="prestation"
<section class="detail"
<table class="rapport"
```

### Adjust Chunk Size

- Smaller files: Set to 1-2 MB
- Larger files: Set to 5-10 MB
- Default 4.5 MB works for most cases

### Custom Output Location

Click "Browse" next to Output Directory to choose where files are saved.

## Troubleshooting

### Can't build?
Install the .NET 8 SDK from https://dotnet.microsoft.com/download

### Need help?
Check the full [README.md](README.md) or [BUILD.md](BUILD.md) for detailed instructions.

## Next Steps

- Read [README.md](README.md) for full documentation
- Check [BUILD.md](BUILD.md) for deployment options
- Run tests: `dotnet test`
- Customize configuration in `config.json`

## Minimal TXT Markup Workflow

Use this when you have a large `.txt` file with tag-like markup (pseudo-XML / pseudo-HTML) and you want to split by a repeating record element.

### Example input

```text
<Envelope>
   <Ficher><Id>1</Id></Ficher>
   <Ficher><Id>2</Id></Ficher>
   <Ficher><Id>3</Id></Ficher>
</Envelope>
```

### Recommended flow

1. Add the `.txt` file to the Desktop app.
2. Use Analyze (if available) to see detected candidate record tags.
3. If the detected tag is wrong, override it in the Analysis/Overrides panel by turning off auto-detect and setting `recordTagName`.
4. Process the file.

### Config override example (optional)

LeniTool writes `config.json` next to the executable on first run. You can add per-extension or per-file overrides:

```json
{
   "extensionProfiles": {
      ".txt": {
         "autoDetectRecordTag": false,
         "recordTagName": "Ficher"
      }
   },
   "fileOverrides": {
      "C:\\data\\one-off.txt": {
         "autoDetectRecordTag": false,
         "recordTagName": "Record"
      }
   }
}
```

## Example Workflow

```powershell
# 1. Build release version
dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# 2. Navigate to output
cd src/LeniTool.Desktop/bin/Release/net8.0-windows/win-x64/publish/

# 3. Run it
./LeniTool.Desktop.exe

# 4. Copy executable anywhere you want - it's portable!
```

That's it! You now have a working HTML file splitter. 🎉
