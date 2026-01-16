namespace LeniTool.Core.Models;

/// <summary>
/// Lightweight analysis metadata for an input file.
/// This is intentionally tolerant of imperfect markup.
/// </summary>
public sealed class AnalysisResult
{
    public string FilePath { get; init; } = string.Empty;
    public string Extension { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string StrategyName { get; init; } = string.Empty;

    /// <summary>
    /// Detected text encoding name (e.g. utf-8, utf-16).
    /// </summary>
    public string EncodingName { get; init; } = string.Empty;

    /// <summary>
    /// True when a BOM/preamble was detected at the start of the file.
    /// </summary>
    public bool HasBom { get; init; }

    /// <summary>
    /// Length of BOM/preamble in bytes.
    /// </summary>
    public int BomLengthBytes { get; init; }

    /// <summary>
    /// Candidate record tags (most likely repeating element names).
    /// Sorted by confidence descending.
    /// </summary>
    public List<CandidateRecord> CandidateRecords { get; init; } = new();

    /// <summary>
    /// Wrapper prefix/suffix boundaries (byte offsets) when a repeating record can be identified.
    /// Prefix ends at the first record start; suffix starts at the end of the last record.
    /// </summary>
    public WrapperRange? WrapperRange { get; init; }

    /// <summary>
    /// Overall confidence score [0..1] for the top candidate.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Estimated number of parts for a target max chunk size.
    /// </summary>
    public int EstimatedPartCount { get; init; }

    /// <summary>
    /// Summary of all tags detected during analysis.
    /// </summary>
    public List<TagSummary> TagSummaries { get; init; } = new();
}
