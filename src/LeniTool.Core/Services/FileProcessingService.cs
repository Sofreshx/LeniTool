using System.Diagnostics;
using LeniTool.Core.Models;

namespace LeniTool.Core.Services;

/// <summary>
/// Handles batch file processing with parallel execution support
/// </summary>
public class FileProcessingService
{
    private readonly SplitConfiguration _config;
    private readonly SplitterStrategyRegistry _registry;

    public FileProcessingService(SplitConfiguration config)
        : this(config, CreateDefaultRegistry(config))
    {
    }

    public FileProcessingService(SplitConfiguration config, SplitterStrategyRegistry registry)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Processes multiple files with optional parallel execution
    /// </summary>
    public async Task<List<SplitResult>> ProcessFilesAsync(
        IEnumerable<string> filePaths,
        string outputDirectory,
        bool parallel = true,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SplitResult>();
        var files = filePaths.ToList();

        if (parallel)
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = _config.MaxParallelFiles,
                CancellationToken = cancellationToken
            };

            var resultsList = new List<SplitResult>();
            var lockObj = new object();

            await Parallel.ForEachAsync(files, options, async (filePath, ct) =>
            {
                var result = await ProcessSingleFileAsync(filePath, outputDirectory, progress, ct);
                lock (lockObj)
                {
                    resultsList.Add(result);
                }
            });

            results = resultsList;
        }
        else
        {
            foreach (var filePath in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var result = await ProcessSingleFileAsync(filePath, outputDirectory, progress, cancellationToken);
                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    /// Processes a single file
    /// </summary>
    private async Task<SplitResult> ProcessSingleFileAsync(
        string filePath,
        string outputDirectory,
        IProgress<ProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SplitResult
        {
            OriginalFilePath = filePath
        };

        try
        {
            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.ErrorMessage = "File not found";
                return result;
            }

            var fileInfo = new FileInfo(filePath);
            result.OriginalSizeBytes = fileInfo.Length;

            if (!_registry.TryGetByFilePath(filePath, out var strategy) || strategy is null)
            {
                result.Success = false;
                result.ErrorMessage = $"Unsupported file type: {fileInfo.Extension}";
                return result;
            }

            progress?.Report(new ProcessingProgress
            {
                FileName = fileInfo.Name,
                Status = "Starting split operation..."
            });

            var outputFiles = await strategy.SplitFileAsync(filePath, outputDirectory, progress, cancellationToken);
            
            result.OutputFiles = outputFiles;
            result.Success = true;

            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;

            progress?.Report(new ProcessingProgress
            {
                FileName = fileInfo.Name,
                PercentComplete = 100,
                Status = $"Complete - {outputFiles.Count} chunks created in {stopwatch.Elapsed.TotalSeconds:F2}s"
            });
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Operation cancelled";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;

            progress?.Report(new ProcessingProgress
            {
                FileName = Path.GetFileName(filePath),
                Status = $"Error: {ex.Message}"
            });
        }

        return result;
    }

    /// <summary>
    /// Validates files before processing
    /// </summary>
    public (bool isValid, List<string> errors) ValidateFiles(IEnumerable<string> filePaths)
    {
        var errors = new List<string>();

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                errors.Add($"File not found: {filePath}");
                continue;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!_registry.SupportsExtension(extension))
            {
                errors.Add($"Unsupported file type: {filePath}");
            }
        }

        return (errors.Count == 0, errors);
    }

    private static SplitterStrategyRegistry CreateDefaultRegistry(SplitConfiguration config)
    {
        var registry = new SplitterStrategyRegistry();
        registry.Register(new HtmlSplitterService(config));
        registry.Register(new TxtMarkupSplitterService(config));
        // NOTE: PDF is intentionally not enabled by default yet.
        // A placeholder strategy exists (`PdfSplitterService`) to reserve the extension point,
        // but it throws on split until real PDF support is implemented.
        // registry.Register(new PdfSplitterService(config));
        return registry;
    }
}
