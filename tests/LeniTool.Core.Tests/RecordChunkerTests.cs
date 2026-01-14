using LeniTool.Core.Models;
using LeniTool.Core.Services;
using Shouldly;
using Xunit;

namespace LeniTool.Core.Tests;

public sealed class RecordChunkerTests
{
    [Fact]
    public async Task WriteChunksAsync_SplitsByTargetBytes_WithoutBreakingRecordBoundaries()
    {
        var filePath = await TestFixtures.CopyFixtureToTempFileAsync("nested-wrapper.txt", "input.txt");
        var outputDir = Path.Combine(Path.GetDirectoryName(filePath)!, "out");

        try
        {
            var analyzer = new MarkupAnalyzer();
            var analysis = await analyzer.AnalyzeAsync(filePath, targetMaxChunkBytes: 250);

            analysis.CandidateRecords.ShouldNotBeEmpty();
            analysis.WrapperRange.ShouldNotBeNull();

            var tag = analysis.CandidateRecords[0].TagName;
            var prefixEnd = analysis.WrapperRange!.PrefixEndOffsetBytes;
            var suffixStart = analysis.WrapperRange!.SuffixStartOffsetBytes;

            var scanner = new RecordSpanScanner();
            var spans = scanner.ScanAsync(
                filePath,
                TestFixtures.Utf8NoBom,
                scanStartOffsetBytes: prefixEnd,
                scanEndOffsetBytesExclusive: suffixStart,
                tagName: tag);

            var config = new SplitConfiguration
            {
                NamingPattern = "{filename}_part{number}.txt"
            };

            var chunker = new RecordChunker();
            var outputs = await chunker.WriteChunksAsync(
                filePath,
                outputDir,
                config,
                targetMaxChunkBytes: 250,
                prefixEndOffsetBytes: prefixEnd,
                suffixStartOffsetBytes: suffixStart,
                recordSpans: spans);

            outputs.Count.ShouldBeGreaterThan(1);

            foreach (var output in outputs)
            {
                new FileInfo(output).Length.ShouldBeLessThanOrEqualTo(250);

                var text = await File.ReadAllTextAsync(output, TestFixtures.Utf8NoBom);
                text.ShouldStartWith("<Envelope>");
                var normalized = text.Replace("\r\n", "\n");
                normalized.TrimEnd().ShouldEndWith("</Envelope>");

                // Each chunk should contain only whole <Ficher> records.
                CountOccurrences(text, "<Ficher").ShouldBe(CountOccurrences(text, "</Ficher>"));
            }
        }
        finally
        {
            TestFixtures.CleanupTempDirForFile(filePath);
        }
    }

    [Fact]
    public async Task WriteChunksAsync_AllowsSingleRecordLargerThanTarget_AsItsOwnChunk()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var filePath = Path.Combine(tempDir, "single-large.txt");
        var outputDir = Path.Combine(tempDir, "out");

        var payload = new string('Z', 5000);
        var content = "<Root>\n" +
                      $"  <Item>{payload}</Item>\n" +
                      "</Root>\n";

        await File.WriteAllTextAsync(filePath, content, TestFixtures.Utf8NoBom);

        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            var open = TestFixtures.IndexOf(bytes, TestFixtures.Utf8NoBom.GetBytes("<Item"));
            open.ShouldBeGreaterThanOrEqualTo(0);

            var closeNeedle = TestFixtures.Utf8NoBom.GetBytes("</Item>");
            var closeStart = TestFixtures.IndexOf(bytes, closeNeedle);
            closeStart.ShouldBeGreaterThan(open);

            var closeEndExclusive = closeStart + closeNeedle.Length;

            async IAsyncEnumerable<RecordSpan> OneSpan()
            {
                yield return new RecordSpan(open, closeEndExclusive);
                await Task.CompletedTask;
            }

            var config = new SplitConfiguration
            {
                NamingPattern = "{filename}_part{number}.txt"
            };

            var chunker = new RecordChunker();
            var outputs = await chunker.WriteChunksAsync(
                filePath,
                outputDir,
                config,
                targetMaxChunkBytes: 2000,
                prefixEndOffsetBytes: open,
                suffixStartOffsetBytes: closeEndExclusive,
                recordSpans: OneSpan());

            outputs.Count.ShouldBe(1);

            var outPath = outputs[0];
            new FileInfo(outPath).Length.ShouldBeGreaterThan(2000);

            var outBytes = await File.ReadAllBytesAsync(outPath);
            outBytes.ShouldBe(bytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private static int CountOccurrences(string text, string needle)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(needle))
            return 0;

        var count = 0;
        var idx = 0;
        while (true)
        {
            idx = text.IndexOf(needle, idx, StringComparison.Ordinal);
            if (idx < 0)
                break;
            count++;
            idx += needle.Length;
        }

        return count;
    }
}
