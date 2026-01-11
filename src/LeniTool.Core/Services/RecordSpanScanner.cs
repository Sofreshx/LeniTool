using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace LeniTool.Core.Services;

public sealed class RecordSpanScanner
{
    private const int DefaultBufferSizeBytes = 64 * 1024;

    public async IAsyncEnumerable<RecordSpan> ScanAsync(
        string filePath,
        Encoding encoding,
        long scanStartOffsetBytes,
        long scanEndOffsetBytesExclusive,
        string tagName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (string.IsNullOrWhiteSpace(tagName))
            yield break;

        if (scanEndOffsetBytesExclusive <= scanStartOffsetBytes)
            yield break;

        var openPattern = encoding.GetBytes("<" + tagName);
        var closePattern = encoding.GetBytes("</" + tagName + ">");

        if (openPattern.Length == 0 || closePattern.Length == 0)
            yield break;

        var openMatcher = new KmpMatcher(openPattern);
        var closeMatcher = new KmpMatcher(closePattern);

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: DefaultBufferSizeBytes,
            options: FileOptions.SequentialScan);

        stream.Seek(scanStartOffsetBytes, SeekOrigin.Begin);

        var remaining = scanEndOffsetBytesExclusive - scanStartOffsetBytes;
        var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSizeBytes);
        long absoluteOffset = scanStartOffsetBytes;

        var state = ScanState.SearchOpen;
        long recordStart = 0;
        long recordCloseEndExclusive = 0;

        try
        {
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var toRead = (int)Math.Min(buffer.Length, remaining);
                var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                    break;

                for (var i = 0; i < read; i++)
                {
                    var pos = absoluteOffset + i;
                    var b = buffer[i];

                    switch (state)
                    {
                        case ScanState.SearchOpen:
                            if (openMatcher.Step(b))
                            {
                                recordStart = pos - openPattern.Length + 1;
                                state = ScanState.SearchClose;
                                closeMatcher.Reset();
                            }
                            break;

                        case ScanState.SearchClose:
                            if (closeMatcher.Step(b))
                            {
                                recordCloseEndExclusive = pos + 1;
                                state = ScanState.SearchNextOpen;
                                openMatcher.Reset();
                            }
                            break;

                        case ScanState.SearchNextOpen:
                            if (openMatcher.Step(b))
                            {
                                var nextOpenStart = pos - openPattern.Length + 1;
                                if (nextOpenStart > recordStart)
                                    yield return new RecordSpan(recordStart, nextOpenStart);

                                recordStart = nextOpenStart;
                                state = ScanState.SearchClose;
                                closeMatcher.Reset();
                            }
                            break;
                    }
                }

                absoluteOffset += read;
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (state == ScanState.SearchNextOpen && recordCloseEndExclusive > recordStart)
            yield return new RecordSpan(recordStart, recordCloseEndExclusive);
    }

    private enum ScanState
    {
        SearchOpen,
        SearchClose,
        SearchNextOpen
    }

    private sealed class KmpMatcher
    {
        private readonly byte[] _pattern;
        private readonly int[] _lps;
        private int _j;

        public KmpMatcher(byte[] pattern)
        {
            _pattern = pattern;
            _lps = BuildLps(pattern);
            _j = 0;
        }

        public void Reset() => _j = 0;

        public bool Step(byte b)
        {
            while (_j > 0 && b != _pattern[_j])
                _j = _lps[_j - 1];

            if (b == _pattern[_j])
                _j++;

            if (_j == _pattern.Length)
            {
                _j = _lps[_j - 1];
                return true;
            }

            return false;
        }

        private static int[] BuildLps(byte[] pattern)
        {
            var lps = new int[pattern.Length];
            var len = 0;

            for (var i = 1; i < pattern.Length;)
            {
                if (pattern[i] == pattern[len])
                {
                    len++;
                    lps[i] = len;
                    i++;
                }
                else if (len != 0)
                {
                    len = lps[len - 1];
                }
                else
                {
                    lps[i] = 0;
                    i++;
                }
            }

            return lps;
        }
    }
}
