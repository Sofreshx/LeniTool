using LeniTool.Core.Models;

namespace LeniTool.Core.Services;

public interface ISplitterStrategy
{
    IReadOnlyCollection<string> SupportedExtensions { get; }

    Task<AnalysisResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);

    Task<List<string>> SplitFileAsync(
        string filePath,
        string outputDirectory,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
