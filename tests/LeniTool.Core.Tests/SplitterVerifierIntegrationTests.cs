using LeniTool.Core.Models;
using LeniTool.Core.Services;
using Shouldly;
using System.Text;
using Xunit;

namespace LeniTool.Core.Tests;

public sealed class SplitterVerifierIntegrationTests
{
    [Fact]
    public async Task Split_Then_Verify_Passes_ForNestedEnvelopeHeaderPayloadFooter_Fixture()
    {
        var inputFile = await TestFixtures.CopyFixtureToTempFileAsync(
            "nested-envelope-header-payload-footer.txt",
            destinationFileName: "input.txt");

        var testDir = Path.GetDirectoryName(inputFile);
        testDir.ShouldNotBeNull();

        var outputDir = Path.Combine(testDir!, "out");
        Directory.CreateDirectory(outputDir);

        var config = new SplitConfiguration
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
            TestFixtures.CleanupTempDirForFile(inputFile);
        }
    }

    [Fact]
    public async Task Split_Then_Verify_Fails_WhenOneOutputIsCorrupted()
    {
        var inputFile = await TestFixtures.CopyFixtureToTempFileAsync(
            "nested-envelope-header-payload-footer.txt",
            destinationFileName: "input.txt");

        var testDir = Path.GetDirectoryName(inputFile);
        testDir.ShouldNotBeNull();

        var outputDir = Path.Combine(testDir!, "out");
        Directory.CreateDirectory(outputDir);

        var config = new SplitConfiguration
        {
            MaxChunkSizeMB = 0.0002,
            NamingPattern = "{filename}_part{number}.txt"
        };

        var splitter = new TxtMarkupSplitterService(config);

        try
        {
            var outputs = await splitter.SplitFileAsync(inputFile, outputDir);
            outputs.Count.ShouldBeGreaterThan(1);

            // Mutate a chunk inside the first record start (avoid prefix/suffix).
            var chunkToCorrupt = outputs[0];
            var bytes = await File.ReadAllBytesAsync(chunkToCorrupt);

            var needle = Encoding.UTF8.GetBytes("<Ficher");
            var idx = TestFixtures.IndexOf(bytes, needle);
            idx.ShouldBeGreaterThanOrEqualTo(0);

            var mutateAt = Math.Min(bytes.Length - 1, idx + needle.Length + 12);
            bytes[mutateAt] ^= 0xFF;
            await File.WriteAllBytesAsync(chunkToCorrupt, bytes);

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
            TestFixtures.CleanupTempDirForFile(inputFile);
        }
    }
}
