using System.Text;
using LeniTool.Core.Models;

namespace LeniTool.Core.Services;

/// <summary>
/// Split strategy for .txt files that contain XML/HTML-like markup.
/// Uses <see cref="MarkupAnalyzer"/> to discover a repeating record tag,
/// then splits on record boundaries using byte offsets (streaming copy).
/// </summary>
public sealed class TxtMarkupSplitterService : ISplitterStrategy
{
    private static readonly IReadOnlyCollection<string> Extensions = new[] { ".txt" };

    private readonly SplitConfiguration _config;
    private readonly MarkupAnalyzer _analyzer;
    private readonly RecordSpanScanner _scanner;
    private readonly RecordChunker _chunker;

    public TxtMarkupSplitterService(SplitConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _analyzer = new MarkupAnalyzer();
        _scanner = new RecordSpanScanner();
        _chunker = new RecordChunker();
    }

    public IReadOnlyCollection<string> SupportedExtensions => Extensions;

    public Task<AnalysisResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var resolved = _config.ResolveForFile(filePath);
        return _analyzer.AnalyzeAsync(filePath, targetMaxChunkBytes: resolved.MaxChunkSizeBytes, cancellationToken: cancellationToken);
    }

    public async Task<List<string>> SplitFileAsync(
        string filePath,
        string outputDirectory,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        var resolved = _config.ResolveForFile(filePath);

        var fileInfo = new FileInfo(filePath);

        // If file is already under size limit, no split needed.
        if (fileInfo.Length <= resolved.MaxChunkSizeBytes)
        {
            progress?.Report(new ProcessingProgress
            {
                FileName = fileInfo.Name,
                Status = "File is already under size limit - no split needed"
            });
            return new List<string> { filePath };
        }

        var analysis = await _analyzer
            .AnalyzeAsync(filePath, targetMaxChunkBytes: resolved.MaxChunkSizeBytes, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var boundaries = TxtMarkupSplitBoundaryResolver.TryResolve(analysis, resolved, fileInfo.Length, out var failureReason);
        if (boundaries is null)
        {
            progress?.Report(new ProcessingProgress
            {
                FileName = fileInfo.Name,
                Status = $"{failureReason} - no split performed"
            });
            return new List<string> { filePath };
        }

        var prefixEnd = boundaries.Value.PrefixEndOffsetBytes;
        var suffixStart = boundaries.Value.SuffixStartOffsetBytes;

        var encoding = ResolveEncoding(analysis.EncodingName);

        var spans = _scanner.ScanAsync(
            filePath,
            encoding,
            scanStartOffsetBytes: prefixEnd,
            scanEndOffsetBytesExclusive: suffixStart,
            tagName: boundaries.Value.TagName,
            cancellationToken: cancellationToken);

        var outputs = await _chunker
            .WriteChunksAsync(
                filePath,
                outputDirectory,
                resolved,
                targetMaxChunkBytes: resolved.MaxChunkSizeBytes,
                prefixEndOffsetBytes: prefixEnd,
                suffixStartOffsetBytes: suffixStart,
                recordSpans: spans,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        if (outputs.Count == 0)
        {
            progress?.Report(new ProcessingProgress
            {
                FileName = fileInfo.Name,
                Status = "No records detected during scan - no split performed"
            });
            return new List<string> { filePath };
        }

        return outputs;
    }

    private static Encoding ResolveEncoding(string encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName))
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var name = encodingName.Trim().ToLowerInvariant();

        return name switch
        {
            "utf-8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            "utf-16" => new UnicodeEncoding(bigEndian: false, byteOrderMark: false),
            "utf-16be" => new UnicodeEncoding(bigEndian: true, byteOrderMark: false),
            _ => Encoding.GetEncoding(name)
        };
    }
}
