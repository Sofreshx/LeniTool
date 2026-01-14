using LeniTool.Core.Services;
using Shouldly;
using Xunit;

namespace LeniTool.Core.Tests;

public sealed class RecordSpanScannerTests
{
    [Fact]
    public async Task ScanAsync_YieldsNonOverlappingIncreasingSpans_ThatContainWholeRecords()
    {
        var filePath = await TestFixtures.CopyFixtureToTempFileAsync("nested-wrapper.txt", "input.txt");

        try
        {
            var analyzer = new MarkupAnalyzer();
            var analysis = await analyzer.AnalyzeAsync(filePath, targetMaxChunkBytes: 1024);

            analysis.CandidateRecords.ShouldNotBeEmpty();
            analysis.WrapperRange.ShouldNotBeNull();

            var tag = analysis.CandidateRecords[0].TagName;
            tag.ShouldBe("Ficher");

            var prefixEnd = analysis.WrapperRange!.PrefixEndOffsetBytes;
            var suffixStart = analysis.WrapperRange!.SuffixStartOffsetBytes;

            var scanner = new RecordSpanScanner();

            var spans = new List<RecordSpan>();
            await foreach (var span in scanner.ScanAsync(
                               filePath,
                               TestFixtures.Utf8NoBom,
                               scanStartOffsetBytes: prefixEnd,
                               scanEndOffsetBytesExclusive: suffixStart,
                               tagName: tag))
            {
                spans.Add(span);
            }

            spans.Count.ShouldBe(3);

            for (var i = 0; i < spans.Count; i++)
            {
                spans[i].StartOffsetBytes.ShouldBeGreaterThanOrEqualTo(prefixEnd);
                spans[i].EndOffsetBytes.ShouldBeLessThanOrEqualTo(suffixStart);
                spans[i].EndOffsetBytes.ShouldBeGreaterThan(spans[i].StartOffsetBytes);

                if (i > 0)
                    spans[i].StartOffsetBytes.ShouldBeGreaterThanOrEqualTo(spans[i - 1].EndOffsetBytes);
            }

            var bytes = await File.ReadAllBytesAsync(filePath);
            foreach (var span in spans)
            {
                var text = TestFixtures.Utf8NoBom.GetString(bytes, (int)span.StartOffsetBytes, (int)span.LengthBytes);

                text.ShouldContain("<Ficher");
                text.ShouldContain("</Ficher>");
            }
        }
        finally
        {
            TestFixtures.CleanupTempDirForFile(filePath);
        }
    }
}
