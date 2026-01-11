namespace LeniTool.Core.Models;

/// <summary>
/// Nullable overrides applied on top of a base <see cref="SplitConfiguration"/>.
/// Any non-null value replaces the base.
/// </summary>
public sealed class SplitConfigurationOverrides
{
    public double? MaxChunkSizeMB { get; set; }

    public List<string>? SegmentationTags { get; set; }
    public List<string>? ClosingTags { get; set; }
    public List<string>? OpeningTags { get; set; }

    public string? NamingPattern { get; set; }
    public string? OutputDirectory { get; set; }

    public int? MaxParallelFiles { get; set; }

    /// <summary>
    /// Optional preferred record tag for markup-based splitters.
    /// Set to empty string to explicitly clear.
    /// </summary>
    public string? RecordTagName { get; set; }

    /// <summary>
    /// Explicitly toggles auto-detection of record tags.
    /// </summary>
    public bool? AutoDetectRecordTag { get; set; }
}

/// <summary>
/// Transient (non-persisted) overrides to apply for a single run.
/// </summary>
public sealed class SplitRunOverrides
{
    public SplitConfigurationOverrides? GlobalOverride { get; set; }

    /// <summary>
    /// Per-file transient overrides. Keys should be full file paths.
    /// </summary>
    public Dictionary<string, SplitConfigurationOverrides>? FileOverrides { get; set; }
}
