namespace LeniTool.Core.Models;

/// <summary>
/// Configuration for file splitting.
/// Historically this was HTML-only; it now also supports per-extension and per-file overrides
/// (used by the .txt markup splitter) while keeping backwards-compatible JSON.
/// </summary>
public class SplitConfiguration
{
    /// <summary>
    /// Maximum chunk size in megabytes
    /// </summary>
    public double MaxChunkSizeMB { get; set; } = 4.5;

    /// <summary>
    /// Hard maximum allowed input file size in megabytes.
    /// When set to 0, the limit is treated as disabled.
    /// </summary>
    public double MaxInputFileSize { get; set; } = 5 * 1024;

    /// <summary>
    /// Auto-analyze threshold in megabytes.
    /// Files larger than this value will be added but not automatically analyzed.
    /// When set to 0, auto-analysis is disabled.
    /// </summary>
    public double AutoAnalyzeThreshold { get; set; } = 100;

    /// <summary>
    /// HTML tags to use as split points (in order of priority)
    /// </summary>
    public List<string> SegmentationTags { get; set; } = new()
    {
        "<section",
        "<div class=\"page\"",
        "<div",
        "<article"
    };

    /// <summary>
    /// Tags to append at the end of each chunk
    /// </summary>
    public List<string> ClosingTags { get; set; } = new()
    {
        "</body>",
        "</html>"
    };

    /// <summary>
    /// Tags to prepend at the beginning of each chunk (except the first)
    /// </summary>
    public List<string> OpeningTags { get; set; } = new()
    {
        "<html>",
        "<body>"
    };

    /// <summary>
    /// Pattern for naming output files. Use {filename} for original name, {number} for part number
    /// </summary>
    public string NamingPattern { get; set; } = "{filename}_part{number}.html";

    /// <summary>
    /// Default output directory (relative or absolute)
    /// </summary>
    public string OutputDirectory { get; set; } = "output";

    /// <summary>
    /// Maximum number of files to process in parallel
    /// </summary>
    public int MaxParallelFiles { get; set; } = 4;

    /// <summary>
    /// Optional preferred record tag for markup-based splitters (e.g. .txt pseudo-XML).
    /// When null/empty and <see cref="AutoDetectRecordTag"/> is true, the splitter will auto-detect.
    /// </summary>
    public string? RecordTagName { get; set; }

    /// <summary>
    /// When true, splitters may auto-detect record tags if <see cref="RecordTagName"/> is not set.
    /// This provides an explicit "auto-detect" vs "manual" flag for the UI.
    /// </summary>
    public bool AutoDetectRecordTag { get; set; } = true;

    /// <summary>
    /// Per-extension profiles (e.g. ".txt" defaults). Values here override global defaults.
    /// </summary>
    public Dictionary<string, SplitConfigurationOverrides>? ExtensionProfiles { get; set; }

    /// <summary>
    /// Persisted per-file overrides. Values here override extension profiles and global defaults.
    /// Keys should be full file paths.
    /// </summary>
    public Dictionary<string, SplitConfigurationOverrides>? FileOverrides { get; set; }

    /// <summary>
    /// Transient per-run overrides (not persisted). Highest precedence.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SplitRunOverrides RunOverrides { get; set; } = new();

    /// <summary>
    /// Gets the maximum chunk size in bytes
    /// </summary>
    public long MaxChunkSizeBytes => (long)(MaxChunkSizeMB * 1024 * 1024);

    /// <summary>
    /// Gets the maximum allowed input file size in bytes.
    /// </summary>
    public long MaxInputFileSizeBytes => (long)(MaxInputFileSize * 1024 * 1024);

    /// <summary>
    /// Gets the auto-analyze threshold in bytes.
    /// </summary>
    public long AutoAnalyzeThresholdBytes => (long)(AutoAnalyzeThreshold * 1024 * 1024);

    /// <summary>
    /// Resolves an effective configuration for a specific file.
    /// Precedence: transient override &gt; persisted file override &gt; extension profile &gt; global defaults.
    /// </summary>
    public SplitConfiguration ResolveForFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        var resolved = new SplitConfiguration
        {
            MaxChunkSizeMB = MaxChunkSizeMB,
            MaxInputFileSize = MaxInputFileSize,
            AutoAnalyzeThreshold = AutoAnalyzeThreshold,
            SegmentationTags = new List<string>(SegmentationTags ?? new List<string>()),
            ClosingTags = new List<string>(ClosingTags ?? new List<string>()),
            OpeningTags = new List<string>(OpeningTags ?? new List<string>()),
            NamingPattern = NamingPattern,
            OutputDirectory = OutputDirectory,
            MaxParallelFiles = MaxParallelFiles,
            RecordTagName = RecordTagName,
            AutoDetectRecordTag = AutoDetectRecordTag
        };

        var extensionKey = NormalizeExtension(Path.GetExtension(filePath));
        if (!string.IsNullOrEmpty(extensionKey) && ExtensionProfiles is not null)
        {
            if (TryGetByExtensionKey(ExtensionProfiles, extensionKey, out var profileOverrides))
                ApplyOverrides(resolved, profileOverrides);
        }

        var fullPathKey = NormalizeFileKey(filePath);
        if (FileOverrides is not null)
        {
            if (FileOverrides.TryGetValue(fullPathKey, out var fileOverrides))
                ApplyOverrides(resolved, fileOverrides);
            else if (FileOverrides.TryGetValue(filePath, out var fileOverrides2))
                ApplyOverrides(resolved, fileOverrides2);
        }

        // Transient overrides (run-level), highest precedence.
        ApplyOverrides(resolved, RunOverrides.GlobalOverride);

        if (RunOverrides.FileOverrides is not null)
        {
            if (RunOverrides.FileOverrides.TryGetValue(fullPathKey, out var runFileOverrides))
                ApplyOverrides(resolved, runFileOverrides);
            else if (RunOverrides.FileOverrides.TryGetValue(filePath, out var runFileOverrides2))
                ApplyOverrides(resolved, runFileOverrides2);
        }

        return resolved;
    }

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public bool IsValid(out string errorMessage)
    {
        if (MaxChunkSizeMB <= 0)
        {
            errorMessage = "Max chunk size must be greater than 0";
            return false;
        }

        if (MaxChunkSizeMB > 100)
        {
            errorMessage = "Max chunk size must be 100 MB or less";
            return false;
        }

        if (MaxInputFileSize < 0)
        {
            errorMessage = "Max input file size must be 0 or greater";
            return false;
        }

        if (AutoAnalyzeThreshold < 0)
        {
            errorMessage = "Auto-analyze threshold must be 0 or greater";
            return false;
        }

        // Optional sanity bounds to avoid accidental huge values.
        // 1,048,576 MB = 1 TB.
        if (MaxInputFileSize > 1_048_576)
        {
            errorMessage = "Max input file size must be 1 TB (1,048,576 MB) or less";
            return false;
        }

        if (AutoAnalyzeThreshold > 1_048_576)
        {
            errorMessage = "Auto-analyze threshold must be 1 TB (1,048,576 MB) or less";
            return false;
        }

        if (MaxInputFileSize > 0 && AutoAnalyzeThreshold > MaxInputFileSize)
        {
            errorMessage = "Auto-analyze threshold must be less than or equal to max input file size";
            return false;
        }

        if (SegmentationTags == null || SegmentationTags.Count == 0)
        {
            errorMessage = "At least one segmentation tag must be specified";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NamingPattern))
        {
            errorMessage = "Naming pattern cannot be empty";
            return false;
        }

        if (!NamingPattern.Contains("{filename}") || !NamingPattern.Contains("{number}"))
        {
            errorMessage = "Naming pattern must contain {filename} and {number}";
            return false;
        }

        if (MaxParallelFiles < 1 || MaxParallelFiles > 32)
        {
            errorMessage = "Max parallel files must be between 1 and 32";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            errorMessage = "Output directory cannot be empty";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static void ApplyOverrides(SplitConfiguration target, SplitConfigurationOverrides? overrides)
    {
        if (target is null)
            throw new ArgumentNullException(nameof(target));

        if (overrides is null)
            return;

        if (overrides.MaxChunkSizeMB.HasValue)
            target.MaxChunkSizeMB = overrides.MaxChunkSizeMB.Value;

        if (overrides.SegmentationTags is not null)
            target.SegmentationTags = new List<string>(overrides.SegmentationTags);

        if (overrides.ClosingTags is not null)
            target.ClosingTags = new List<string>(overrides.ClosingTags);

        if (overrides.OpeningTags is not null)
            target.OpeningTags = new List<string>(overrides.OpeningTags);

        if (overrides.NamingPattern is not null)
            target.NamingPattern = overrides.NamingPattern;

        if (overrides.OutputDirectory is not null)
            target.OutputDirectory = overrides.OutputDirectory;

        if (overrides.MaxParallelFiles.HasValue)
            target.MaxParallelFiles = overrides.MaxParallelFiles.Value;

        if (overrides.RecordTagName is not null)
            target.RecordTagName = overrides.RecordTagName;

        if (overrides.AutoDetectRecordTag.HasValue)
            target.AutoDetectRecordTag = overrides.AutoDetectRecordTag.Value;
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        var ext = extension.Trim().ToLowerInvariant();
        if (!ext.StartsWith('.'))
            ext = "." + ext;

        return ext;
    }

    private static string NormalizeFileKey(string filePath)
    {
        try
        {
            return Path.GetFullPath(filePath);
        }
        catch
        {
            return filePath;
        }
    }

    private static bool TryGetByExtensionKey(
        Dictionary<string, SplitConfigurationOverrides> dict,
        string extensionKey,
        out SplitConfigurationOverrides? overrides)
    {
        if (dict.TryGetValue(extensionKey, out var exact))
        {
            overrides = exact;
            return true;
        }

        // Allow users to store keys without a leading dot.
        var noDot = extensionKey.StartsWith('.') ? extensionKey[1..] : extensionKey;
        if (!string.IsNullOrEmpty(noDot) && dict.TryGetValue(noDot, out var alt))
        {
            overrides = alt;
            return true;
        }

        overrides = null;
        return false;
    }
}
