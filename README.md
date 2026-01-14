# LeniTool - Large File Splitter (HTML + Markup TXT)

LeniTool is a Windows desktop application for splitting large files into smaller parts while keeping important structure intact.

The primary app is the **Avalonia Desktop** UI in `src/LeniTool.Desktop/`. A legacy/experimental MAUI UI project still exists in `src/LeniTool.UI/`, but it is not the main documented workflow.

## Features

✨ **Smart Splitting**
- Configurable maximum chunk size (default: 4.5 MB)
- **HTML**: splits near configured segmentation tags while preserving structure (opening/closing tags are added as needed)
- **TXT (markup / pseudo-XML)**: splits on whole “record” elements (auto-detected or manually configured)
- Preserves wrapper/prefix/suffix bytes around records when possible (for TXT markup)

🚀 **High Performance**
- Parallel file processing
- Handles multiple files simultaneously
- Progress tracking for each file

⚙️ **Flexible Configuration**
- Persistent `config.json` (auto-created next to the executable on first run)
- Global defaults + per-extension profiles + per-file overrides
- Manual override of record-tag detection from the Desktop UI

🎨 **Modern UI**
- Clean, intuitive interface
- Drag & drop file support
- Real-time progress tracking
- Detailed logging

## Supported File Types

| Extension | Support | Notes |
|---|---|---|
| `.html`, `.htm` | ✅ | Split near configured HTML segmentation tags |
| `.txt` | ✅ | For text files containing XML/HTML-like markup; splits on a repeating record tag |
| `.pdf` | ❌ (planned) | A placeholder strategy exists, but PDF splitting is not implemented |

## Quick Start

### Prerequisites

1. Install the .NET 8 SDK from https://dotnet.microsoft.com/download

### Run the Application

```powershell
dotnet run --project src/LeniTool.Desktop/LeniTool.Desktop.csproj
```

### Create Windows Executable

```powershell
dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The executable will be in: `src/LeniTool.Desktop/bin/Release/net8.0-windows/win-x64/publish/`

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

# Run the application (Avalonia Desktop)
dotnet run --project src/LeniTool.Desktop/LeniTool.Desktop.csproj
```

### Creating a Single-File Executable

For Windows:
```bash
dotnet publish src/LeniTool.Desktop/LeniTool.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable will be in: `src/LeniTool.Desktop/bin/Release/net8.0-windows/win-x64/publish/`

### Legacy / Experimental MAUI UI

There is also a MAUI UI project in `src/LeniTool.UI/`. It is not the main documented workflow and may require installing MAUI workloads.

## Usage

### Basic Workflow (Desktop)

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

LeniTool reads/writes `config.json` using **camelCase** JSON properties.

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

## TXT Markup Splitting (auto-detect + overrides)

For `.txt` files that contain tag-like markup (pseudo-XML / pseudo-HTML), LeniTool can split on a repeating **record tag**.

**How auto-detection works (high level):** the analyzer scans for tag-like tokens (e.g. `<Record ...>` / `</Record>`), counts repetitions, checks open/close balance, and ranks candidates by a confidence score. The top candidate is used when `autoDetectRecordTag` is enabled and `recordTagName` is not set.

**Override options:**
- Desktop UI: use the Analysis / Overrides panel to turn off auto-detect and enter a `recordTagName`.
- Config: set global defaults, per-extension defaults, and/or per-file overrides.

### Example `.txt` input

```text
<Envelope>
   <Header>...</Header>

   <Ficher>
      <Id>1</Id>
      <Name>Alice</Name>
   </Ficher>

   <Ficher>
      <Id>2</Id>
      <Name>Bob</Name>
   </Ficher>

   <Footer>...</Footer>
</Envelope>
```

### Config snippet: per-extension + per-file overrides

```json
{
   "maxChunkSizeMB": 4.5,
   "autoDetectRecordTag": true,
   "recordTagName": null,

   "extensionProfiles": {
      ".txt": {
         "autoDetectRecordTag": false,
         "recordTagName": "Ficher"
      }
   },

   "fileOverrides": {
      "C:\\data\\special-case.txt": {
         "autoDetectRecordTag": false,
         "recordTagName": "Record"
      }
   }
}
```

Notes:
- Extension keys may be stored as `.txt` or `txt` (both are accepted).
- File override keys should be full paths; LeniTool normalizes paths when resolving overrides.

## Project Structure

```
LeniTool/
├── src/
│   ├── LeniTool.Core/           # Business logic library
│   │   ├── Models/              # Data models
│   │   └── Services/            # Core services
│   ├── LeniTool.Desktop/        # Avalonia Desktop application (primary)
│   └── LeniTool.UI/             # MAUI UI (legacy/experimental)
│       ├── ViewModels/          # MVVM ViewModels
│       ├── Converters/          # Value converters
│       └── Resources/           # UI resources
└── tests/
    └── LeniTool.Core.Tests/     # Unit tests
```

## How It Works

1. **Strategy selection**: based on file extension (`.html/.htm`, `.txt`, `.pdf` placeholder)
2. **Size check**: if under the max chunk size, no split is performed
3. **Analysis (when applicable)**: detects candidates / wrapper boundaries (TXT markup) and estimates parts
4. **Split**: produces output parts based on the strategy’s boundaries (HTML tags or record spans)

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
- For HTML: check that segmentation tags exist in the input
- For TXT markup: ensure there is a repeating record tag, or configure `recordTagName`
- Review the log output for error messages

### PDF files
PDF support is currently **not implemented**. Attempting to split a `.pdf` will throw an exception.

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
