using LeniTool.Core.Models;
using LeniTool.Core.Services;
using Shouldly;
using System.Text;
using Xunit;

namespace LeniTool.Core.Tests;

public class ConfigurationServiceTests
{
    private readonly string _testConfigDirectory;

    public ConfigurationServiceTests()
    {
        _testConfigDirectory = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testConfigDirectory);
    }

    [Fact]
    public async Task LoadConfiguration_CreatesDefaultWhenNotExists()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory);

        // Act
        var config = await service.LoadConfigurationAsync();

        // Assert
        config.ShouldNotBeNull();
        config.MaxChunkSizeMB.ShouldBe(4.5);
        config.SegmentationTags.ShouldNotBeEmpty();
        File.Exists(service.GetConfigFilePath()).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAndLoadConfiguration_Roundtrip()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory);
        var config = new SplitConfiguration
        {
            MaxChunkSizeMB = 10.0,
            SegmentationTags = new List<string> { "<test>", "<example>" },
            NamingPattern = "custom_{filename}_{number}.html"
        };

        // Act
        await service.SaveConfigurationAsync(config);
        var loaded = await service.LoadConfigurationAsync();

        // Assert
        loaded.MaxChunkSizeMB.ShouldBe(10.0);
        loaded.SegmentationTags.ShouldBe(new[] { "<test>", "<example>" });
        loaded.NamingPattern.ShouldBe("custom_{filename}_{number}.html");
    }

    [Fact]
    public void SplitConfiguration_Validation()
    {
        // Arrange & Act
        var validConfig = new SplitConfiguration();
        var invalidConfig1 = new SplitConfiguration { MaxChunkSizeMB = -1 };
        var invalidConfig2 = new SplitConfiguration { SegmentationTags = new List<string>() };
        var invalidConfig3 = new SplitConfiguration { NamingPattern = "no_placeholders.html" };

        // Assert
        validConfig.IsValid(out _).ShouldBeTrue();
        invalidConfig1.IsValid(out var error1).ShouldBeFalse();
        error1.ShouldContain("greater than 0");
        invalidConfig2.IsValid(out var error2).ShouldBeFalse();
        error2.ShouldContain("At least one");
        invalidConfig3.IsValid(out var error3).ShouldBeFalse();
        error3.ShouldContain("must contain");
    }
}

public class HtmlSplitterServiceTests
{
    [Fact]
    public void EstimateChunkCount_ReturnsCorrectEstimate()
    {
        // Arrange
        var config = new SplitConfiguration { MaxChunkSizeMB = 1.0 };
        var service = new HtmlSplitterService(config);

        // Act
        var count1 = service.EstimateChunkCount(500_000); // 0.5 MB
        var count2 = service.EstimateChunkCount(1_500_000); // 1.5 MB
        var count3 = service.EstimateChunkCount(5_000_000); // 5 MB

        // Assert
        count1.ShouldBe(1);
        count2.ShouldBe(2);
        count3.ShouldBe(5);
    }

    [Fact]
    public async Task SplitFile_ThrowsWhenFileNotFound()
    {
        // Arrange
        var config = new SplitConfiguration();
        var service = new HtmlSplitterService(config);

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(async () =>
            await service.SplitFileAsync("nonexistent.html", "output"));
    }

    [Fact]
    public async Task SplitFile_NoSplitWhenUnderLimit()
    {
        // Arrange
        var testFile = Path.GetTempFileName();
        var smallContent = "<html><body><p>Small content</p></body></html>";
        await File.WriteAllTextAsync(testFile, smallContent);

        var config = new SplitConfiguration { MaxChunkSizeMB = 1.0 };
        var service = new HtmlSplitterService(config);
        var outputDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());

        try
        {
            // Act
            var results = await service.SplitFileAsync(testFile, outputDir);

            // Assert
            results.Count.ShouldBe(1);
            results[0].ShouldBe(testFile); // Returns original file when no split needed
        }
        finally
        {
            File.Delete(testFile);
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }
}

public class FileProcessingServiceTests
{
    private sealed class FakeSplitterStrategy : ISplitterStrategy
    {
        public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".fake" };

        public int SplitCalls { get; private set; }
        public string? LastSplitFilePath { get; private set; }
        public string? LastOutputDirectory { get; private set; }

        public Task<AnalysisResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(filePath);
            return Task.FromResult(new AnalysisResult
            {
                FilePath = filePath,
                Extension = info.Extension,
                FileSizeBytes = info.Exists ? info.Length : 0,
                StrategyName = nameof(FakeSplitterStrategy)
            });
        }

        public Task<List<string>> SplitFileAsync(
            string filePath,
            string outputDirectory,
            IProgress<ProcessingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SplitCalls++;
            LastSplitFilePath = filePath;
            LastOutputDirectory = outputDirectory;
            return Task.FromResult(new List<string> { Path.Combine(outputDirectory, "dummy.fake") });
        }
    }

    [Fact]
    public async Task ProcessFiles_SelectsStrategyByExtension()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        var inputFile = Path.Combine(testDir, "input.fake");
        await File.WriteAllTextAsync(inputFile, "hello");
        var outputDir = Path.Combine(testDir, "out");

        var config = new SplitConfiguration();
        var fake = new FakeSplitterStrategy();
        var registry = new SplitterStrategyRegistry(new[] { fake });
        var service = new FileProcessingService(config, registry);

        try
        {
            // Act
            var results = await service.ProcessFilesAsync(new[] { inputFile }, outputDir, parallel: false);

            // Assert
            results.Count.ShouldBe(1);
            results[0].Success.ShouldBeTrue();
            results[0].OutputFiles.Count.ShouldBe(1);
            fake.SplitCalls.ShouldBe(1);
            fake.LastSplitFilePath.ShouldBe(inputFile);
            fake.LastOutputDirectory.ShouldBe(outputDir);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task ValidateFiles_UsesRegistryInsteadOfHardcodedHtml()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        var inputFile = Path.Combine(testDir, "input.fake");
        await File.WriteAllTextAsync(inputFile, "hello");

        var config = new SplitConfiguration();
        var registry = new SplitterStrategyRegistry(new[] { new FakeSplitterStrategy() });
        var service = new FileProcessingService(config, registry);

        try
        {
            // Act
            var (isValid, errors) = service.ValidateFiles(new[] { inputFile });

            // Assert
            isValid.ShouldBeTrue();
            errors.ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task ValidateFiles_AllowsTxt_WhenUsingDefaultRegistry()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        var inputFile = Path.Combine(testDir, "input.txt");
        await File.WriteAllTextAsync(inputFile, "hello");

        var config = new SplitConfiguration();
        var service = new FileProcessingService(config);

        try
        {
            // Act
            var (isValid, errors) = service.ValidateFiles(new[] { inputFile });

            // Assert
            isValid.ShouldBeTrue();
            errors.ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }
}

public sealed class TxtMarkupSplitterServiceTests
{
    [Fact]
    public async Task SplitFile_PreservesWrapperAndWholeRecords_Utf8()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        var inputFile = Path.Combine(testDir, "input.txt");
        var outputDir = Path.Combine(testDir, "out");

        var records = string.Join("", Enumerable.Range(1, 12)
            .Select(i => $"  <Ficher>Record {i}</Ficher>\n"));

        var content = "<Example>\n" + records + "</Example>\n";
        await File.WriteAllTextAsync(inputFile, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var config = new SplitConfiguration { MaxChunkSizeMB = 0.0002 };
        var service = new TxtMarkupSplitterService(config);

        try
        {
            var outputs = await service.SplitFileAsync(inputFile, outputDir);

            outputs.Count.ShouldBeGreaterThan(1);
            foreach (var file in outputs)
            {
                var text = await File.ReadAllTextAsync(file, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                text.ShouldContain("<Example>");
                text.ShouldContain("</Example>");

                CountOccurrences(text, "<Ficher>").ShouldBe(CountOccurrences(text, "</Ficher>"));
            }
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task SplitFile_PreservesWrapperAndWholeRecords_Utf16Le_WithBom()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        var inputFile = Path.Combine(testDir, "input-utf16.txt");
        var outputDir = Path.Combine(testDir, "out");

        var records = string.Join("", Enumerable.Range(1, 10)
            .Select(i => $"  <Item>V{i}</Item>\r\n"));

        var content = "<Root>\r\n" + records + "</Root>\r\n";
        var utf16LeWithBom = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        await File.WriteAllTextAsync(inputFile, content, utf16LeWithBom);

        var config = new SplitConfiguration { MaxChunkSizeMB = 0.0002 };
        var service = new TxtMarkupSplitterService(config);

        try
        {
            var outputs = await service.SplitFileAsync(inputFile, outputDir);

            outputs.Count.ShouldBeGreaterThan(1);
            foreach (var file in outputs)
            {
                var text = await File.ReadAllTextAsync(file, utf16LeWithBom);
                text.ShouldContain("<Root>");
                text.ShouldContain("</Root>");

                CountOccurrences(text, "<Item>").ShouldBe(CountOccurrences(text, "</Item>"));
            }
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
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
