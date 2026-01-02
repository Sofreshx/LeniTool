using LeniTool.Core.Models;
using LeniTool.Core.Services;
using Shouldly;
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
