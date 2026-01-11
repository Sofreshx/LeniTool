namespace LeniTool.Core.Services;

public sealed class SplitterStrategyRegistry
{
    private readonly Dictionary<string, ISplitterStrategy> _strategiesByExtension = new(StringComparer.OrdinalIgnoreCase);

    public SplitterStrategyRegistry(IEnumerable<ISplitterStrategy>? strategies = null)
    {
        if (strategies is null)
            return;

        foreach (var strategy in strategies)
        {
            Register(strategy);
        }
    }

    public IReadOnlyCollection<string> SupportedExtensions => _strategiesByExtension.Keys.ToArray();

    public void Register(ISplitterStrategy strategy)
    {
        if (strategy is null)
            throw new ArgumentNullException(nameof(strategy));

        foreach (var ext in strategy.SupportedExtensions)
        {
            var normalized = NormalizeExtension(ext);
            _strategiesByExtension[normalized] = strategy;
        }
    }

    public bool SupportsExtension(string extension) => _strategiesByExtension.ContainsKey(NormalizeExtension(extension));

    public bool TryGetByFilePath(string filePath, out ISplitterStrategy? strategy)
    {
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(ext))
        {
            strategy = null;
            return false;
        }

        return _strategiesByExtension.TryGetValue(NormalizeExtension(ext), out strategy);
    }

    public ISplitterStrategy GetRequiredByFilePath(string filePath)
    {
        if (!TryGetByFilePath(filePath, out var strategy) || strategy is null)
            throw new NotSupportedException($"Unsupported file type: '{Path.GetExtension(filePath)}'");

        return strategy;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        var ext = extension.Trim();
        if (!ext.StartsWith('.'))
            ext = "." + ext;

        return ext.ToLowerInvariant();
    }
}
