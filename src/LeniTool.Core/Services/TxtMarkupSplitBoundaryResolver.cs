using LeniTool.Core.Models;

namespace LeniTool.Core.Services;

internal static class TxtMarkupSplitBoundaryResolver
{
    public static TxtMarkupSplitBoundaries? TryResolve(
        AnalysisResult analysis,
        SplitConfiguration resolvedConfiguration,
        long fileLengthBytes,
        out string? failureReason)
    {
        if (analysis is null)
            throw new ArgumentNullException(nameof(analysis));
        if (resolvedConfiguration is null)
            throw new ArgumentNullException(nameof(resolvedConfiguration));

        failureReason = null;

        var configuredTag = resolvedConfiguration.RecordTagName?.Trim();
        var allowAutoDetect = resolvedConfiguration.AutoDetectRecordTag;
        var hasConfiguredTag = !string.IsNullOrWhiteSpace(configuredTag);

        if (!allowAutoDetect && !hasConfiguredTag)
        {
            failureReason = "Record tag auto-detection disabled and no record tag configured.";
            return null;
        }

        CandidateRecord? selected = null;
        var configuredTagDetected = false;
        if (hasConfiguredTag)
        {
            selected = analysis.CandidateRecords
                .FirstOrDefault(r => string.Equals(r.TagName, configuredTag, StringComparison.OrdinalIgnoreCase));

            configuredTagDetected = selected is not null;

            // If the tag was configured but not detected during analysis, we can still attempt a scan.
            // Wrapper boundaries will fall back to the full file (0..Length).
            if (selected is null)
                selected = new CandidateRecord { TagName = configuredTag! };
        }
        else
        {
            selected = analysis.CandidateRecords.FirstOrDefault();
        }

        if (selected is null || string.IsNullOrWhiteSpace(selected.TagName))
        {
            failureReason = "No repeating record tag detected.";
            return null;
        }

        var prefixEnd = selected.FirstOpenOffsetBytes;
        var suffixStart = selected.LastCloseEndOffsetBytes;

        if (prefixEnd <= 0 && suffixStart <= 0)
        {
            // If we are using a user-configured tag that analysis didn't detect, we can't rely on
            // wrapper boundaries computed for a different candidate. Scan the full file.
            if (hasConfiguredTag && !configuredTagDetected)
            {
                prefixEnd = 0;
                suffixStart = fileLengthBytes;
            }
            else
            {
                var wrapper = analysis.WrapperRange;
                prefixEnd = wrapper?.PrefixEndOffsetBytes ?? 0;
                suffixStart = wrapper?.SuffixStartOffsetBytes ?? fileLengthBytes;
            }
        }

        if (prefixEnd < 0)
            prefixEnd = 0;
        if (suffixStart <= 0 || suffixStart > fileLengthBytes)
            suffixStart = fileLengthBytes;
        if (suffixStart < prefixEnd)
        {
            prefixEnd = 0;
            suffixStart = fileLengthBytes;
        }

        return new TxtMarkupSplitBoundaries(
            TagName: selected.TagName,
            PrefixEndOffsetBytes: prefixEnd,
            SuffixStartOffsetBytes: suffixStart);
    }
}

internal readonly record struct TxtMarkupSplitBoundaries(
    string TagName,
    long PrefixEndOffsetBytes,
    long SuffixStartOffsetBytes);
