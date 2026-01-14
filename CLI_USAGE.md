# LeniTool CLI - Status

There is currently **no supported CLI project** in this repository.

Older versions of this documentation referenced `src/LeniTool.Cli/LeniTool.Cli.csproj`; that project does not exist, and these instructions were outdated.

## Recommended Alternatives

### 1) Use the Desktop app (recommended)

The primary UI is the Avalonia Desktop app:

```powershell
dotnet run --project src/LeniTool.Desktop/LeniTool.Desktop.csproj
```

It supports:
- `.html/.htm` splitting using segmentation tags
- `.txt` markup splitting using record-tag auto-detection + manual override

### 2) Automate via config.json

LeniTool reads/writes `config.json` next to the executable. You can preconfigure `.txt` defaults using `extensionProfiles` and special cases using `fileOverrides`.

Example:

```json
{
  "extensionProfiles": {
    ".txt": {
      "autoDetectRecordTag": false,
      "recordTagName": "Ficher"
    }
  }
}
```

### 3) Programmatic use

If you need a true CLI/batch pipeline today, integrate `LeniTool.Core` into your own console host.

## PDF Note

`.pdf` is not supported yet. A placeholder strategy exists in Core to reserve the extension point, but PDF splitting is not implemented.
