namespace LeniTool.Core.Models;

public sealed class WrapperRange
{
    /// <summary>
    /// Byte offset where the wrapper prefix ends (first record start).
    /// </summary>
    public long PrefixEndOffsetBytes { get; init; }

    /// <summary>
    /// Byte offset where the wrapper suffix begins (end of last record).
    /// </summary>
    public long SuffixStartOffsetBytes { get; init; }
}
