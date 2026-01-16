# Build and Deployment Guide

## Quick Build Commands

### Windows (Only Platform Supported)
```powershell
# Debug build
dotnet build

# Release build with single-file executable (Windows)
dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true

# Output: src/LeniTool.Desktop/bin/Release/net8.0-windows/win-x64/publish/LeniTool.Desktop.exe
```

**Note:** This is a Windows-only application. macOS and Linux are not supported.

### macOS (Apple Silicon)
```bash
# Release build with single-file executable
dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false

# Output: src/LeniTool.Desktop/bin/Release/net8.0-maccatalyst/osx-arm64/publish/LeniTool.Desktop.app
```

### macOS (Intel)
```bash
dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false
```

### Linux
```bash
# Release build with single-file executable
dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false

# Output: src/LeniTool.Desktop/bin/Release/net8.0-android/linux-x64/publish/LeniTool.Desktop
```

## Build Options Explained

- `-c Release`: Release configuration (optimized)
- `-r [runtime]`: Target runtime identifier
- `--self-contained true`: Includes .NET runtime (no installation needed)
- `-p:PublishSingleFile=true`: Creates a single executable
- `-p:IncludeNativeLibrariesForSelfExtract=true`: Embeds native libs (Windows)
- `-p:PublishTrimmed=false`: Don't trim assemblies (prevents issues with reflection)

## Runtime Identifiers (RID)

Common RIDs:
- `win-x64` - Windows 64-bit
- `win-x86` - Windows 32-bit
- `win-arm64` - Windows ARM64
- `osx-x64` - macOS Intel
- `osx-arm64` - macOS Apple Silicon
- `linux-x64` - Linux 64-bit
- `linux-arm64` - Linux ARM64

## Distribution Package

After building, create a distribution package:

### Windows
```powershell
# Create distribution folder
New-Item -ItemType Directory -Force -Path "dist\windows"

# Copy executable
Copy-Item "src\LeniTool.Desktop\bin\Release\net8.0-windows\win-x64\publish\LeniTool.Desktop.exe" "dist\windows\"

# Copy config template
Copy-Item "config.template.json" "dist\windows\config.json"

# Copy README
Copy-Item "README.md" "dist\windows\"

# Create ZIP
Compress-Archive -Path "dist\windows\*" -DestinationPath "dist\LeniTool-win-x64.zip"
```

### macOS/Linux
```bash
# Create distribution folder
mkdir -p dist/macos

# Copy executable
cp -r src/LeniTool.Desktop/bin/Release/net8.0-maccatalyst/osx-arm64/publish/LeniTool.Desktop.app dist/macos/

# Copy config template
cp config.template.json dist/macos/config.json

# Copy README
cp README.md dist/macos/

# Create archive
cd dist && tar -czf LeniTool-osx-arm64.tar.gz macos/
```

## Testing the Build

```bash
# Run the published executable
./src/LeniTool.Desktop/bin/Release/[target-framework]/[runtime]/publish/LeniTool.Desktop

# Or on Windows
.\src\LeniTool.Desktop\bin\Release\net8.0-windows\win-x64\publish\LeniTool.Desktop.exe
```

## Troubleshooting

### "The application to execute does not exist"
- Ensure the build completed successfully
- Check the output path matches your target framework

### "This app can't run on your PC" (Windows)
- Build with the correct RID for your architecture (x64 vs x86 vs ARM64)

### Permission denied (macOS/Linux)
```bash
chmod +x ./path/to/LeniTool.Desktop
```



## Size Optimization

For smaller executables (advanced):

```bash
dotnet publish \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:TrimMode=link
```

⚠️ **Warning**: Trimming can cause runtime issues with reflection. Test thoroughly!

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    strategy:
      matrix:
        os: [windows-latest, macos-latest, ubuntu-latest]
    runs-on: ${{ matrix.os }}
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
          
      - name: Publish (Windows)
        if: matrix.os == 'windows-latest'
        run: dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
        
      - name: Publish (macOS)
        if: matrix.os == 'macos-latest'
        run: dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
        
      - name: Publish (Linux)
        if: matrix.os == 'ubuntu-latest'
        run: dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
        
      - name: Upload artifacts
        uses: actions/upload-artifact@v3
        with:
          name: release-${{ matrix.os }}
          path: src/LeniTool.Desktop/bin/Release/**/publish/
```
