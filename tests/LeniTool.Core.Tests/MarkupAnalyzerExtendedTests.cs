using LeniTool.Core.Services;
using Shouldly;
using System.Text;
using Xunit;

namespace LeniTool.Core.Tests;

public sealed class MarkupAnalyzerExtendedTests
{
    [Fact]
    public async Task AnalyzeAsync_OrdersCandidatesByConfidenceDescending()
    {
        var filePath = await TestFixtures.CopyFixtureToTempFileAsync("multi-tag-ambiguity.txt", "input.txt");

        try
        {
            var analyzer = new MarkupAnalyzer();
            var result = await analyzer.AnalyzeAsync(filePath, targetMaxChunkBytes: 1024);

            result.CandidateRecords.Count.ShouldBeGreaterThan(1);

            for (var i = 0; i < result.CandidateRecords.Count - 1; i++)
            {
                result.CandidateRecords[i].Confidence.ShouldBeGreaterThanOrEqualTo(result.CandidateRecords[i + 1].Confidence);
            }

            result.CandidateRecords[0].TagName.ShouldBe("A");
        }
        finally
        {
            TestFixtures.CleanupTempDirForFile(filePath);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_SamplingRestrictionCanExcludeTagsOnlySeenAfterSampleLimit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "sampled.txt");

        // Build a file where <A> is frequent early, and <B> is frequent only after the sample limit.
        // When sampling is enabled, the analyzer first chooses a small set of tag names from the sample,
        // and then scans the full file restricted to those tag names.
        var early = string.Join("", Enumerable.Range(1, 120).Select(i => $"<A>Early{i}</A>\n"));
        var padding = new string('X', 1500); // force early region to exceed small sample sizes
        var late = string.Join("", Enumerable.Range(1, 400).Select(i => $"<B>Late{i}</B>\n"));
        var content = "<Root>\n" + early + padding + "\n" + late + "</Root>\n";

        await File.WriteAllTextAsync(filePath, content, TestFixtures.Utf8NoBom);

        try
        {
            var analyzer = new MarkupAnalyzer();

            var sampled = await analyzer.AnalyzeAsync(
                filePath,
                targetMaxChunkBytes: 1024,
                maxCandidates: 3,
                sampleLimitBytes: 1024);

            sampled.CandidateRecords.ShouldNotBeEmpty();
            sampled.CandidateRecords.Any(c => c.TagName == "B").ShouldBeFalse();
            sampled.CandidateRecords[0].TagName.ShouldBe("A");

            var full = await analyzer.AnalyzeAsync(
                filePath,
                targetMaxChunkBytes: 1024,
                maxCandidates: 3,
                sampleLimitBytes: long.MaxValue);

            full.CandidateRecords.ShouldNotBeEmpty();
            full.CandidateRecords.Any(c => c.TagName == "B").ShouldBeTrue();
            full.CandidateRecords[0].TagName.ShouldBe("B");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_ToleratesMalformedMarkup_StillProducesCandidates_WhenRepeatedOpenTagsExist()
    {
        var filePath = await TestFixtures.CopyFixtureToTempFileAsync("malformed-tags.txt", "input.txt");

        try
        {
            var analyzer = new MarkupAnalyzer();
            var result = await analyzer.AnalyzeAsync(filePath, targetMaxChunkBytes: 1024);

            result.CandidateRecords.ShouldNotBeEmpty();
            result.CandidateRecords[0].TagName.ShouldBe("Item");
            result.CandidateRecords[0].Confidence.ShouldBeLessThan(1.0);
        }
        finally
        {
            TestFixtures.CleanupTempDirForFile(filePath);
        }
    }
}
