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

        var configuredTag = resolved.RecordTagName?.Trim();
        var allowAutoDetect = resolved.AutoDetectRecordTag;
        var hasConfiguredTag = !string.IsNullOrWhiteSpace(configuredTag);

        if (!allowAutoDetect && !hasConfiguredTag)
        {
            progress?.Report(new ProcessingProgress
            {
                FileName = fileInfo.Name,
                Status = "Record tag auto-detection disabled and no record tag configured - no split performed"
            });
            return new List<string> { filePath };
        }

        CandidateRecord? selected = null;
        if (hasConfiguredTag)
        {
            selected = analysis.CandidateRecords
                .FirstOrDefault(r => string.Equals(r.TagName, configuredTag, StringComparison.OrdinalIgnoreCase));

            // If the tag was configured but not detected during analysis, we can still attempt a scan.
            // Wrapper boundaries will fall back to the full file (0..Length).
            if (selected is null)
                selected = new CandidateRecord { TagName = configuredTag! };
        }
        else
        {
            selected = analysis.CandidateRecords.FirstOrDefault();
        }

        if (selected is null || string.IsNullOrWhiteSpace(selected.TagName))
        {
            progress?.Report(new ProcessingProgress
            {
                FileName = fileInfo.Name,
                Status = "No repeating record tag detected - no split performed"
            });
            return new List<string> { filePath };
        }

        var prefixEnd = selected.FirstOpenOffsetBytes;
        var suffixStart = selected.LastCloseEndOffsetBytes;

        if (prefixEnd <= 0 && suffixStart <= 0)
        {
            // If we are using a user-configured tag that analysis didn't detect, we can't rely on
            // wrapper boundaries computed for a different candidate. Scan the full file.
            if (hasConfiguredTag && analysis.CandidateRecords.All(r =>
                    !string.Equals(r.TagName, configuredTag, StringComparison.OrdinalIgnoreCase)))
            {
                prefixEnd = 0;
                suffixStart = fileInfo.Length;
            }
            else
            {
                var wrapper = analysis.WrapperRange;
                prefixEnd = wrapper?.PrefixEndOffsetBytes ?? 0;
                suffixStart = wrapper?.SuffixStartOffsetBytes ?? fileInfo.Length;
            }
        }

        if (prefixEnd < 0)
            prefixEnd = 0;
        if (suffixStart <= 0 || suffixStart > fileInfo.Length)
            suffixStart = fileInfo.Length;
        if (suffixStart < prefixEnd)
        {
            prefixEnd = 0;
            suffixStart = fileInfo.Length;
        }

        var encoding = ResolveEncoding(analysis.EncodingName);

        var spans = _scanner.ScanAsync(
            filePath,
            encoding,
            scanStartOffsetBytes: prefixEnd,
            scanEndOffsetBytesExclusive: suffixStart,
            tagName: selected.TagName,
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
