namespace LeniTool.Core.Models;

/// <summary>
/// Configuration for HTML file splitting
/// </summary>
public class SplitConfiguration
{
    /// <summary>
    /// Maximum chunk size in megabytes
    /// </summary>
    public double MaxChunkSizeMB { get; set; } = 4.5;

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
    /// Gets the maximum chunk size in bytes
    /// </summary>
    public long MaxChunkSizeBytes => (long)(MaxChunkSizeMB * 1024 * 1024);

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
}
