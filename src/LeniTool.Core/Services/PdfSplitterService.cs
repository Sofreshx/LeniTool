using LeniTool.Core.Models;

namespace LeniTool.Core.Services;

/// <summary>
/// Placeholder splitter strategy for PDF files.
/// This only wires the extension point; full PDF support is intentionally not implemented yet.
/// </summary>
public sealed class PdfSplitterService : ISplitterStrategy
{
    private static readonly IReadOnlyCollection<string> Extensions = new[] { ".pdf" };

    private readonly SplitConfiguration _config;

    public PdfSplitterService(SplitConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public IReadOnlyCollection<string> SupportedExtensions => Extensions;

    public Task<AnalysisResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileInfo = new FileInfo(filePath);
        return Task.FromResult(new AnalysisResult
        {
            FilePath = filePath,
            Extension = fileInfo.Extension,
            FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            StrategyName = $"{nameof(PdfSplitterService)} (placeholder - not implemented)",
            Confidence = 0,
            EstimatedPartCount = 0
        });
    }

    public Task<List<string>> SplitFileAsync(
        string filePath,
        string outputDirectory,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        throw new NotImplementedException(
            "PDF splitting is not implemented yet. This is a placeholder strategy to reserve the .pdf extension point.");
    }
}
