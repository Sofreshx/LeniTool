using System.Text;

namespace LeniTool.Core.Tests;

internal static class TestFixtures
{
    public static string GetFixturePath(string fixtureFileName)
    {
        if (string.IsNullOrWhiteSpace(fixtureFileName))
            throw new ArgumentException("Fixture file name is required.", nameof(fixtureFileName));

        return Path.Combine(AppContext.BaseDirectory, "fixtures", fixtureFileName);
    }

    public static async Task<string> CopyFixtureToTempFileAsync(
        string fixtureFileName,
        string? destinationFileName = null,
        CancellationToken cancellationToken = default)
    {
        var sourcePath = GetFixturePath(fixtureFileName);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Fixture not found: {sourcePath}", sourcePath);

        var testDir = Path.Combine(Path.GetTempPath(), "LeniToolTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);

        var destName = destinationFileName ?? fixtureFileName;
        var destPath = Path.Combine(testDir, destName);

        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken);
        await File.WriteAllBytesAsync(destPath, bytes, cancellationToken);

        return destPath;
    }

    public static void CleanupTempDirForFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        var dir = Path.GetDirectoryName(filePath);
        if (dir is null)
            return;

        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    public static int IndexOf(byte[] haystack, byte[] needle)
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

    public static Encoding Utf8NoBom { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}
