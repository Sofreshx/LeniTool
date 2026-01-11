using System.Text;
using LeniTool.Core.Services;
using Shouldly;
using Xunit;

namespace LeniTool.Core.Tests;

public sealed class MarkupAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_DetectsRecordTagAndWrapper_ForSimpleWrapperWithRepeatedRecords_Utf8()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "input.txt");

        // Wrapper <Example> contains repeated <Ficher> records.
        var content = "<Example>\n" +
                      "  <Ficher>One</Ficher>\n" +
                      "  <Ficher>Two</Ficher>\n" +
                      "</Example>\n";

        await File.WriteAllTextAsync(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        try
        {
            var analyzer = new MarkupAnalyzer();

            var result = await analyzer.AnalyzeAsync(filePath, targetMaxChunkBytes: 10);

            result.EncodingName.ShouldBe("utf-8");
            result.FileSizeBytes.ShouldBeGreaterThan(0);
            result.CandidateRecords.ShouldNotBeEmpty();

            var best = result.CandidateRecords[0];
            best.TagName.ShouldBe("Ficher");
            best.CountEstimate.ShouldBe(2);
            best.Confidence.ShouldBeGreaterThan(0);

            result.WrapperRange.ShouldNotBeNull();
            result.WrapperRange!.PrefixEndOffsetBytes.ShouldBe(best.FirstOpenOffsetBytes);
            result.WrapperRange!.SuffixStartOffsetBytes.ShouldBe(best.LastCloseEndOffsetBytes);
            result.WrapperRange!.PrefixEndOffsetBytes.ShouldBeGreaterThan(0);
            result.WrapperRange!.SuffixStartOffsetBytes.ShouldBeGreaterThan(result.WrapperRange!.PrefixEndOffsetBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsUtf16AndComputesEvenByteOffsets()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "input-utf16.txt");

        var content = "<Root>\n" +
                      "  <Item>A</Item>\n" +
                      "  <Item>B</Item>\n" +
                      "</Root>\n";

        var utf16LeWithBom = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        await File.WriteAllTextAsync(filePath, content, utf16LeWithBom);

        try
        {
            var analyzer = new MarkupAnalyzer();
            var result = await analyzer.AnalyzeAsync(filePath, targetMaxChunkBytes: 10);

            result.EncodingName.ShouldBe("utf-16");
            result.HasBom.ShouldBeTrue();
            result.BomLengthBytes.ShouldBe(2);

            var best = result.CandidateRecords[0];
            best.TagName.ShouldBe("Item");
            best.CountEstimate.ShouldBe(2);

            // UTF-16 offsets should align to 2-byte boundaries.
            (best.FirstOpenOffsetBytes % 2).ShouldBe(0);
            (best.LastCloseEndOffsetBytes % 2).ShouldBe(0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
