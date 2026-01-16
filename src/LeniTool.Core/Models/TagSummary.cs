namespace LeniTool.Core.Models;

public sealed class TagSummary
{
    public string TagName { get; init; } = string.Empty;
    public int OpenCount { get; init; }
    public int CloseCount { get; init; }

    public int TotalCount => OpenCount + CloseCount;
}
