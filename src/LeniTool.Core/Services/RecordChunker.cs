using System.Buffers;
using LeniTool.Core.Models;

namespace LeniTool.Core.Services;

public sealed class RecordChunker
{
    private const int CopyBufferSizeBytes = 64 * 1024;

    public async Task<List<string>> WriteChunksAsync(
        string filePath,
        string outputDirectory,
        SplitConfiguration config,
        long targetMaxChunkBytes,
        long prefixEndOffsetBytes,
        long suffixStartOffsetBytes,
        IAsyncEnumerable<RecordSpan> recordSpans,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (config is null)
            throw new ArgumentNullException(nameof(config));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        var fileInfo = new FileInfo(filePath);
        var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
        var extension = fileInfo.Extension;

        Directory.CreateDirectory(outputDirectory);

        await using var input = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: CopyBufferSizeBytes,
            options: FileOptions.RandomAccess);

        if (prefixEndOffsetBytes < 0)
            prefixEndOffsetBytes = 0;
        if (suffixStartOffsetBytes < 0 || suffixStartOffsetBytes > fileInfo.Length)
            suffixStartOffsetBytes = fileInfo.Length;
        if (prefixEndOffsetBytes > suffixStartOffsetBytes)
        {
            prefixEndOffsetBytes = 0;
            suffixStartOffsetBytes = fileInfo.Length;
        }

        var prefixBytes = await ReadRangeAsync(input, startOffsetBytes: 0, lengthBytes: prefixEndOffsetBytes, cancellationToken)
            .ConfigureAwait(false);
        var suffixBytes = await ReadRangeAsync(input, startOffsetBytes: suffixStartOffsetBytes, lengthBytes: fileInfo.Length - suffixStartOffsetBytes, cancellationToken)
            .ConfigureAwait(false);

        var overheadBytes = (long)prefixBytes.Length + suffixBytes.Length;
        if (targetMaxChunkBytes <= 0)
            targetMaxChunkBytes = fileInfo.Length;

        var outputFiles = new List<string>();

        FileStream? currentOut = null;
        long currentSizeBytes = 0;
        var currentRecordCount = 0;
        var partNumber = 0;

        try
        {
            await foreach (var span in recordSpans.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (span.LengthBytes <= 0)
                    continue;

                if (span.StartOffsetBytes < 0 || span.EndOffsetBytes > fileInfo.Length || span.StartOffsetBytes >= span.EndOffsetBytes)
                    continue;

                if (currentOut is null)
                {
                    (currentOut, partNumber, currentSizeBytes) = await StartNewChunkAsync(partNumber).ConfigureAwait(false);
                    currentRecordCount = 0;
                }

                var wouldBeSize = currentSizeBytes + span.LengthBytes;

                if (currentRecordCount > 0 && wouldBeSize > targetMaxChunkBytes)
                {
                    var finishedPath = currentOut.Name;
                    await FinalizeChunkAsync(currentOut).ConfigureAwait(false);
                    outputFiles.Add(finishedPath);
                    currentOut = null;

                    (currentOut, partNumber, currentSizeBytes) = await StartNewChunkAsync(partNumber).ConfigureAwait(false);
                    currentRecordCount = 0;
                    wouldBeSize = currentSizeBytes + span.LengthBytes;
                }

                if (currentRecordCount == 0 && overheadBytes + span.LengthBytes > targetMaxChunkBytes)
                {
                    progress?.Report(new ProcessingProgress
                    {
                        FileName = fileInfo.Name,
                        Status = $"Single record larger than target ({targetMaxChunkBytes} bytes) - writing as its own chunk"
                    });
                }

                await CopyRangeAsync(input, currentOut, span.StartOffsetBytes, span.LengthBytes, cancellationToken).ConfigureAwait(false);
                currentSizeBytes = wouldBeSize;
                currentRecordCount++;
            }

            if (currentOut is not null && currentRecordCount > 0)
            {
                var finishedPath = currentOut.Name;
                await FinalizeChunkAsync(currentOut).ConfigureAwait(false);
                outputFiles.Add(finishedPath);
                currentOut = null;
            }
        }
        finally
        {
            if (currentOut is not null)
                await currentOut.DisposeAsync().ConfigureAwait(false);
        }

        return outputFiles;

        async Task<(FileStream stream, int nextPartNumber, long initialSizeBytes)> StartNewChunkAsync(int currentPart)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var newPart = currentPart + 1;

            var outputFileName = config.NamingPattern
                .Replace("{filename}", fileName)
                .Replace("{number}", newPart.ToString("D3"));

            outputFileName = Path.ChangeExtension(outputFileName, extension);

            var outputPath = Path.Combine(outputDirectory, outputFileName);

            var stream = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: CopyBufferSizeBytes,
                options: FileOptions.SequentialScan);

            if (prefixBytes.Length > 0)
                await stream.WriteAsync(prefixBytes, cancellationToken).ConfigureAwait(false);

            progress?.Report(new ProcessingProgress
            {
                FileName = fileInfo.Name,
                CurrentChunk = newPart,
                Status = $"Writing chunk {newPart}..."
            });

            return (stream, newPart, prefixBytes.Length + suffixBytes.Length);
        }

        async Task FinalizeChunkAsync(FileStream stream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (suffixBytes.Length > 0)
                await stream.WriteAsync(suffixBytes, cancellationToken).ConfigureAwait(false);

            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<byte[]> ReadRangeAsync(
        FileStream input,
        long startOffsetBytes,
        long lengthBytes,
        CancellationToken cancellationToken)
    {
        if (lengthBytes <= 0)
            return Array.Empty<byte>();

        if (lengthBytes > int.MaxValue)
            throw new InvalidOperationException("Prefix/suffix too large to buffer in memory.");

        input.Seek(startOffsetBytes, SeekOrigin.Begin);

        var buffer = new byte[(int)lengthBytes];
        var offset = 0;
        while (offset < buffer.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await input.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                break;
            offset += read;
        }

        if (offset == buffer.Length)
            return buffer;

        return buffer.AsSpan(0, offset).ToArray();
    }

    private static async Task CopyRangeAsync(
        FileStream input,
        FileStream output,
        long startOffsetBytes,
        long lengthBytes,
        CancellationToken cancellationToken)
    {
        input.Seek(startOffsetBytes, SeekOrigin.Begin);

        var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSizeBytes);
        try
        {
            long remaining = lengthBytes;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var toRead = (int)Math.Min(buffer.Length, remaining);
                var read = await input.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                    break;

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
