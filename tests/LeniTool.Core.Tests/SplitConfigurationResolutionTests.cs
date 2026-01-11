using System.Text;
using LeniTool.Core.Models;
using LeniTool.Core.Services;
using Shouldly;
using Xunit;

namespace LeniTool.Core.Tests;

public sealed class SplitConfigurationResolutionTests
{
    [Fact]
    public void ResolveForFile_UsesPrecedence_TransientOverFileOverProfileOverGlobal()
    {
        var config = new SplitConfiguration
        {
            MaxChunkSizeMB = 10,
            RecordTagName = "Global",
            AutoDetectRecordTag = true,
            ExtensionProfiles = new()
            {
                [".txt"] = new SplitConfigurationOverrides
                {
                    MaxChunkSizeMB = 9,
                    RecordTagName = "Profile",
                    AutoDetectRecordTag = false
                }
            },
            FileOverrides = new()
            {
                [Path.GetFullPath("C:/tmp/a.txt")] = new SplitConfigurationOverrides
                {
                    MaxChunkSizeMB = 8,
                    RecordTagName = "File"
                }
            }
        };

        config.RunOverrides = new SplitRunOverrides
        {
            GlobalOverride = new SplitConfigurationOverrides
            {
                MaxChunkSizeMB = 7,
                RecordTagName = "RunGlobal"
            },
            FileOverrides = new()
            {
                [Path.GetFullPath("C:/tmp/a.txt")] = new SplitConfigurationOverrides
                {
                    MaxChunkSizeMB = 6,
                    RecordTagName = "RunFile",
                    AutoDetectRecordTag = true
                }
            }
        };

        var resolved = config.ResolveForFile("C:/tmp/a.txt");

        resolved.MaxChunkSizeMB.ShouldBe(6);
        resolved.RecordTagName.ShouldBe("RunFile");
        resolved.AutoDetectRecordTag.ShouldBeTrue();
    }

    [Fact]
    public async Task ConfigurationService_LoadConfiguration_BackwardsCompatibleWithOldJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "config.json");

            // Old config shape (no extensionProfiles/fileOverrides/runOverrides/recordTagName fields)
            var oldJson = "{\n" +
                          "  \"maxChunkSizeMB\": 12.5,\n" +
                          "  \"segmentationTags\": [\"<div\"],\n" +
                          "  \"closingTags\": [\"</body>\"],\n" +
                          "  \"openingTags\": [\"<body>\"],\n" +
                          "  \"namingPattern\": \"{filename}_part{number}.html\",\n" +
                          "  \"outputDirectory\": \"out\",\n" +
                          "  \"maxParallelFiles\": 2\n" +
                          "}\n";

            await File.WriteAllTextAsync(configPath, oldJson, Encoding.UTF8);

            var service = new ConfigurationService(tempDir);
            var loaded = await service.LoadConfigurationAsync();

            loaded.ShouldNotBeNull();
            loaded.MaxChunkSizeMB.ShouldBe(12.5);
            loaded.SegmentationTags.ShouldBe(new[] { "<div" });
            loaded.OutputDirectory.ShouldBe("out");

            loaded.ExtensionProfiles.ShouldBeNull();
            loaded.FileOverrides.ShouldBeNull();
            loaded.RecordTagName.ShouldBeNull();
            loaded.AutoDetectRecordTag.ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TxtMarkupSplitter_UsesProfileChunkSizeAndConfiguredRecordTag_WhenPresent()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        var inputFile = Path.Combine(testDir, "input.txt");
        var outputDir = Path.Combine(testDir, "out");

        // Two repeating tags: many <A>, fewer <B>. Analyzer would likely prefer A.
        var recordsA = string.Join("", Enumerable.Range(1, 30).Select(i => $"  <A>AA{i}</A>\n"));
        var recordsB = string.Join("", Enumerable.Range(1, 10).Select(i => $"  <B>BB{i}</B>\n"));
        var content = "<Root>\n" + recordsA + recordsB + "</Root>\n";

        await File.WriteAllTextAsync(inputFile, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var config = new SplitConfiguration
        {
            // Global max is large enough that we'd normally NOT split.
            MaxChunkSizeMB = 1.0,
            ExtensionProfiles = new()
            {
                [".txt"] = new SplitConfigurationOverrides
                {
                    // Force splitting by making the target chunk tiny.
                    MaxChunkSizeMB = 0.0002,
                    RecordTagName = "B",
                    AutoDetectRecordTag = false
                }
            }
        };

        var service = new TxtMarkupSplitterService(config);

        try
        {
            var outputs = await service.SplitFileAsync(inputFile, outputDir);

            outputs.Count.ShouldBeGreaterThan(1);
            foreach (var file in outputs)
            {
                var text = await File.ReadAllTextAsync(file, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                text.ShouldContain("<Root>");
                text.ShouldContain("</Root>");

                // Since we forced recordTagName=B, every chunk should contain only whole <B> records.
                CountOccurrences(text, "<B>").ShouldBe(CountOccurrences(text, "</B>"));
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
