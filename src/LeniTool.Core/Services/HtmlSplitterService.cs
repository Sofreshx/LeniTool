using System.Text;
using LeniTool.Core.Models;

namespace LeniTool.Core.Services;

/// <summary>
/// Core service for splitting HTML files into chunks
/// </summary>
public class HtmlSplitterService
{
    private readonly SplitConfiguration _config;

    public HtmlSplitterService(SplitConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Splits an HTML file into multiple chunks based on configuration
    /// </summary>
    /// <remarks>
    /// Note: Current implementation works with character positions in strings.
    /// The MaxChunkSizeBytes configuration is treated as character count for simplicity.
    /// For UTF-8 text, actual byte size may vary (1-4 bytes per character).
    /// This is acceptable for HTML which is mostly ASCII/single-byte characters.
    /// </remarks>
    public async Task<List<string>> SplitFileAsync(string filePath, string outputDirectory, IProgress<ProcessingProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        // Read entire file
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var fileInfo = new FileInfo(filePath);
        var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
        var extension = fileInfo.Extension;

        // If file is smaller than max size, no need to split
        if (fileInfo.Length <= _config.MaxChunkSizeBytes)
        {
            progress?.Report(new ProcessingProgress
            {
                FileName = fileInfo.Name,
                Status = "File is already under size limit - no split needed"
            });
            return new List<string> { filePath };
        }

        // Find split points
        var splitPoints = FindSplitPoints(content);
        
        // Create chunks
        var chunks = CreateChunks(content, splitPoints);

        // Save chunks
        var outputFiles = new List<string>();
        Directory.CreateDirectory(outputDirectory);

        for (int i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var outputFileName = _config.NamingPattern
                .Replace("{filename}", fileName)
                .Replace("{number}", (i + 1).ToString("D3"));
            
            if (!outputFileName.EndsWith(extension))
                outputFileName += extension;

            var outputPath = Path.Combine(outputDirectory, outputFileName);
            await File.WriteAllTextAsync(outputPath, chunks[i], Encoding.UTF8, cancellationToken);
            outputFiles.Add(outputPath);

            progress?.Report(new ProcessingProgress
            {
                FileName = fileInfo.Name,
                CurrentChunk = i + 1,
                TotalChunks = chunks.Count,
                PercentComplete = ((i + 1) / (double)chunks.Count) * 100,
                Status = $"Created chunk {i + 1}/{chunks.Count}"
            });
        }

        return outputFiles;
    }

    /// <summary>
    /// Finds optimal split points in the HTML content
    /// </summary>
    private List<int> FindSplitPoints(string content)
    {
        var splitPoints = new List<int> { 0 }; // Always start at 0
        var currentPosition = 0;
        var targetChunkSize = _config.MaxChunkSizeBytes;

        while (currentPosition < content.Length)
        {
            var nextPosition = currentPosition + (int)targetChunkSize;

            // If we're at the end, add it and break
            if (nextPosition >= content.Length)
            {
                if (currentPosition < content.Length)
                    splitPoints.Add(content.Length);
                break;
            }

            // Find the best split point near the target size
            var splitPoint = FindNearestSplitTag(content, nextPosition, currentPosition);
            
            if (splitPoint > currentPosition)
            {
                splitPoints.Add(splitPoint);
                currentPosition = splitPoint;
            }
            else
            {
                // Fallback: split at target size if no tag found
                splitPoints.Add(nextPosition);
                currentPosition = nextPosition;
            }
        }

        return splitPoints;
    }

    /// <summary>
    /// Finds the nearest segmentation tag to the target position
    /// </summary>
    private int FindNearestSplitTag(string content, int targetPosition, int minPosition)
    {
        const int searchWindow = 50000; // Search 50KB before and after target
        var searchStart = Math.Max(minPosition, targetPosition - searchWindow);
        var searchEnd = Math.Min(content.Length, targetPosition + searchWindow);

        int bestPosition = -1;
        int bestDistance = int.MaxValue;

        // Try each segmentation tag in order of priority
        foreach (var tag in _config.SegmentationTags)
        {
            var pos = searchStart;
            while (pos < searchEnd)
            {
                var index = content.IndexOf(tag, pos, StringComparison.OrdinalIgnoreCase);
                if (index == -1 || index >= searchEnd)
                    break;

                var distance = Math.Abs(index - targetPosition);
                if (distance < bestDistance && index > minPosition)
                {
                    bestDistance = distance;
                    bestPosition = index;
                }

                pos = index + 1;
            }

            // If we found a good match with high-priority tag, use it
            if (bestPosition != -1 && bestDistance < searchWindow / 2)
                break;
        }

        return bestPosition;
    }

    /// <summary>
    /// Creates chunks from content and split points
    /// </summary>
    private List<string> CreateChunks(string content, List<int> splitPoints)
    {
        var chunks = new List<string>();

        for (int i = 0; i < splitPoints.Count - 1; i++)
        {
            var start = splitPoints[i];
            var end = splitPoints[i + 1];
            var chunk = content.Substring(start, end - start);

            // Add opening tags for chunks after the first
            if (i > 0 && _config.OpeningTags.Any())
            {
                var opening = string.Join(Environment.NewLine, _config.OpeningTags);
                chunk = opening + Environment.NewLine + chunk;
            }

            // Add closing tags to all chunks except the last (which should already have them)
            if (i < splitPoints.Count - 2 && _config.ClosingTags.Any())
            {
                var closing = string.Join(Environment.NewLine, _config.ClosingTags);
                chunk = chunk + Environment.NewLine + closing;
            }

            chunks.Add(chunk);
        }

        return chunks;
    }

    /// <summary>
    /// Estimates the number of chunks that will be created
    /// </summary>
    public int EstimateChunkCount(long fileSizeBytes)
    {
        if (fileSizeBytes <= _config.MaxChunkSizeBytes)
            return 1;

        return (int)Math.Ceiling((double)fileSizeBytes / _config.MaxChunkSizeBytes);
    }
}
