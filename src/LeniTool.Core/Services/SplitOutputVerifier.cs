using System.Buffers;
using System.Text;
using LeniTool.Core.Models;

namespace LeniTool.Core.Services;

public static class SplitOutputVerifier
{
    private const int CopyBufferSizeBytes = 64 * 1024;
    private const int HexContextBytes = 16;

    public static async Task<SplitOutputVerificationResult> VerifyTxtMarkupSplitAsync(
        string sourceFilePath,
        IReadOnlyList<string> chunkFilePaths,
        SplitConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (configuration is null)
            throw new ArgumentNullException(nameof(configuration));

        var analyzer = new MarkupAnalyzer();
        var resolved = configuration.ResolveForFile(sourceFilePath);
        var analysis = await analyzer
            .AnalyzeAsync(sourceFilePath, targetMaxChunkBytes: resolved.MaxChunkSizeBytes, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var fileLength = new FileInfo(sourceFilePath).Length;
        var boundaries = TxtMarkupSplitBoundaryResolver.TryResolve(analysis, resolved, fileLength, out var failureReason);
        if (boundaries is null)
        {
            return SplitOutputVerificationResult.Fail(new SplitOutputVerificationFailure
            {
                Kind = SplitOutputVerificationFailureKind.CannotResolveBoundaries,
                Message = failureReason ?? "Cannot resolve TXT markup boundaries."
            });
        }

        return await VerifyAsync(
                sourceFilePath,
                chunkFilePaths,
                prefixEndOffsetBytes: boundaries.Value.PrefixEndOffsetBytes,
                suffixStartOffsetBytes: boundaries.Value.SuffixStartOffsetBytes,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<SplitOutputVerificationResult> VerifyAsync(
        string sourceFilePath,
        IReadOnlyList<string> chunkFilePaths,
        long prefixEndOffsetBytes,
        long suffixStartOffsetBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("Source file path is required.", nameof(sourceFilePath));
        if (chunkFilePaths is null)
            throw new ArgumentNullException(nameof(chunkFilePaths));

        if (!File.Exists(sourceFilePath))
        {
            return SplitOutputVerificationResult.Fail(new SplitOutputVerificationFailure
            {
                Kind = SplitOutputVerificationFailureKind.SourceNotFound,
                Message = $"Source file not found: {sourceFilePath}"
            });
        }

        if (chunkFilePaths.Count == 0)
        {
            return SplitOutputVerificationResult.Fail(new SplitOutputVerificationFailure
            {
                Kind = SplitOutputVerificationFailureKind.NoChunksProvided,
                Message = "No chunk files provided."
            });
        }

        var sourceInfo = new FileInfo(sourceFilePath);
        var sourceLength = sourceInfo.Length;

        SanitizeBoundaries(ref prefixEndOffsetBytes, ref suffixStartOffsetBytes, sourceLength);

        var expectedPrefixLength = prefixEndOffsetBytes;
        var expectedSuffixLength = sourceLength - suffixStartOffsetBytes;
        var expectedInnerLength = suffixStartOffsetBytes - prefixEndOffsetBytes;

        var actualInnerLength = 0L;
        for (var i = 0; i < chunkFilePaths.Count; i++)
        {
            var path = chunkFilePaths[i];
            if (!File.Exists(path))
            {
                return SplitOutputVerificationResult.Fail(new SplitOutputVerificationFailure
                {
                    Kind = SplitOutputVerificationFailureKind.ChunkNotFound,
                    ChunkIndex = i,
                    ChunkPath = path,
                    Message = $"Chunk file not found: {path}",
                    ExpectedInnerLengthBytes = expectedInnerLength,
                    ActualInnerLengthBytes = actualInnerLength
                });
            }

            var chunkLength = new FileInfo(path).Length;
            var minLength = expectedPrefixLength + expectedSuffixLength;
            if (chunkLength < minLength)
            {
                return SplitOutputVerificationResult.Fail(new SplitOutputVerificationFailure
                {
                    Kind = SplitOutputVerificationFailureKind.InvalidChunkLength,
                    ChunkIndex = i,
                    ChunkPath = path,
                    Message = $"Chunk too small to contain prefix+suffix. ChunkLength={chunkLength}, PrefixLength={expectedPrefixLength}, SuffixLength={expectedSuffixLength}",
                    ExpectedInnerLengthBytes = expectedInnerLength,
                    ActualInnerLengthBytes = actualInnerLength
                });
            }

            actualInnerLength += (chunkLength - minLength);
        }

        // 1) Validate prefix/suffix equality for every chunk.
        await using (var source = new FileStream(
                         sourceFilePath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read,
                         bufferSize: CopyBufferSizeBytes,
                         options: FileOptions.RandomAccess))
        {
            for (var i = 0; i < chunkFilePaths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = chunkFilePaths[i];

                await using var chunk = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: CopyBufferSizeBytes,
                    options: FileOptions.RandomAccess);

                if (expectedPrefixLength > 0)
                {
                    var prefixMismatch = await CompareRangesAsync(
                            source, aStartOffsetBytes: 0,
                            chunk, bStartOffsetBytes: 0,
                            lengthBytes: expectedPrefixLength,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (prefixMismatch is not null)
                    {
                        return SplitOutputVerificationResult.Fail(new SplitOutputVerificationFailure
                        {
                            Kind = SplitOutputVerificationFailureKind.PrefixMismatch,
                            ChunkIndex = i,
                            ChunkPath = path,
                            Message = $"Prefix mismatch in chunk {i} at prefixOffset={prefixMismatch.Value.DiffOffsetBytes}",
                            ExpectedInnerLengthBytes = expectedInnerLength,
                            ActualInnerLengthBytes = actualInnerLength,
                            ExpectedHexSnippet = prefixMismatch.Value.ExpectedHex,
                            ActualHexSnippet = prefixMismatch.Value.ActualHex
                        });
                    }
                }

                if (expectedSuffixLength > 0)
                {
                    var chunkLen = chunk.Length;
                    var suffixMismatch = await CompareRangesAsync(
                            source, aStartOffsetBytes: suffixStartOffsetBytes,
                            chunk, bStartOffsetBytes: chunkLen - expectedSuffixLength,
                            lengthBytes: expectedSuffixLength,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (suffixMismatch is not null)
                    {
                        return SplitOutputVerificationResult.Fail(new SplitOutputVerificationFailure
                        {
                            Kind = SplitOutputVerificationFailureKind.SuffixMismatch,
                            ChunkIndex = i,
                            ChunkPath = path,
                            Message = $"Suffix mismatch in chunk {i} at suffixOffset={suffixMismatch.Value.DiffOffsetBytes}",
                            ExpectedInnerLengthBytes = expectedInnerLength,
                            ActualInnerLengthBytes = actualInnerLength,
                            ExpectedHexSnippet = suffixMismatch.Value.ExpectedHex,
                            ActualHexSnippet = suffixMismatch.Value.ActualHex
                        });
                    }
                }
            }
        }

        // 2) Validate inner concatenation equals source inner.
        await using (var sourceInner = new FileStream(
                         sourceFilePath,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read,
                         bufferSize: CopyBufferSizeBytes,
                         options: FileOptions.SequentialScan))
        {
            sourceInner.Seek(prefixEndOffsetBytes, SeekOrigin.Begin);

            var expectedRemaining = expectedInnerLength;
            var globalInnerOffset = 0L;

            var expectedBuffer = ArrayPool<byte>.Shared.Rent(CopyBufferSizeBytes);
            var actualBuffer = ArrayPool<byte>.Shared.Rent(CopyBufferSizeBytes);
            try
            {
                for (var i = 0; i < chunkFilePaths.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var path = chunkFilePaths[i];

                    await using var chunk = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: CopyBufferSizeBytes,
                        options: FileOptions.SequentialScan);

                    var chunkInnerLength = chunk.Length - expectedPrefixLength - expectedSuffixLength;
                    chunk.Seek(expectedPrefixLength, SeekOrigin.Begin);

                    var remainingInChunk = chunkInnerLength;
                    while (remainingInChunk > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var toRead = (int)Math.Min(CopyBufferSizeBytes, remainingInChunk);
                        var actualRead = await ReadExactlyOrThrowAsync(chunk, actualBuffer, toRead, cancellationToken)
                            .ConfigureAwait(false);

                        var expectedToRead = actualRead;
                        if (expectedRemaining < expectedToRead)
                            expectedToRead = (int)expectedRemaining;

                        var expectedRead = await ReadExactlyOrThrowAsync(sourceInner, expectedBuffer, expectedToRead, cancellationToken)
                            .ConfigureAwait(false);

                        // If chunks contain more inner bytes than source inner, mismatch.
                        if (expectedRead < actualRead)
                        {
                            var snippet = BuildHexSnippets(expectedBuffer.AsSpan(0, expectedRead), actualBuffer.AsSpan(0, actualRead), diffIndex: expectedRead);
                            return SplitOutputVerificationResult.Fail(new SplitOutputVerificationFailure
                            {
                                Kind = SplitOutputVerificationFailureKind.InnerLengthMismatch,
                                ChunkIndex = i,
                                ChunkPath = path,
                                FirstDiffOffsetInInnerBytes = globalInnerOffset + expectedRead,
                                Message = "Chunk inner data is longer than source inner data.",
                                ExpectedInnerLengthBytes = expectedInnerLength,
                                ActualInnerLengthBytes = actualInnerLength,
                                ExpectedHexSnippet = snippet.ExpectedHex,
                                ActualHexSnippet = snippet.ActualHex
                            });
                        }

                        var mismatchIndex = FirstMismatchIndex(expectedBuffer.AsSpan(0, expectedRead), actualBuffer.AsSpan(0, actualRead));
                        if (mismatchIndex >= 0)
                        {
                            var snippet = BuildHexSnippets(expectedBuffer.AsSpan(0, expectedRead), actualBuffer.AsSpan(0, actualRead), mismatchIndex);
                            return SplitOutputVerificationResult.Fail(new SplitOutputVerificationFailure
                            {
                                Kind = SplitOutputVerificationFailureKind.InnerMismatch,
                                ChunkIndex = i,
                                ChunkPath = path,
                                FirstDiffOffsetInInnerBytes = globalInnerOffset + mismatchIndex,
                                Message = $"Inner mismatch at innerOffset={globalInnerOffset + mismatchIndex} (chunk={i})",
                                ExpectedInnerLengthBytes = expectedInnerLength,
                                ActualInnerLengthBytes = actualInnerLength,
                                ExpectedHexSnippet = snippet.ExpectedHex,
                                ActualHexSnippet = snippet.ActualHex
                            });
                        }

                        globalInnerOffset += expectedRead;
                        expectedRemaining -= expectedRead;
                        remainingInChunk -= actualRead;
                    }
                }

                if (expectedRemaining != 0)
                {
                    return SplitOutputVerificationResult.Fail(new SplitOutputVerificationFailure
                    {
                        Kind = SplitOutputVerificationFailureKind.InnerLengthMismatch,
                        Message = $"Source inner has {expectedRemaining} extra bytes not present in chunk inners.",
                        FirstDiffOffsetInInnerBytes = globalInnerOffset,
                        ExpectedInnerLengthBytes = expectedInnerLength,
                        ActualInnerLengthBytes = actualInnerLength
                    });
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(expectedBuffer);
                ArrayPool<byte>.Shared.Return(actualBuffer);
            }
        }

        return SplitOutputVerificationResult.Ok();
    }

    private static void SanitizeBoundaries(ref long prefixEndOffsetBytes, ref long suffixStartOffsetBytes, long fileLengthBytes)
    {
        if (prefixEndOffsetBytes < 0)
            prefixEndOffsetBytes = 0;
        if (suffixStartOffsetBytes < 0 || suffixStartOffsetBytes > fileLengthBytes)
            suffixStartOffsetBytes = fileLengthBytes;
        if (prefixEndOffsetBytes > suffixStartOffsetBytes)
        {
            prefixEndOffsetBytes = 0;
            suffixStartOffsetBytes = fileLengthBytes;
        }
    }

    private static async Task<int> ReadExactlyOrThrowAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                break;
            offset += read;
        }
        return offset;
    }

    private static int FirstMismatchIndex(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        var min = Math.Min(expected.Length, actual.Length);
        for (var i = 0; i < min; i++)
        {
            if (expected[i] != actual[i])
                return i;
        }
        if (expected.Length != actual.Length)
            return min;
        return -1;
    }

    private static (string ExpectedHex, string ActualHex) BuildHexSnippets(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual, int diffIndex)
    {
        var expectedStart = Math.Max(0, diffIndex - HexContextBytes);
        var expectedEnd = Math.Min(expected.Length, diffIndex + HexContextBytes);
        var actualStart = Math.Max(0, diffIndex - HexContextBytes);
        var actualEnd = Math.Min(actual.Length, diffIndex + HexContextBytes);

        var expectedSlice = expected.Slice(expectedStart, expectedEnd - expectedStart);
        var actualSlice = actual.Slice(actualStart, actualEnd - actualStart);

        return (ToHex(expectedSlice), ToHex(actualSlice));
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        var sb = new StringBuilder(bytes.Length * 3);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(bytes[i].ToString("X2"));
        }
        return sb.ToString();
    }

    private static async Task<RangeMismatch?> CompareRangesAsync(
        FileStream a,
        long aStartOffsetBytes,
        FileStream b,
        long bStartOffsetBytes,
        long lengthBytes,
        CancellationToken cancellationToken)
    {
        if (lengthBytes <= 0)
            return null;

        a.Seek(aStartOffsetBytes, SeekOrigin.Begin);
        b.Seek(bStartOffsetBytes, SeekOrigin.Begin);

        var bufferA = ArrayPool<byte>.Shared.Rent(CopyBufferSizeBytes);
        var bufferB = ArrayPool<byte>.Shared.Rent(CopyBufferSizeBytes);
        try
        {
            long remaining = lengthBytes;
            long comparedOffset = 0;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var toRead = (int)Math.Min(CopyBufferSizeBytes, remaining);

                var readA = await ReadExactlyOrThrowAsync(a, bufferA, toRead, cancellationToken).ConfigureAwait(false);
                var readB = await ReadExactlyOrThrowAsync(b, bufferB, toRead, cancellationToken).ConfigureAwait(false);

                var min = Math.Min(readA, readB);
                var mismatch = FirstMismatchIndex(bufferA.AsSpan(0, readA), bufferB.AsSpan(0, readB));
                if (mismatch >= 0)
                {
                    var snippet = BuildHexSnippets(bufferA.AsSpan(0, readA), bufferB.AsSpan(0, readB), mismatch);
                    return new RangeMismatch(
                        DiffOffsetBytes: comparedOffset + mismatch,
                        ExpectedHex: snippet.ExpectedHex,
                        ActualHex: snippet.ActualHex);
                }

                if (min < toRead)
                {
                    var snippet = BuildHexSnippets(bufferA.AsSpan(0, readA), bufferB.AsSpan(0, readB), min);
                    return new RangeMismatch(
                        DiffOffsetBytes: comparedOffset + min,
                        ExpectedHex: snippet.ExpectedHex,
                        ActualHex: snippet.ActualHex);
                }

                comparedOffset += toRead;
                remaining -= toRead;
            }

            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bufferA);
            ArrayPool<byte>.Shared.Return(bufferB);
        }
    }

    private readonly record struct RangeMismatch(long DiffOffsetBytes, string ExpectedHex, string ActualHex);
}

public sealed class SplitOutputVerificationResult
{
    public bool IsSuccess { get; init; }
    public SplitOutputVerificationFailure? Failure { get; init; }

    public static SplitOutputVerificationResult Ok() => new() { IsSuccess = true };

    public static SplitOutputVerificationResult Fail(SplitOutputVerificationFailure failure) => new()
    {
        IsSuccess = false,
        Failure = failure ?? throw new ArgumentNullException(nameof(failure))
    };
}

public enum SplitOutputVerificationFailureKind
{
    SourceNotFound,
    NoChunksProvided,
    ChunkNotFound,
    CannotResolveBoundaries,
    InvalidChunkLength,
    PrefixMismatch,
    SuffixMismatch,
    InnerMismatch,
    InnerLengthMismatch
}

public sealed class SplitOutputVerificationFailure
{
    public SplitOutputVerificationFailureKind Kind { get; init; }
    public string Message { get; init; } = string.Empty;
    public int? ChunkIndex { get; init; }
    public string? ChunkPath { get; init; }
    public long? FirstDiffOffsetInInnerBytes { get; init; }
    public long ExpectedInnerLengthBytes { get; init; }
    public long ActualInnerLengthBytes { get; init; }
    public string? ExpectedHexSnippet { get; init; }
    public string? ActualHexSnippet { get; init; }
}
