namespace LeniTool.Core.Models;

/// <summary>
/// Result of a file splitting operation
/// </summary>
public class SplitResult
{
    public string OriginalFilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> OutputFiles { get; set; } = new();
    public long OriginalSizeBytes { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public int ChunkCount => OutputFiles.Count;
}

/// <summary>
/// Progress information for file processing
/// </summary>
public class ProcessingProgress
{
    public string FileName { get; set; } = string.Empty;
    public int CurrentChunk { get; set; }
    public int TotalChunks { get; set; }
    public double PercentComplete { get; set; }
    public string Status { get; set; } = string.Empty;
}
