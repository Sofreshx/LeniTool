# LeniTool - HTML File Splitter

A modern .NET MAUI Windows application for splitting large HTML files (like RSP "retour sur prestation" files) into smaller, manageable chunks while preserving HTML structure.

## Features

✨ **Smart Splitting**
- Configurable maximum chunk size (default: 4.5 MB)
- Intelligent segmentation at HTML tags
- Preserves HTML structure and tag balance
- Adds custom opening/closing tags to chunks

🚀 **High Performance**
- Parallel file processing
- Handles multiple files simultaneously
- Progress tracking for each file

⚙️ **Flexible Configuration**
- Customizable segmentation tags
- Configurable opening/closing tags
- Custom file naming patterns
- Persistent configuration (saved locally)

🎨 **Modern UI**
- Clean, intuitive interface
- Drag & drop file support
- Real-time progress tracking
- Detailed logging

## Quick Start

### Prerequisites

1. Install the .NET 8 SDK from https://dotnet.microsoft.com/download
2. Install MAUI workload (one-time):
   ```powershell
   dotnet workload install maui
   ```

### Run the Application

```powershell
# Build and run
dotnet build
dotnet run --project src/LeniTool.UI/LeniTool.UI.csproj
```

### Create Windows Executable

```powershell
dotnet publish src/LeniTool.UI/LeniTool.UI.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The executable will be in: `src/LeniTool.UI/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/LeniTool.UI.exe`

## Download & Run

1. Download `LeniTool.exe` from the latest release
2. Double-click to run - no installation required!
3. The application will create a `config.json` file on first run with default settings

## Building from Source

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 (recommended) or VS Code with C# extension

### Build Instructions

```bash
# Clone the repository
git clone <repository-url>
cd LeniTool

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the application
dotnet run --project src/LeniTool.UI/LeniTool.UI.csproj
```

### Creating a Single-File Executable

For Windows:
```bash
dotnet publish src/LeniTool.UI/LeniTool.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

For macOS (Apple Silicon):
```bash
dotnet publish src/LeniTool.UI/LeniTool.UI.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

For Linux:
```bash
dotnet publish src/LeniTool.UI/LeniTool.UI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

The executable will be in: `src/LeniTool.UI/bin/Release/net8.0-[platform]/[runtime]/publish/`

## Usage

### Basic Workflow

1. **Configure Settings** (optional)
   - Click "Expand" on the Configuration section
   - Adjust max chunk size, segmentation tags, etc.
   - Click "Save Config" to persist settings

2. **Add Files**
   - Click "Add Files" to select HTML files
   - Or drag & drop files into the application

3. **Process Files**
   - Click "Process Files" to start splitting
   - Monitor progress in the Status & Log section
   - Output files will be saved to the configured output directory

### Configuration

#### Segmentation Tags
Tags where the splitter will try to break the file. Listed in priority order:
```
<section
<div class="page"
<div
<article
```

You can add any HTML tag or tag with attributes. The splitter will find the closest match to the target chunk size.

#### Opening Tags
Tags added to the beginning of each chunk (except the first):
```
<html>
<body>
```

#### Closing Tags
Tags added to the end of each chunk (except the last):
```
</body>
</html>
```

#### Naming Pattern
Pattern for output file names. Use placeholders:
- `{filename}` - Original filename without extension
- `{number}` - Part number (001, 002, etc.)

Example: `{filename}_part{number}.html` → `document_part001.html`

#### Output Directory
Where to save the split files. Can be:
- Absolute path: `C:\Users\YourName\Documents\Output`
- Relative path: `output` (relative to executable)
- Empty: Uses default `Documents/LeniTool/Output`

## Configuration File

Settings are saved in `config.json` next to the executable:

```json
{
  "maxChunkSizeMB": 4.5,
  "segmentationTags": ["<section", "<div"],
  "closingTags": ["</body>", "</html>"],
  "openingTags": ["<html>", "<body>"],
  "namingPattern": "{filename}_part{number}.html",
  "outputDirectory": "output",
  "maxParallelFiles": 4
}
```

You can edit this file directly or use the in-app configuration interface.

## Project Structure

```
LeniTool/
├── src/
│   ├── LeniTool.Core/           # Business logic library
│   │   ├── Models/              # Data models
│   │   └── Services/            # Core services
│   └── LeniTool.UI/             # MAUI application
│       ├── ViewModels/          # MVVM ViewModels
│       ├── Converters/          # Value converters
│       └── Resources/           # UI resources
└── tests/
    └── LeniTool.Core.Tests/     # Unit tests
```

## How It Works

1. **File Reading**: The file is read entirely into memory
2. **Size Check**: If under the limit, no splitting occurs
3. **Split Point Detection**: The algorithm searches for configured tags near the target chunk size
4. **Chunk Creation**: Content is split at optimal points
5. **Tag Balancing**: Opening/closing tags are added to maintain valid HTML
6. **File Writing**: Chunks are written with numbered filenames

The splitter uses a smart algorithm that:
- Prioritizes higher-priority segmentation tags
- Searches within a window around the target size
- Falls back to size-based splitting if no tags found
- Maintains HTML structure integrity

## Troubleshooting

### The app won't start
- Ensure you have .NET 8.0 runtime installed (or use self-contained build)
- Check that the executable has proper permissions

### Files aren't being split
- Verify the file is larger than the configured chunk size
- Check that segmentation tags exist in the HTML
- Review the log output for error messages

### Chunks are too large/small
- Adjust the "Max Chunk Size" setting
- Add or modify segmentation tags
- Ensure your HTML has appropriate structure

### Configuration isn't saving
- Check that the application has write permissions in its directory
- Verify the config.json file isn't locked by another process

## Development

### Running Tests

```bash
dotnet test
```

### Adding New Features

The project is structured for easy extension:

- **Core Logic**: Add to `LeniTool.Core/Services/`
- **Models**: Add to `LeniTool.Core/Models/`
- **UI**: Add to `LeniTool.UI/ViewModels/` or `LeniTool.UI/`

### Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

[Your License Here]

## Support

For issues, questions, or feature requests, please open an issue on GitHub.

## Credits

Built with:
- .NET 8.0
- .NET MAUI
- CommunityToolkit.Mvvm
