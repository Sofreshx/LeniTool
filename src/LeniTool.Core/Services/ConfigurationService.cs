using System.Text.Json;
using System.Text.Json.Serialization;
using LeniTool.Core.Models;

namespace LeniTool.Core.Services;

/// <summary>
/// Handles loading and saving configuration
/// </summary>
public class ConfigurationService
{
    private readonly string _configFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ConfigurationService(string? configDirectory = null)
    {
        var directory = configDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        _configFilePath = Path.Combine(directory, "config.json");
    }

    /// <summary>
    /// Loads configuration from file, or creates default if it doesn't exist
    /// </summary>
    public async Task<SplitConfiguration> LoadConfigurationAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                var defaultConfig = new SplitConfiguration();
                await SaveConfigurationAsync(defaultConfig);
                return defaultConfig;
            }

            var json = await File.ReadAllTextAsync(_configFilePath);
            var config = JsonSerializer.Deserialize<SplitConfiguration>(json, JsonOptions);
            return config ?? new SplitConfiguration();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            return new SplitConfiguration();
        }
    }

    /// <summary>
    /// Saves configuration to file
    /// </summary>
    public async Task SaveConfigurationAsync(SplitConfiguration configuration)
    {
        try
        {
            var json = JsonSerializer.Serialize(configuration, JsonOptions);
            await File.WriteAllTextAsync(_configFilePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the path to the configuration file
    /// </summary>
    public string GetConfigFilePath() => _configFilePath;
}
