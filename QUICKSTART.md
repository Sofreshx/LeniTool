# Quick Start Guide

## Getting Started in 5 Minutes

### 1. Prerequisites

Install .NET 8 SDK and MAUI workload (one-time setup):

```powershell
# Install MAUI workload
dotnet workload install maui
```

### 2. Build the Application

Open a terminal in the LeniTool directory and run:

```powershell
dotnet build
```

### 3. Run the Application

```powershell
dotnet run --project src/LeniTool.UI/LeniTool.UI.csproj
```

### 4. Create Standalone Executable

```powershell
dotnet publish src/LeniTool.UI/LeniTool.UI.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The executable will be in: `src/LeniTool.UI/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/LeniTool.UI.exe`

Copy this file anywhere and run it - no installation needed!

### 5. Use the Application

1. **Expand Configuration** (optional)
   - Click "Expand ▼" to see configuration options
   - Adjust settings as needed
   - Default: 4.5 MB chunks, splits at `<section>`, `<div>` tags

2. **Add Files**
   - Click "Add Files" button
   - Select one or more HTML files
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
```powershell
# Install .NET 8 SDK from: https://dotnet.microsoft.com/download
# Install MAUI workload
dotnet workload install maui
```

### MAUI errors?
```powershell
# Update workloads
dotnet workload update
```

### Need help?
Check the full [README.md](README.md) or [BUILD.md](BUILD.md) for detailed instructions.

## Next Steps

- Read [README.md](README.md) for full documentation
- Check [BUILD.md](BUILD.md) for deployment options
- Run tests: `dotnet test`
- Customize configuration in `config.json`

## Example Workflow

```powershell
# 1. Build release version
dotnet publish src/LeniTool.UI/LeniTool.UI.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# 2. Navigate to output
cd src/LeniTool.UI/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/

# 3. Run it
./LeniTool.UI.exe

# 4. Copy executable anywhere you want - it's portable!
```

That's it! You now have a working HTML file splitter. 🎉
