using LeniTool.Core.Services;
using Shouldly;
using System.Text;
using Xunit;

namespace LeniTool.Core.Tests;

public sealed class SplitOutputVerifierTests
{
    [Fact]
    public async Task VerifyTxtMarkupSplitAsync_Passes_ForUtf8()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        var inputFile = Path.Combine(testDir, "input.txt");
        var outputDir = Path.Combine(testDir, "out");

        var records = string.Join("", Enumerable.Range(1, 14)
            .Select(i => $"  <Ficher>Record {i}</Ficher>\n"));
        var content = "<Example>\n" + records + "</Example>\n";
        await File.WriteAllTextAsync(inputFile, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var config = new LeniTool.Core.Models.SplitConfiguration
        {
            MaxChunkSizeMB = 0.0002,
            NamingPattern = "{filename}_part{number}.txt"
        };

        var splitter = new TxtMarkupSplitterService(config);

        try
        {
            var outputs = await splitter.SplitFileAsync(inputFile, outputDir);
            outputs.Count.ShouldBeGreaterThan(1);

            var result = await SplitOutputVerifier.VerifyTxtMarkupSplitAsync(inputFile, outputs, config);
            result.IsSuccess.ShouldBeTrue(result.Failure?.Message);
            result.Failure.ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task VerifyTxtMarkupSplitAsync_Passes_ForUtf16Le_WithBom()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        var inputFile = Path.Combine(testDir, "input-utf16.txt");
        var outputDir = Path.Combine(testDir, "out");

        var records = string.Join("", Enumerable.Range(1, 12)
            .Select(i => $"  <Item>V{i}</Item>\r\n"));

        var content = "<Root>\r\n" + records + "</Root>\r\n";
        var utf16LeWithBom = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        await File.WriteAllTextAsync(inputFile, content, utf16LeWithBom);

        var config = new LeniTool.Core.Models.SplitConfiguration
        {
            MaxChunkSizeMB = 0.0002,
            NamingPattern = "{filename}_part{number}.txt"
        };

        var splitter = new TxtMarkupSplitterService(config);

        try
        {
            var outputs = await splitter.SplitFileAsync(inputFile, outputDir);
            outputs.Count.ShouldBeGreaterThan(1);

            var result = await SplitOutputVerifier.VerifyTxtMarkupSplitAsync(inputFile, outputs, config);
            result.IsSuccess.ShouldBeTrue(result.Failure?.Message);
            result.Failure.ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task VerifyTxtMarkupSplitAsync_Fails_WithFirstDiff_WhenChunkInnerMutated()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        var inputFile = Path.Combine(testDir, "input.txt");
        var outputDir = Path.Combine(testDir, "out");

        var records = string.Join("", Enumerable.Range(1, 16)
            .Select(i => $"  <Ficher>Record {i}</Ficher>\n"));
        var content = "<Example>\n" + records + "</Example>\n";
        await File.WriteAllTextAsync(inputFile, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var config = new LeniTool.Core.Models.SplitConfiguration
        {
            MaxChunkSizeMB = 0.0002,
            NamingPattern = "{filename}_part{number}.txt"
        };

        var splitter = new TxtMarkupSplitterService(config);

        try
        {
            var outputs = await splitter.SplitFileAsync(inputFile, outputDir);
            outputs.Count.ShouldBeGreaterThan(1);

            // Mutate the first chunk inside the first record (avoid prefix/suffix).
            var firstChunk = outputs[0];
            var bytes = await File.ReadAllBytesAsync(firstChunk);
            var needle = Encoding.UTF8.GetBytes("<Ficher");
            var idx = IndexOf(bytes, needle);
            idx.ShouldBeGreaterThanOrEqualTo(0);

            var mutateAt = Math.Min(bytes.Length - 1, idx + needle.Length + 10);
            bytes[mutateAt] ^= 0xFF;
            await File.WriteAllBytesAsync(firstChunk, bytes);

            var result = await SplitOutputVerifier.VerifyTxtMarkupSplitAsync(inputFile, outputs, config);
            result.IsSuccess.ShouldBeFalse();
            result.Failure.ShouldNotBeNull();

            result.Failure!.Kind.ShouldBe(SplitOutputVerificationFailureKind.InnerMismatch);
            result.Failure.ChunkIndex.ShouldBe(0);
            result.Failure.FirstDiffOffsetInInnerBytes.ShouldNotBeNull();
            result.Failure.ExpectedHexSnippet.ShouldNotBeNullOrWhiteSpace();
            result.Failure.ActualHexSnippet.ShouldNotBeNullOrWhiteSpace();
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        if (haystack.Length == 0 || needle.Length == 0 || needle.Length > haystack.Length)
            return -1;

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return i;
        }

        return -1;
    }
}
