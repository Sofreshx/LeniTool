using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LeniTool.Core.Services;

public static class OutputFileNamer
{
    private const string FilenameToken = "{filename}";
    private const string NumberToken = "{number}";

    public static string BuildPartFileName(
        string? namingPattern,
        string sourceFilePath,
        int partNumber,
        int partNumberDigits = 3)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            throw new ArgumentException("Source file path is required.", nameof(sourceFilePath));

        var fileInfo = new FileInfo(sourceFilePath);
        var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
        var extension = fileInfo.Extension;

        var pattern = string.IsNullOrWhiteSpace(namingPattern)
            ? "{filename}_part{number}"
            : namingPattern.Trim();

        // Guard against common user mistake: missing {number} causes overwrites.
        if (!pattern.Contains(NumberToken, StringComparison.OrdinalIgnoreCase))
        {
            var patternNoExt = Path.GetFileNameWithoutExtension(pattern);
            var patternExt = Path.GetExtension(pattern);
            pattern = string.IsNullOrWhiteSpace(patternExt)
                ? patternNoExt + "_part{number}"
                : patternNoExt + "_part{number}" + patternExt;
        }

        if (!pattern.Contains(FilenameToken, StringComparison.OrdinalIgnoreCase))
            pattern = FilenameToken + "_" + pattern;

        var part = partNumberDigits <= 0
            ? partNumber.ToString()
            : partNumber.ToString("D" + partNumberDigits);

        var outputFileName = ReplaceTokenIgnoreCase(pattern, FilenameToken, fileName);
        outputFileName = ReplaceTokenIgnoreCase(outputFileName, NumberToken, part);

        outputFileName = SanitizeFileName(outputFileName);

        // Always use the source file extension.
        outputFileName = Path.ChangeExtension(outputFileName, extension);

        return outputFileName;
    }

    private static string ReplaceTokenIgnoreCase(string input, string token, string replacement)
    {
        if (input.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
            return input;

        var escaped = Regex.Escape(token);
        return Regex.Replace(input, escaped, replacement ?? string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return fileName;

        var invalid = Path.GetInvalidFileNameChars();
        if (!fileName.Any(c => invalid.Contains(c)))
            return fileName;

        var chars = fileName
            .Select(c => invalid.Contains(c) ? '_' : c)
            .ToArray();

        return new string(chars);
    }
}
