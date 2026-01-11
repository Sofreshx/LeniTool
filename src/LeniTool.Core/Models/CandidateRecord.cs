namespace LeniTool.Core.Models;

public sealed class CandidateRecord
{
    public string TagName { get; init; } = string.Empty;

    /// <summary>
    /// Byte offset of the first observed opening tag ("&lt;Tag").
    /// </summary>
    public long FirstOpenOffsetBytes { get; init; }

    /// <summary>
    /// Byte offset immediately after the last observed closing tag end ("&gt;").
    /// For self-closing tags, this is the end of the "&gt;".
    /// </summary>
    public long LastCloseEndOffsetBytes { get; init; }

    public int CountEstimate { get; init; }

    /// <summary>
    /// Candidate confidence score [0..1].
    /// </summary>
    public double Confidence { get; init; }
}
