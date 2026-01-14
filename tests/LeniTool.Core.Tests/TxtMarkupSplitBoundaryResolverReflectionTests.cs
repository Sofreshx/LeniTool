using System.Reflection;
using LeniTool.Core.Models;
using Shouldly;
using Xunit;

namespace LeniTool.Core.Tests;

public sealed class TxtMarkupSplitBoundaryResolverReflectionTests
{
    [Fact]
    public void TryResolve_ReturnsNull_WhenAutoDetectDisabledAndNoConfiguredTag()
    {
        var analysis = new AnalysisResult
        {
            FilePath = "c:/tmp/input.txt",
            Extension = ".txt",
            FileSizeBytes = 123,
            StrategyName = "Test",
            CandidateRecords = new()
        };

        var config = new SplitConfiguration
        {
            AutoDetectRecordTag = false,
            RecordTagName = null
        };

        var (boundaries, failureReason) = InvokeTryResolve(analysis, config, fileLengthBytes: 123);

        boundaries.ShouldBeNull();
        failureReason.ShouldNotBeNullOrWhiteSpace();
        failureReason!.ShouldContain("auto-detection", Case.Insensitive);
    }

    [Fact]
    public void TryResolve_FallsBackToFullFile_WhenConfiguredTagNotDetectedAndOffsetsMissing()
    {
        // No candidates include the configured tag, so the resolver will use full-file boundaries.
        var analysis = new AnalysisResult
        {
            FilePath = "c:/tmp/input.txt",
            Extension = ".txt",
            FileSizeBytes = 1000,
            StrategyName = "Test",
            CandidateRecords = new()
            {
                new CandidateRecord { TagName = "A", FirstOpenOffsetBytes = 10, LastCloseEndOffsetBytes = 900, CountEstimate = 50, Confidence = 0.9 }
            }
        };

        var config = new SplitConfiguration
        {
            AutoDetectRecordTag = false,
            RecordTagName = "B"
        };

        var (boundaries, failureReason) = InvokeTryResolve(analysis, config, fileLengthBytes: 1000);

        failureReason.ShouldBeNull();
        boundaries.ShouldNotBeNull();

        boundaries!.TagName.ShouldBe("B");
        boundaries.PrefixEndOffsetBytes.ShouldBe(0);
        boundaries.SuffixStartOffsetBytes.ShouldBe(1000);
    }

    [Fact]
    public void TryResolve_UsesSelectedCandidateOffsets_WhenConfiguredTagDetected()
    {
        var analysis = new AnalysisResult
        {
            FilePath = "c:/tmp/input.txt",
            Extension = ".txt",
            FileSizeBytes = 1000,
            StrategyName = "Test",
            WrapperRange = new WrapperRange { PrefixEndOffsetBytes = 123, SuffixStartOffsetBytes = 456 },
            CandidateRecords = new()
            {
                new CandidateRecord { TagName = "A", FirstOpenOffsetBytes = 10, LastCloseEndOffsetBytes = 900, CountEstimate = 50, Confidence = 0.9 },
                new CandidateRecord { TagName = "B", FirstOpenOffsetBytes = 111, LastCloseEndOffsetBytes = 888, CountEstimate = 5, Confidence = 0.2 }
            }
        };

        var config = new SplitConfiguration
        {
            AutoDetectRecordTag = true,
            RecordTagName = "B"
        };

        var (boundaries, failureReason) = InvokeTryResolve(analysis, config, fileLengthBytes: 1000);

        failureReason.ShouldBeNull();
        boundaries.ShouldNotBeNull();

        boundaries!.TagName.ShouldBe("B");
        boundaries.PrefixEndOffsetBytes.ShouldBe(111);
        boundaries.SuffixStartOffsetBytes.ShouldBe(888);
    }

    private static (ResolvedBoundaries? boundaries, string? failureReason) InvokeTryResolve(
        AnalysisResult analysis,
        SplitConfiguration config,
        long fileLengthBytes)
    {
        var resolverType = typeof(LeniTool.Core.Services.TxtMarkupSplitterService).Assembly
            .GetType("LeniTool.Core.Services.TxtMarkupSplitBoundaryResolver", throwOnError: true);

        var method = resolverType!.GetMethod(
            "TryResolve",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(AnalysisResult), typeof(SplitConfiguration), typeof(long), typeof(string).MakeByRefType() },
            modifiers: null);

        method.ShouldNotBeNull();

        var args = new object?[] { analysis, config.ResolveForFile(analysis.FilePath), fileLengthBytes, null };
        var raw = method!.Invoke(null, args);
        var failureReason = args[3] as string;

        if (raw is null)
            return (null, failureReason);

        var tagName = (string)raw.GetType().GetProperty("TagName")!.GetValue(raw)!;
        var prefixEnd = (long)raw.GetType().GetProperty("PrefixEndOffsetBytes")!.GetValue(raw)!;
        var suffixStart = (long)raw.GetType().GetProperty("SuffixStartOffsetBytes")!.GetValue(raw)!;

        return (new ResolvedBoundaries(tagName, prefixEnd, suffixStart), failureReason);
    }

    private sealed record ResolvedBoundaries(string TagName, long PrefixEndOffsetBytes, long SuffixStartOffsetBytes);
}
