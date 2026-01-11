using System.Buffers;
using System.Text;
using LeniTool.Core.Models;

namespace LeniTool.Core.Services;

/// <summary>
/// Streaming-friendly analyzer for XML-like / HTML-like markup.
/// Detects repeating tag names as candidate "record" elements and wrapper boundaries.
/// </summary>
public sealed class MarkupAnalyzer
{
    public const int DefaultMaxCandidates = 3;
    public const int DefaultBufferSizeBytes = 64 * 1024;
    public const int DefaultCarryBytes = 8 * 1024;
    public const long DefaultSampleLimitBytes = 2L * 1024 * 1024;

    public async Task<AnalysisResult> AnalyzeAsync(
        string filePath,
        long targetMaxChunkBytes,
        int maxCandidates = DefaultMaxCandidates,
        long sampleLimitBytes = DefaultSampleLimitBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        var (encoding, bomLength) = await DetectEncodingAsync(filePath, cancellationToken).ConfigureAwait(false);
        var encodingName = NormalizeEncodingName(encoding);

        var extension = fileInfo.Extension;
        var fileSize = fileInfo.Length;

        // Prefer sampling for very large files: identify likely candidates quickly,
        // then do a second streaming pass restricted to the top candidates.
        var tagNamesToTrack = (fileSize > sampleLimitBytes)
            ? await SampleTopTagNamesAsync(filePath, encoding, bomLength, sampleLimitBytes, cancellationToken).ConfigureAwait(false)
            : null;

        var stats = await ScanAsync(filePath, encoding, bomLength, tagNamesToTrack, cancellationToken).ConfigureAwait(false);
        var candidates = ComputeCandidates(stats, fileSize, maxCandidates);

        WrapperRange? wrapper = null;
        var confidence = 0d;
        if (candidates.Count > 0)
        {
            var best = candidates[0];
            confidence = best.Confidence;

            if (best.FirstOpenOffsetBytes >= 0 && best.LastCloseEndOffsetBytes > best.FirstOpenOffsetBytes)
            {
                wrapper = new WrapperRange
                {
                    PrefixEndOffsetBytes = best.FirstOpenOffsetBytes,
                    SuffixStartOffsetBytes = best.LastCloseEndOffsetBytes
                };
            }
        }

        var estimatedParts = 0;
        if (targetMaxChunkBytes > 0)
            estimatedParts = (int)Math.Max(1, (long)Math.Ceiling(fileSize / (double)targetMaxChunkBytes));

        return new AnalysisResult
        {
            FilePath = filePath,
            Extension = extension,
            FileSizeBytes = fileSize,
            StrategyName = nameof(MarkupAnalyzer),
            EncodingName = encodingName,
            HasBom = bomLength > 0,
            BomLengthBytes = bomLength,
            CandidateRecords = candidates,
            WrapperRange = wrapper,
            Confidence = confidence,
            EstimatedPartCount = estimatedParts
        };
    }

    private static async Task<(Encoding encoding, int bomLength)> DetectEncodingAsync(string filePath, CancellationToken cancellationToken)
    {
        byte[] header = new byte[4];
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken).ConfigureAwait(false);

        if (read >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
            return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 3);

        if (read >= 2 && header[0] == 0xFF && header[1] == 0xFE)
            return (new UnicodeEncoding(bigEndian: false, byteOrderMark: true), 2);

        if (read >= 2 && header[0] == 0xFE && header[1] == 0xFF)
            return (new UnicodeEncoding(bigEndian: true, byteOrderMark: true), 2);

        // Heuristic: if the first bytes show lots of NULs, it's likely UTF-16.
        if (read >= 4)
        {
            var nulCount = 0;
            for (var i = 0; i < read; i++)
            {
                if (header[i] == 0)
                    nulCount++;
            }

            if (nulCount >= 2)
            {
                // Guess LE as default without BOM.
                return (new UnicodeEncoding(bigEndian: false, byteOrderMark: false), 0);
            }
        }

        // Default: UTF-8 (ASCII is a strict subset).
        return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 0);
    }

    private static string NormalizeEncodingName(Encoding encoding)
    {
        // Keep display stable across platforms.
        if (encoding is UnicodeEncoding)
        {
            // 1200 = UTF-16 LE, 1201 = UTF-16 BE
            return encoding.CodePage == 1201 ? "utf-16be" : "utf-16";
        }

        if (encoding is UTF8Encoding)
            return "utf-8";

        return encoding.WebName;
    }

    private static async Task<HashSet<string>> SampleTopTagNamesAsync(
        string filePath,
        Encoding encoding,
        int bomLength,
        long sampleLimitBytes,
        CancellationToken cancellationToken)
    {
        var stats = await ScanAsync(filePath, encoding, bomLength, null, cancellationToken, maxBytesToScan: sampleLimitBytes)
            .ConfigureAwait(false);

        // Track the most frequent names (opens + closes) to reduce second pass work.
        return stats
            .OrderByDescending(kvp => kvp.Value.OpenCount + kvp.Value.CloseCount)
            .Take(12)
            .Select(kvp => kvp.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<Dictionary<string, TagStats>> ScanAsync(
        string filePath,
        Encoding encoding,
        int bomLength,
        HashSet<string>? restrictToTagNames,
        CancellationToken cancellationToken,
        long? maxBytesToScan = null)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (bomLength > 0)
            stream.Seek(bomLength, SeekOrigin.Begin);

        var isUtf16 = encoding is UnicodeEncoding;
        var step = isUtf16 ? 2 : 1;

        var bufferSize = DefaultBufferSizeBytes;
        if (isUtf16 && (bufferSize % 2 != 0))
            bufferSize++;

        var rent = ArrayPool<byte>.Shared.Rent(bufferSize);
        var leftover = Array.Empty<byte>();
        var carryBytes = DefaultCarryBytes;
        if (isUtf16 && (carryBytes % 2 != 0))
            carryBytes++;

        var stats = new Dictionary<string, TagStats>(StringComparer.Ordinal);
        long totalReadAfterBom = 0;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (maxBytesToScan.HasValue && totalReadAfterBom >= maxBytesToScan.Value)
                    break;

                var maxReadThisIter = bufferSize;
                if (maxBytesToScan.HasValue)
                {
                    var remaining = maxBytesToScan.Value - totalReadAfterBom;
                    if (remaining <= 0)
                        break;

                    maxReadThisIter = (int)Math.Min(maxReadThisIter, remaining);
                    if (isUtf16 && (maxReadThisIter % 2 != 0))
                        maxReadThisIter--;
                }

                var readStartOffset = bomLength + totalReadAfterBom;
                var read = await stream.ReadAsync(rent.AsMemory(0, maxReadThisIter), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                    break;

                if (isUtf16 && (read % 2 != 0))
                {
                    // Keep the last odd byte as leftover.
                    read -= 1;
                }

                totalReadAfterBom += read;

                var combined = new byte[leftover.Length + read];
                if (leftover.Length > 0)
                    Buffer.BlockCopy(leftover, 0, combined, 0, leftover.Length);
                Buffer.BlockCopy(rent, 0, combined, leftover.Length, read);

                var combinedStartOffset = readStartOffset - leftover.Length;

                var newLeftoverLength = Math.Min(carryBytes, combined.Length);
                if (isUtf16 && (newLeftoverLength % 2 != 0))
                    newLeftoverLength--;
                var processLength = combined.Length - newLeftoverLength;
                if (processLength < 0)
                    processLength = 0;

                ScanBuffer(combined, processLength, combinedStartOffset, step, restrictToTagNames, stats);

                leftover = newLeftoverLength > 0
                    ? combined.AsSpan(combined.Length - newLeftoverLength, newLeftoverLength).ToArray()
                    : Array.Empty<byte>();
            }

            // Process any remaining bytes (best-effort).
            if (leftover.Length > 0)
            {
                var combinedStartOffset = bomLength + totalReadAfterBom - leftover.Length;
                ScanBuffer(leftover, leftover.Length, combinedStartOffset, step, restrictToTagNames, stats);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }

        return stats;
    }

    private static void ScanBuffer(
        ReadOnlySpan<byte> buffer,
        int processLength,
        long bufferStartOffset,
        int step,
        HashSet<string>? restrictToTagNames,
        Dictionary<string, TagStats> stats)
    {
        var i = 0;
        var limit = Math.Max(0, processLength - step);

        while (i <= limit)
        {
            if (!IsCharAt(buffer, i, step, '<'))
            {
                i += step;
                continue;
            }

            var tagStart = i;
            var j = i + step;
            if (j > limit)
                break;

            // Skip declarations and comments: <! ...>  or <? ...>
            if (IsCharAt(buffer, j, step, '!') || IsCharAt(buffer, j, step, '?'))
            {
                var end = FindChar(buffer, j + step, processLength, step, '>');
                if (end < 0)
                    break;

                i = end + step;
                continue;
            }

            var isClose = false;
            if (IsCharAt(buffer, j, step, '/'))
            {
                isClose = true;
                j += step;
            }

            if (j > limit)
                break;

            // Parse tag name
            var nameStart = j;
            if (!TryReadChar(buffer, nameStart, step, out var first) || !IsNameStart(first))
            {
                i += step;
                continue;
            }

            j += step;
            while (j <= limit)
            {
                if (!TryReadChar(buffer, j, step, out var ch))
                    break;

                if (!IsNameChar(ch))
                    break;

                j += step;
            }

            var nameBytesLength = j - nameStart;
            if (nameBytesLength <= 0)
            {
                i += step;
                continue;
            }

            var name = ReadAsciiName(buffer.Slice(nameStart, nameBytesLength), step);
            if (string.IsNullOrWhiteSpace(name))
            {
                i += step;
                continue;
            }

            if (restrictToTagNames is not null && !restrictToTagNames.Contains(name))
            {
                // Skip scanning to end of tag to keep offsets correct.
                var end = FindChar(buffer, j, processLength, step, '>');
                if (end < 0)
                    break;
                i = end + step;
                continue;
            }

            var tagEnd = FindChar(buffer, j, processLength, step, '>');
            if (tagEnd < 0)
                break;

            var isSelfClosing = false;
            if (!isClose)
            {
                // Check last non-whitespace char before '>' for '/'
                var k = tagEnd - step;
                while (k >= 0 && k >= tagStart)
                {
                    if (!TryReadChar(buffer, k, step, out var ck))
                        break;

                    if (!char.IsWhiteSpace(ck))
                    {
                        isSelfClosing = ck == '/';
                        break;
                    }

                    k -= step;
                }
            }

            var absoluteTagStart = bufferStartOffset + tagStart;
            var absoluteTagEndExclusive = bufferStartOffset + tagEnd + step;

            if (!stats.TryGetValue(name, out var tagStats))
            {
                tagStats = new TagStats
                {
                    FirstOpenOffsetBytes = long.MaxValue,
                    LastCloseEndOffsetBytes = -1
                };
                stats[name] = tagStats;
            }

            if (isClose)
            {
                tagStats.CloseCount++;
                if (absoluteTagEndExclusive > tagStats.LastCloseEndOffsetBytes)
                    tagStats.LastCloseEndOffsetBytes = absoluteTagEndExclusive;
            }
            else
            {
                tagStats.OpenCount++;
                if (absoluteTagStart < tagStats.FirstOpenOffsetBytes)
                    tagStats.FirstOpenOffsetBytes = absoluteTagStart;

                if (isSelfClosing)
                {
                    tagStats.CloseCount++;
                    if (absoluteTagEndExclusive > tagStats.LastCloseEndOffsetBytes)
                        tagStats.LastCloseEndOffsetBytes = absoluteTagEndExclusive;
                }
            }

            i = tagEnd + step;
        }
    }

    private static List<CandidateRecord> ComputeCandidates(Dictionary<string, TagStats> stats, long fileSizeBytes, int maxCandidates)
    {
        var candidates = stats
            .Where(kvp => kvp.Value.OpenCount >= 2)
            .Select(kvp =>
            {
                var name = kvp.Key;
                var s = kvp.Value;

                var open = s.OpenCount;
                var close = s.CloseCount;
                var balance = open == 0 ? 0d : 1d - (Math.Abs(open - close) / (double)Math.Max(open, close));

                var repetition = Math.Min(1d, open / 20d);
                var span = 0d;
                if (s.FirstOpenOffsetBytes != long.MaxValue && s.LastCloseEndOffsetBytes > s.FirstOpenOffsetBytes && fileSizeBytes > 0)
                    span = Math.Min(1d, (s.LastCloseEndOffsetBytes - s.FirstOpenOffsetBytes) / (double)fileSizeBytes);

                var score = (repetition * 0.6) + (balance * 0.3) + (span * 0.1);
                score = Math.Clamp(score, 0d, 1d);

                return new CandidateRecord
                {
                    TagName = name,
                    FirstOpenOffsetBytes = s.FirstOpenOffsetBytes == long.MaxValue ? -1 : s.FirstOpenOffsetBytes,
                    LastCloseEndOffsetBytes = s.LastCloseEndOffsetBytes,
                    CountEstimate = open,
                    Confidence = score
                };
            })
            .OrderByDescending(c => c.Confidence)
            .ThenByDescending(c => c.CountEstimate)
            .Take(Math.Max(1, maxCandidates))
            .ToList();

        return candidates;
    }

    private sealed class TagStats
    {
        public int OpenCount;
        public int CloseCount;
        public long FirstOpenOffsetBytes;
        public long LastCloseEndOffsetBytes;
    }

    private static bool TryReadChar(ReadOnlySpan<byte> buffer, int index, int step, out char ch)
    {
        ch = default;
        if (index < 0)
            return false;

        if (step == 1)
        {
            if (index >= buffer.Length)
                return false;
            ch = (char)buffer[index];
            return true;
        }

        if (index + 1 >= buffer.Length)
            return false;

        // UTF-16 (LE/BE) - detect endianness by looking for NUL pattern is unreliable here;
        // instead treat both by checking which byte is likely NUL for ASCII.
        // If either byte is NUL, assume the other is the ASCII code point.
        var b0 = buffer[index];
        var b1 = buffer[index + 1];
        if (b0 == 0 && b1 != 0)
        {
            ch = (char)b1; // BE ASCII
            return true;
        }

        if (b1 == 0 && b0 != 0)
        {
            ch = (char)b0; // LE ASCII
            return true;
        }

        // Fallback to LE for non-ASCII (best effort)
        ch = (char)(b0 | (b1 << 8));
        return true;
    }

    private static bool IsCharAt(ReadOnlySpan<byte> buffer, int index, int step, char expected)
        => TryReadChar(buffer, index, step, out var ch) && ch == expected;

    private static int FindChar(ReadOnlySpan<byte> buffer, int startIndex, int processLength, int step, char expected)
    {
        var limit = Math.Max(0, processLength - step);
        for (var i = startIndex; i <= limit; i += step)
        {
            if (IsCharAt(buffer, i, step, expected))
                return i;
        }
        return -1;
    }

    private static bool IsNameStart(char c)
        => char.IsLetter(c) || c == '_' || c == ':';

    private static bool IsNameChar(char c)
        => IsNameStart(c) || char.IsDigit(c) || c == '-' || c == '.';

    private static string ReadAsciiName(ReadOnlySpan<byte> nameBytes, int step)
    {
        if (nameBytes.IsEmpty)
            return string.Empty;

        if (step == 1)
        {
            // Tag names are ASCII by convention in markup.
            return Encoding.ASCII.GetString(nameBytes);
        }

        var chars = new char[nameBytes.Length / step];
        var idx = 0;
        for (var i = 0; i + 1 < nameBytes.Length; i += step)
        {
            // See TryReadChar: one of the bytes should be NUL for ASCII.
            var b0 = nameBytes[i];
            var b1 = nameBytes[i + 1];
            chars[idx++] = b0 == 0 ? (char)b1 : (char)b0;
        }

        return new string(chars, 0, idx);
    }
}
