using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using LeniTool.Core.Models;
using LeniTool.Core.Services;
using Avalonia.Media;
using ReactiveUI;

namespace LeniTool.Desktop.ViewModels;

public class MainViewModel : ReactiveObject
{
    private readonly ConfigurationService _configService;
    private SplitConfiguration _configuration;
    private bool _isBusy;
    private bool _isSettingsOpen;
    private bool _isLogExpanded = false;
    private string _statusText = "Ready";
    private FileItemViewModel? _selectedFile;
    private string? _lastResolvedOutputDirectory;

    public MainViewModel()
    {
        _configService = new ConfigurationService();
        _configuration = new SplitConfiguration();

        Files = new ObservableCollection<FileItemViewModel>();
        LogEntries = new ObservableCollection<string>();

        var canRemoveTrackedItems = this.WhenAnyValue(x => x.IsBusy)
            .Select(isBusy => !isBusy);

        LoadConfigCommand = ReactiveCommand.CreateFromTask(LoadConfigurationAsync);
        SaveConfigCommand = ReactiveCommand.CreateFromTask(SaveConfigurationAsync);
        ClearFilesCommand = ReactiveCommand.Create(ClearFiles, canRemoveTrackedItems);
        ProcessFilesCommand = ReactiveCommand.CreateFromTask(async () => await ProcessFilesAsync());
        OpenSettingsCommand = ReactiveCommand.Create(() => { IsSettingsOpen = true; });
        CloseSettingsCommand = ReactiveCommand.Create(() => { IsSettingsOpen = false; });
        ToggleLogCommand = ReactiveCommand.Create(() => { IsLogExpanded = !IsLogExpanded; });

        OpenOutputFolderCommand = ReactiveCommand.Create(OpenOutputFolder);
        RemoveFileCommand = ReactiveCommand.Create<FileItemViewModel?>(RemoveFileFromQueue, canRemoveTrackedItems);
        RemoveProcessedOutputCommand = ReactiveCommand.Create<ProcessedOutputItemViewModel?>(RemoveProcessedOutput, canRemoveTrackedItems);
        RemoveProcessedOutputsForFileCommand = ReactiveCommand.Create<FileItemViewModel?>(RemoveProcessedOutputsForFile, canRemoveTrackedItems);

        var hasSelection = this.WhenAnyValue(x => x.SelectedFile).Select(f => f is not null);
        AnalyzeSelectedFileCommand = ReactiveCommand.CreateFromTask(AnalyzeSelectedFileAsync, hasSelection);

        _ = LoadConfigurationAsync();
    }

    #region Properties

    public SplitConfiguration Configuration
    {
        get => _configuration;
        set => this.RaiseAndSetIfChanged(ref _configuration, value);
    }

    public double MaxChunkSizeMB
    {
        get => Configuration.MaxChunkSizeMB;
        set
        {
            Configuration.MaxChunkSizeMB = value;
            this.RaisePropertyChanged();
        }
    }

    public string SegmentationTagsText
    {
        get => string.Join(Environment.NewLine, Configuration.SegmentationTags);
        set
        {
            Configuration.SegmentationTags = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            this.RaisePropertyChanged();
        }
    }

    public string ClosingTagsText
    {
        get => string.Join(Environment.NewLine, Configuration.ClosingTags);
        set
        {
            Configuration.ClosingTags = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            this.RaisePropertyChanged();
        }
    }

    public string OpeningTagsText
    {
        get => string.Join(Environment.NewLine, Configuration.OpeningTags);
        set
        {
            Configuration.OpeningTags = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            this.RaisePropertyChanged();
        }
    }

    public string NamingPattern
    {
        get => Configuration.NamingPattern;
        set
        {
            Configuration.NamingPattern = value;
            this.RaisePropertyChanged();
        }
    }

    public string OutputDirectory
    {
        get => Configuration.OutputDirectory;
        set
        {
            Configuration.OutputDirectory = value;
            this.RaisePropertyChanged();
        }
    }

    public int MaxParallelFiles
    {
        get => Configuration.MaxParallelFiles;
        set
        {
            Configuration.MaxParallelFiles = value;
            this.RaisePropertyChanged();
        }
    }

    public double MaxInputFileSizeMB
    {
        get => Configuration.MaxInputFileSize;
        set
        {
            Configuration.MaxInputFileSize = Math.Max(0, value);
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(MaxInputFileSizeMB));
        }
    }

    public double AutoAnalyzeThresholdMB
    {
        get => Configuration.AutoAnalyzeThreshold;
        set
        {
            Configuration.AutoAnalyzeThreshold = Math.Max(0, value);
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(AutoAnalyzeThresholdMB));
        }
    }

    public bool TxtDefaultsAutoDetectRecordTag
    {
        get => GetExtensionProfile(".txt")?.AutoDetectRecordTag ?? true;
        set
        {
            var profile = GetOrCreateExtensionProfile(".txt");
            profile.AutoDetectRecordTag = value;
            if (value)
                profile.RecordTagName = null;

            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(IsTxtDefaultsManualRecordTag));
            this.RaisePropertyChanged(nameof(TxtDefaultsRecordTagName));
        }
    }

    public string TxtDefaultsRecordTagName
    {
        get => GetExtensionProfile(".txt")?.RecordTagName ?? string.Empty;
        set
        {
            var profile = GetOrCreateExtensionProfile(".txt");
            var trimmed = (value ?? string.Empty).Trim();
            profile.RecordTagName = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
            this.RaisePropertyChanged();
        }
    }

    public bool IsTxtDefaultsManualRecordTag => !TxtDefaultsAutoDetectRecordTag;

    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => this.RaiseAndSetIfChanged(ref _isSettingsOpen, value);
    }

    public bool IsLogExpanded
    {
        get => _isLogExpanded;
        set
        {
            this.RaiseAndSetIfChanged(ref _isLogExpanded, value);
            this.RaisePropertyChanged(nameof(IsLogCollapsed));
        }
    }

    public bool IsLogCollapsed => !IsLogExpanded;

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public FileItemViewModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedFile, value);
            this.RaisePropertyChanged(nameof(HasSelectedFile));
            this.RaisePropertyChanged(nameof(HasNoSelectedFile));
        }
    }

    public bool HasSelectedFile => SelectedFile is not null;
    public bool HasNoSelectedFile => SelectedFile is null;

    public ObservableCollection<FileItemViewModel> Files { get; }
    public ObservableCollection<string> LogEntries { get; }

    public string? LastResolvedOutputDirectory
    {
        get => _lastResolvedOutputDirectory;
        private set => this.RaiseAndSetIfChanged(ref _lastResolvedOutputDirectory, value);
    }

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> LoadConfigCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveConfigCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ProcessFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleLogCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenOutputFolderCommand { get; }
    public ReactiveCommand<FileItemViewModel?, Unit> RemoveFileCommand { get; }
    public ReactiveCommand<ProcessedOutputItemViewModel?, Unit> RemoveProcessedOutputCommand { get; }
    public ReactiveCommand<FileItemViewModel?, Unit> RemoveProcessedOutputsForFileCommand { get; }

    public ReactiveCommand<Unit, Unit> AnalyzeSelectedFileCommand { get; }

    #endregion

    #region Methods

    private async Task LoadConfigurationAsync()
    {
        try
        {
            Configuration = await _configService.LoadConfigurationAsync();
            this.RaisePropertyChanged(nameof(MaxChunkSizeMB));
            this.RaisePropertyChanged(nameof(SegmentationTagsText));
            this.RaisePropertyChanged(nameof(ClosingTagsText));
            this.RaisePropertyChanged(nameof(OpeningTagsText));
            this.RaisePropertyChanged(nameof(NamingPattern));
            this.RaisePropertyChanged(nameof(OutputDirectory));
            this.RaisePropertyChanged(nameof(MaxParallelFiles));
            this.RaisePropertyChanged(nameof(MaxInputFileSizeMB));
            this.RaisePropertyChanged(nameof(AutoAnalyzeThresholdMB));
            this.RaisePropertyChanged(nameof(TxtDefaultsAutoDetectRecordTag));
            this.RaisePropertyChanged(nameof(TxtDefaultsRecordTagName));
            this.RaisePropertyChanged(nameof(IsTxtDefaultsManualRecordTag));
            AddLog("Configuration loaded successfully");

            // Refresh effective defaults for already-added files.
            foreach (var file in Files)
                file.ApplyDefaultsFrom(Configuration);
        }
        catch (Exception ex)
        {
            AddLog($"Error loading configuration: {ex.Message}");
        }
    }

    private async Task SaveConfigurationAsync()
    {
        try
        {
            if (!Configuration.IsValid(out var errorMessage))
            {
                AddLog($"Invalid configuration: {errorMessage}");
                return;
            }

            await _configService.SaveConfigurationAsync(Configuration);
            AddLog("Configuration saved successfully");
        }
        catch (Exception ex)
        {
            AddLog($"Error saving configuration: {ex.Message}");
        }
    }

    public void AddLogPublic(string message) => AddLog(message);

    private SplitConfigurationOverrides? GetExtensionProfile(string extensionKey)
    {
        if (Configuration.ExtensionProfiles is null)
            return null;

        if (Configuration.ExtensionProfiles.TryGetValue(extensionKey, out var profile))
            return profile;

        var alternateKey = extensionKey.TrimStart('.');
        return Configuration.ExtensionProfiles.TryGetValue(alternateKey, out var alternateProfile)
            ? alternateProfile
            : null;
    }

    private SplitConfigurationOverrides GetOrCreateExtensionProfile(string extensionKey)
    {
        Configuration.ExtensionProfiles ??= new Dictionary<string, SplitConfigurationOverrides>(StringComparer.OrdinalIgnoreCase);

        if (Configuration.ExtensionProfiles.TryGetValue(extensionKey, out var profile))
            return profile;

        var alternateKey = extensionKey.TrimStart('.');
        if (Configuration.ExtensionProfiles.TryGetValue(alternateKey, out var alternateProfile))
        {
            Configuration.ExtensionProfiles.Remove(alternateKey);
            Configuration.ExtensionProfiles[extensionKey] = alternateProfile;
            return alternateProfile;
        }

        var createdProfile = new SplitConfigurationOverrides();
        Configuration.ExtensionProfiles[extensionKey] = createdProfile;
        return createdProfile;
    }

    public async Task AddFilesFromPathsAsync(IEnumerable<string> filePaths)
    {
        var addedAny = false;
        var addedCount = 0;

        var maxBytes = Configuration.MaxInputFileSizeBytes;
        var thresholdBytes = Configuration.AutoAnalyzeThresholdBytes;

        foreach (var filePath in filePaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var fullPath = SafeGetFullPath(filePath);
            if (!File.Exists(fullPath))
            {
                AddLog($"File not found: {filePath}");
                continue;
            }

            if (Files.Any(f => string.Equals(SafeGetFullPath(f.FilePath), fullPath, StringComparison.OrdinalIgnoreCase)))
                continue;

            var fileInfo = new FileInfo(fullPath);

            if (maxBytes > 0 && fileInfo.Length > maxBytes)
            {
                var rejected = new FileItemViewModel
                {
                    FileName = fileInfo.Name,
                    FilePath = fullPath,
                    IsRejected = true,
                    Status = $"Rejected (exceeds max input size: {FormatBytes(maxBytes)})"
                };
                rejected.ApplyDefaultsFrom(Configuration);
                Files.Add(rejected);
                SelectedFile ??= rejected;
                addedAny = true;
                addedCount++;

                AddLog($"Rejected {fileInfo.Name} ({FormatBytes(fileInfo.Length)}): exceeds max input size {FormatBytes(maxBytes)}");
                continue;
            }

            var shouldAutoAnalyze = thresholdBytes > 0
                ? fileInfo.Length <= thresholdBytes
                : false;

            var vm = new FileItemViewModel
            {
                FileName = fileInfo.Name,
                FilePath = fullPath,
                Status = shouldAutoAnalyze
                    ? "Analyzing..."
                    : (thresholdBytes <= 0
                        ? "Added (auto-analyze disabled - click Analyze)"
                        : $"Added (large file - click Analyze; threshold: {FormatBytes(thresholdBytes)})")
            };
            vm.ApplyDefaultsFrom(Configuration);
            Files.Add(vm);
            SelectedFile ??= vm;
            addedAny = true;
            addedCount++;

            if (shouldAutoAnalyze)
                await AnalyzeFileAsync(vm);
        }

        if (addedAny)
            AddLog($"Added {addedCount} file(s)");
    }

    private void ClearFiles()
    {
        var filesWithTrackedOutputs = Files
            .Where(file => file.HasProcessedOutputs)
            .ToList();

        if (filesWithTrackedOutputs.Count > 0)
        {
            var trackedOutputCount = filesWithTrackedOutputs.Sum(file => file.ProcessedOutputFiles.Count);
            AddLog($"Cannot clear the file queue while {filesWithTrackedOutputs.Count} file(s) still have {trackedOutputCount} tracked generated output(s). Remove generated outputs first to preserve the rollback path.");
            return;
        }

        Files.Clear();
        SelectedFile = null;
        StatusText = "Ready";
        AddLog("Cleared file queue");
    }

    private void RemoveFileFromQueue(FileItemViewModel? file)
    {
        if (file is null)
            return;

        if (file.ProcessedOutputFiles.Count > 0)
        {
            AddLog($"Cannot remove {file.FileName} from the queue while {file.ProcessedOutputFiles.Count} generated output(s) are still tracked. Remove generated outputs first to preserve the rollback path.");
            return;
        }

        var removedIndex = Files.IndexOf(file);
        if (removedIndex < 0)
            return;

        var trackedOutputCount = file.ProcessedOutputFiles.Count;
        var wasSelected = ReferenceEquals(SelectedFile, file);

        Files.RemoveAt(removedIndex);

        if (wasSelected)
            SelectedFile = DetermineSelectionFallback(removedIndex);

        if (Files.Count == 0)
            StatusText = "Ready";

        var outputSuffix = trackedOutputCount > 0
            ? $" ({trackedOutputCount} generated output(s) still tracked on disk)"
            : string.Empty;
        AddLog($"Removed file from queue: {file.FileName}{outputSuffix}");
    }

    private void RemoveProcessedOutput(ProcessedOutputItemViewModel? outputItem)
    {
        if (outputItem is null)
            return;

        var file = FindFile(outputItem.SourceFilePath);
        if (file is null)
        {
            AddLog($"Unable to remove generated output; source file is no longer in the queue: {outputItem.DisplayName}");
            return;
        }

        var deletionOutcome = DeleteGeneratedOutputFile(outputItem);
        AddLog(deletionOutcome.Message);

        if (!deletionOutcome.CanRemoveReference)
            return;

        if (!file.RemoveProcessedOutput(outputItem.FullPath))
        {
            AddLog($"Generated output entry was already absent from {file.FileName}: {outputItem.DisplayName}");
            return;
        }

        if (!file.HasProcessedOutputs)
            AddLog($"Removed the last generated output for {file.FileName}; it can be processed again.");
    }

    private void RemoveProcessedOutputsForFile(FileItemViewModel? file)
    {
        if (file is null)
            return;

        var outputs = file.ProcessedOutputFiles.ToList();
        if (outputs.Count == 0)
        {
            AddLog($"No generated outputs are tracked for {file.FileName}");
            return;
        }

        var removedCount = 0;
        var failureCount = 0;

        foreach (var output in outputs)
        {
            var deletionOutcome = DeleteGeneratedOutputFile(output);
            AddLog(deletionOutcome.Message);

            if (!deletionOutcome.CanRemoveReference)
            {
                failureCount++;
                continue;
            }

            if (file.RemoveProcessedOutput(output.FullPath))
                removedCount++;
        }

        AddLog($"Generated output cleanup for {file.FileName}: removed {removedCount}, failed {failureCount}.");

        if (removedCount > 0 && !file.HasProcessedOutputs)
            AddLog($"Rollback complete for {file.FileName}; it can be processed again.");
    }

    private async Task ProcessFilesAsync()
    {
        if (!Files.Any())
        {
            AddLog("No files to process");
            return;
        }

        if (!Configuration.IsValid(out var errorMessage))
        {
            AddLog($"Invalid configuration: {errorMessage}");
            return;
        }

        IsBusy = true;
        StatusText = "Processing...";

        try
        {
            var outputDir = ResolveOutputDirectory(OutputDirectory, out var relativeBaseUsed);

            LastResolvedOutputDirectory = outputDir;

            AddLog($"Output directory setting: {(string.IsNullOrWhiteSpace(OutputDirectory) ? "(empty)" : OutputDirectory)}");
            AddLog($"Output directory (resolved): {outputDir}");
            if (!string.IsNullOrWhiteSpace(OutputDirectory) && !Path.IsPathRooted(OutputDirectory) && !string.IsNullOrWhiteSpace(relativeBaseUsed))
                AddLog($"(relative to: {relativeBaseUsed})");

            ApplyRunOverridesFromUi();

            var processingService = new FileProcessingService(Configuration);
            var progress = new Progress<ProcessingProgress>(p =>
            {
                var file = Files.FirstOrDefault(f => f.FileName == p.FileName);
                if (file != null)
                {
                    file.Status = p.Status;
                    file.Progress = p.PercentComplete;
                }
                StatusText = p.Status;
            });

            var alreadyProcessed = Files
                .Where(f => f.IsProcessed)
                .Select(f => f.FileName)
                .ToList();

            var filePaths = Files
                .Where(f => !f.IsRejected && !f.IsProcessed)
                .Select(f => f.FilePath)
                .ToList();

            if (alreadyProcessed.Count > 0)
                AddLog($"Skipped {alreadyProcessed.Count} already processed file(s) this session.");

            if (filePaths.Count == 0)
            {
                AddLog("No eligible files to process (all files rejected)");
                StatusText = "No eligible files";
                return;
            }
            var results = await processingService.ProcessFilesAsync(filePaths, outputDir, true, progress);

            foreach (var result in results)
            {
                var file = Files.FirstOrDefault(f => f.FilePath == result.OriginalFilePath);
                if (file != null)
                {
                    if (result.Success)
                    {
                        file.Progress = 100;
                        file.SetProcessedOutputs(result.OutputFiles);
                    }
                    else
                    {
                        file.Status = $"Error: {result.ErrorMessage}";
                        file.Progress = 0;
                    }
                }

                if (result.Success)
                {
                    var outputDirectories = result.OutputFiles
                        .Select(p => Path.GetDirectoryName(p) ?? string.Empty)
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var outputDirUsed = outputDirectories.Count == 1 ? outputDirectories[0] : outputDir;
                    AddLog($"✓ {Path.GetFileName(result.OriginalFilePath)}: {result.ChunkCount} chunks in {result.ProcessingTime.TotalSeconds:F2}s");
                    AddLog($"  Output folder: {outputDirUsed}");

                    if (result.OutputFiles.Count > 0)
                    {
                        var first = Path.GetFileName(result.OutputFiles[0]);
                        var last = Path.GetFileName(result.OutputFiles[^1]);
                        AddLog($"  Output names: {first}{(result.OutputFiles.Count > 1 ? $" ... {last}" : string.Empty)}");
                    }

                    if (outputDirectories.Count > 1)
                        AddLog($"  Note: outputs span multiple folders ({outputDirectories.Count}).");
                }
                else
                {
                    AddLog($"✗ {Path.GetFileName(result.OriginalFilePath)}: {result.ErrorMessage}");
                }
            }

            var successCount = results.Count(r => r.Success);
            StatusText = $"Complete: {successCount}/{results.Count} files processed successfully";
            AddLog($"Processing complete: {successCount}/{results.Count} successful");
        }
        catch (Exception ex)
        {
            AddLog($"Error processing files: {ex.Message}");
            StatusText = "Error occurred";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenOutputFolder()
    {
        try
        {
            var dir = !string.IsNullOrWhiteSpace(LastResolvedOutputDirectory)
                ? LastResolvedOutputDirectory
                : ResolveOutputDirectory(OutputDirectory, out _);

            Directory.CreateDirectory(dir);

            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });

            AddLog($"Opened output folder: {dir}");
        }
        catch (Exception ex)
        {
            AddLog($"Failed to open output folder: {ex.Message}");
        }
    }

    private static string ResolveOutputDirectory(string? outputDirectorySetting, out string? relativeBaseUsed)
    {
        relativeBaseUsed = null;
        if (string.IsNullOrWhiteSpace(outputDirectorySetting))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LeniTool",
                "Output");
        }

        if (Path.IsPathRooted(outputDirectorySetting))
            return Path.GetFullPath(outputDirectorySetting);

        // Relative output paths: try repo root (if present), then current directory, then executable.
        var bases = new List<string>();
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (!string.IsNullOrWhiteSpace(repoRoot))
            bases.Add(repoRoot);

        if (!string.IsNullOrWhiteSpace(Environment.CurrentDirectory))
            bases.Add(Environment.CurrentDirectory);

        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
            bases.Add(AppContext.BaseDirectory);

        foreach (var baseDir in bases.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.GetFullPath(Path.Combine(baseDir, outputDirectorySetting));
            if (Directory.Exists(candidate))
            {
                relativeBaseUsed = baseDir;
                return candidate;
            }
        }

        // Fallback to first base (repo root if found), otherwise executable directory.
        var fallbackBase = bases.FirstOrDefault() ?? AppContext.BaseDirectory;
        relativeBaseUsed = fallbackBase;
        return Path.GetFullPath(Path.Combine(fallbackBase, outputDirectorySetting));
    }

    private static string? FindRepoRoot(string startDir)
    {
        if (string.IsNullOrWhiteSpace(startDir))
            return null;

        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var slnPath = Path.Combine(dir.FullName, "LeniTool.sln");
            var gitPath = Path.Combine(dir.FullName, ".git");
            if (File.Exists(slnPath) || Directory.Exists(gitPath))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }

    private async Task AnalyzeSelectedFileAsync()
    {
        if (SelectedFile is null)
            return;

        await AnalyzeFileAsync(SelectedFile);
    }

    private FileItemViewModel? DetermineSelectionFallback(int removedIndex)
    {
        if (Files.Count == 0)
            return null;

        if (removedIndex >= 0 && removedIndex < Files.Count)
            return Files[removedIndex];

        return Files[Files.Count - 1];
    }

    private FileItemViewModel? FindFile(string sourceFilePath)
    {
        var normalizedPath = SafeGetFullPath(sourceFilePath);
        return Files.FirstOrDefault(file =>
            string.Equals(SafeGetFullPath(file.FilePath), normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    private static (bool CanRemoveReference, string Message) DeleteGeneratedOutputFile(ProcessedOutputItemViewModel outputItem)
    {
        try
        {
            if (!File.Exists(outputItem.FullPath))
                return (true, $"Generated output already missing: {outputItem.FullPath}");

            File.Delete(outputItem.FullPath);
            return (true, $"Deleted generated output: {outputItem.FullPath}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete generated output {outputItem.DisplayName}: {ex.Message}");
        }
    }



    private void ApplyRunOverridesFromUi()
    {
        Configuration.RunOverrides.FileOverrides = new Dictionary<string, SplitConfigurationOverrides>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Files)
        {
            var key = SafeGetFullPath(file.FilePath);
            Configuration.RunOverrides.FileOverrides[key] = file.ToOverrides();
        }
    }

    private async Task AnalyzeFileAsync(FileItemViewModel file)
    {
        if (file is null)
            return;

        try
        {
            file.Status = "Analyzing...";
            file.IsAnalyzing = true;

            var strategy = CreateStrategyFor(file.FilePath);
            if (strategy is null)
            {
                file.Status = "Unsupported file type";
                return;
            }

            var analysis = await strategy.AnalyzeAsync(file.FilePath, CancellationToken.None);
            file.Analysis = analysis;
            file.Status = analysis.CandidateRecords.Count > 0
                ? $"Analyzed ({analysis.CandidateRecords.Count} candidate(s))"
                : "Analyzed";
        }
        catch (Exception ex)
        {
            file.Status = $"Analysis error: {ex.Message}";
        }
        finally
        {
            file.IsAnalyzing = false;
        }
    }

    private ISplitterStrategy? CreateStrategyFor(string filePath)
    {
        var ext = (Path.GetExtension(filePath) ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".txt" => new TxtMarkupSplitterService(Configuration),
            ".html" => new HtmlSplitterService(Configuration),
            ".htm" => new HtmlSplitterService(Configuration),
            _ => null
        };
    }

    private static string SafeGetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogEntries.Insert(0, $"[{timestamp}] {message}");
        
        while (LogEntries.Count > 100)
        {
            LogEntries.RemoveAt(LogEntries.Count - 1);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0)
            return "-";

        const double KB = 1024d;
        const double MB = 1024d * 1024d;
        const double GB = 1024d * 1024d * 1024d;

        if (bytes >= (long)GB)
            return $"{bytes / GB:F2} GB";
        if (bytes >= (long)MB)
            return $"{bytes / MB:F2} MB";
        if (bytes >= (long)KB)
            return $"{bytes / KB:F2} KB";
        return $"{bytes:N0} bytes";
    }

    #endregion
}

public class FileItemViewModel : ReactiveObject
{
    private static readonly IBrush BadgeNeutralBgBrush = new SolidColorBrush(Color.Parse("#242A35"));
    private static readonly IBrush BadgeNeutralFgBrush = new SolidColorBrush(Color.Parse("#D8DEE9"));
    private static readonly IBrush BadgeSuccessBgBrush = new SolidColorBrush(Color.Parse("#173426"));
    private static readonly IBrush BadgeSuccessFgBrush = new SolidColorBrush(Color.Parse("#B7F3D0"));
    private static readonly IBrush BadgeWarningBgBrush = new SolidColorBrush(Color.Parse("#3A2D12"));
    private static readonly IBrush BadgeWarningFgBrush = new SolidColorBrush(Color.Parse("#FFD89A"));
    private static readonly IBrush BadgeDangerBgBrush = new SolidColorBrush(Color.Parse("#3A1820"));
    private static readonly IBrush BadgeDangerFgBrush = new SolidColorBrush(Color.Parse("#FFC0CB"));

    private string _status = string.Empty;
    private double _progress;
    private AnalysisResult? _analysis;
    private bool _isAnalyzing;
    private bool _isRejected;
    private bool _isProcessed;
    private double _overrideMaxChunkSizeMb = 4.5;
    private bool? _overrideAutoDetectRecordTag = true;
    private string _overrideRecordTagName = string.Empty;
    private string _overrideNamingPattern = string.Empty;
    private ObservableCollection<ProcessedOutputItemViewModel> _processedOutputFiles = new();

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    public bool IsRejected
    {
        get => _isRejected;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRejected, value);
            this.RaisePropertyChanged(nameof(StatusBadgeText));
            this.RaisePropertyChanged(nameof(StatusBadgeClasses));
            this.RaisePropertyChanged(nameof(StatusBadgeBackground));
            this.RaisePropertyChanged(nameof(StatusBadgeForeground));
        }
    }

    public bool IsProcessed
    {
        get => _isProcessed;
        set
        {
            this.RaiseAndSetIfChanged(ref _isProcessed, value);
            this.RaisePropertyChanged(nameof(HasProcessedOutputs));
            this.RaisePropertyChanged(nameof(StatusBadgeText));
            this.RaisePropertyChanged(nameof(StatusBadgeClasses));
            this.RaisePropertyChanged(nameof(StatusBadgeBackground));
            this.RaisePropertyChanged(nameof(StatusBadgeForeground));
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            this.RaiseAndSetIfChanged(ref _status, value);
            this.RaisePropertyChanged(nameof(StatusBadgeText));
            this.RaisePropertyChanged(nameof(StatusBadgeClasses));
            this.RaisePropertyChanged(nameof(StatusBadgeBackground));
            this.RaisePropertyChanged(nameof(StatusBadgeForeground));
        }
    }

    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set
        {
            this.RaiseAndSetIfChanged(ref _isAnalyzing, value);
            this.RaisePropertyChanged(nameof(StatusBadgeText));
            this.RaisePropertyChanged(nameof(StatusBadgeClasses));
            this.RaisePropertyChanged(nameof(StatusBadgeBackground));
            this.RaisePropertyChanged(nameof(StatusBadgeForeground));
        }
    }

    public AnalysisResult? Analysis
    {
        get => _analysis;
        set
        {
            this.RaiseAndSetIfChanged(ref _analysis, value);
            this.RaisePropertyChanged(nameof(IsAnalyzed));
            this.RaisePropertyChanged(nameof(FileSizeBytes));
            this.RaisePropertyChanged(nameof(FileSizeDisplay));
            this.RaisePropertyChanged(nameof(FileSizeShortDisplay));
            this.RaisePropertyChanged(nameof(EncodingDisplay));
            this.RaisePropertyChanged(nameof(CandidateRecords));
            this.RaisePropertyChanged(nameof(TagSummaries));
            this.RaisePropertyChanged(nameof(HasTagSummaries));
            this.RaisePropertyChanged(nameof(AnalysisIssues));
            this.RaisePropertyChanged(nameof(HasAnalysisIssues));
            this.RaisePropertyChanged(nameof(AnalysisStatusText));
            this.RaisePropertyChanged(nameof(EstimatedPartCount));
            this.RaisePropertyChanged(nameof(TopCandidateDisplay));
            this.RaisePropertyChanged(nameof(StatusBadgeText));
            this.RaisePropertyChanged(nameof(StatusBadgeClasses));
            this.RaisePropertyChanged(nameof(StatusBadgeBackground));
            this.RaisePropertyChanged(nameof(StatusBadgeForeground));
        }
    }

    public bool IsAnalyzed => Analysis is not null;

    public IReadOnlyList<CandidateRecord> CandidateRecords => (IReadOnlyList<CandidateRecord>?)Analysis?.CandidateRecords ?? Array.Empty<CandidateRecord>();

    public IReadOnlyList<TagSummary> TagSummaries => (IReadOnlyList<TagSummary>?)Analysis?.TagSummaries ?? Array.Empty<TagSummary>();

    public bool HasTagSummaries => TagSummaries.Count > 0;

    public IReadOnlyList<string> AnalysisIssues => BuildAnalysisIssues();

    public bool HasAnalysisIssues => AnalysisIssues.Count > 0;

    public string AnalysisStatusText
    {
        get
        {
            if (Analysis is null)
                return "Not analyzed";

            return HasAnalysisIssues ? "Issues detected" : "OK";
        }
    }

    public long FileSizeBytes
    {
        get
        {
            if (Analysis is not null && Analysis.FileSizeBytes > 0)
                return Analysis.FileSizeBytes;

            try
            {
                return new FileInfo(FilePath).Length;
            }
            catch
            {
                return 0;
            }
        }
    }

    public string FileSizeDisplay
    {
        get
        {
            var bytes = FileSizeBytes;
            if (bytes <= 0)
                return "-";

            var mb = bytes / (1024d * 1024d);
            return mb >= 1
                ? $"{mb:F2} MB ({bytes:N0} bytes)"
                : $"{bytes:N0} bytes";
        }
    }

    public string FileSizeShortDisplay
    {
        get
        {
            var bytes = FileSizeBytes;
            if (bytes <= 0)
                return "-";

            const double KB = 1024d;
            const double MB = 1024d * 1024d;
            const double GB = 1024d * 1024d * 1024d;

            if (bytes >= (long)GB)
                return $"{bytes / GB:F2} GB";
            if (bytes >= (long)MB)
                return $"{bytes / MB:F2} MB";
            if (bytes >= (long)KB)
                return $"{bytes / KB:F2} KB";
            return $"{bytes:N0} B";
        }
    }

    public string EncodingDisplay
    {
        get
        {
            if (Analysis is null)
                return "-";
            if (string.IsNullOrWhiteSpace(Analysis.EncodingName))
                return "-";
            return Analysis.HasBom
                ? $"{Analysis.EncodingName} (BOM)"
                : Analysis.EncodingName;
        }
    }

    public double OverrideMaxChunkSizeMB
    {
        get => _overrideMaxChunkSizeMb;
        set
        {
            this.RaiseAndSetIfChanged(ref _overrideMaxChunkSizeMb, value);
            this.RaisePropertyChanged(nameof(EstimatedPartCount));
        }
    }

    public bool? OverrideAutoDetectRecordTag
    {
        get => _overrideAutoDetectRecordTag;
        set
        {
            this.RaiseAndSetIfChanged(ref _overrideAutoDetectRecordTag, value);
            this.RaisePropertyChanged(nameof(IsManualRecordTag));
        }
    }

    public string OverrideRecordTagName
    {
        get => _overrideRecordTagName;
        set => this.RaiseAndSetIfChanged(ref _overrideRecordTagName, value);
    }

    public string OverrideNamingPattern
    {
        get => _overrideNamingPattern;
        set => this.RaiseAndSetIfChanged(ref _overrideNamingPattern, value);
    }

    public bool IsManualRecordTag => !(OverrideAutoDetectRecordTag ?? true);

    public ObservableCollection<ProcessedOutputItemViewModel> ProcessedOutputFiles
    {
        get => _processedOutputFiles;
        private set
        {
            this.RaiseAndSetIfChanged(ref _processedOutputFiles, value);
            this.RaisePropertyChanged(nameof(HasProcessedOutputs));
        }
    }

    public bool HasProcessedOutputs => ProcessedOutputFiles.Count > 0;

    public int EstimatedPartCount
    {
        get
        {
            var bytes = FileSizeBytes;
            if (bytes <= 0)
                return 0;

            var targetBytes = (long)(OverrideMaxChunkSizeMB * 1024d * 1024d);
            if (targetBytes <= 0)
                return 0;

            return (int)Math.Max(1, Math.Ceiling(bytes / (double)targetBytes));
        }
    }

    public string TopCandidateDisplay
    {
        get
        {
            var top = CandidateRecords
                .OrderByDescending(c => c.Confidence)
                .FirstOrDefault();

            if (top is null)
                return "-";

            if (string.IsNullOrWhiteSpace(top.TagName))
                return "-";

            var pct = Math.Clamp(top.Confidence, 0d, 1d);
            return $"{top.TagName} ({pct:P0})";
        }
    }

    private IReadOnlyList<string> BuildAnalysisIssues()
    {
        if (Analysis is null)
            return Array.Empty<string>();

        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(Analysis.EncodingName))
            issues.Add("Encoding could not be detected.");

        if (TagSummaries.Count == 0)
            issues.Add("No tags detected. File may not be markup or is malformed.");

        if (CandidateRecords.Count == 0)
            issues.Add("No repeating record tag detected. Auto-split may not work.");

        if (CandidateRecords.Count > 0 && Analysis.WrapperRange is null)
            issues.Add("Could not determine wrapper boundaries for records.");

        if (CandidateRecords.Count > 0 && Analysis.Confidence < 0.4)
            issues.Add($"Low confidence record tag detection ({Analysis.Confidence:P0}).");

        var top = CandidateRecords
            .OrderByDescending(c => c.Confidence)
            .FirstOrDefault();

        if (top is not null)
        {
            var summary = TagSummaries.FirstOrDefault(s =>
                string.Equals(s.TagName, top.TagName, StringComparison.OrdinalIgnoreCase));

            if (summary is not null && summary.OpenCount != summary.CloseCount)
            {
                issues.Add($"Tag <{summary.TagName}> has mismatched open/close counts (open: {summary.OpenCount}, close: {summary.CloseCount}).");
            }
        }

        return issues;
    }

    public string StatusBadgeText
    {
        get
        {
            if (IsRejected)
                return "Rejected";
            if (IsProcessed)
                return "Processed";
            if (IsAnalyzing)
                return "Analyzing";
            if (Analysis is not null && CandidateRecords.Count > 0)
                return "Analyzed";
            if (Analysis is not null)
                return "Analyzed";
            if (!string.IsNullOrWhiteSpace(Status))
                return "Pending";
            return "Pending";
        }
    }

    public string StatusBadgeClasses
    {
        get
        {
            if (IsRejected)
                return "badge danger";
            if (IsProcessed)
                return "badge success";
            if (IsAnalyzing)
                return "badge warning";
            if (Analysis is not null)
                return "badge success";
            return "badge";
        }
    }

    public IBrush StatusBadgeBackground
    {
        get
        {
            if (IsRejected)
                return BadgeDangerBgBrush;
            if (IsProcessed)
                return BadgeSuccessBgBrush;
            if (IsAnalyzing)
                return BadgeWarningBgBrush;
            if (IsAnalyzed)
                return BadgeSuccessBgBrush;
            return BadgeNeutralBgBrush;
        }
    }

    public IBrush StatusBadgeForeground
    {
        get
        {
            if (IsRejected)
                return BadgeDangerFgBrush;
            if (IsProcessed)
                return BadgeSuccessFgBrush;
            if (IsAnalyzing)
                return BadgeWarningFgBrush;
            if (IsAnalyzed)
                return BadgeSuccessFgBrush;
            return BadgeNeutralFgBrush;
        }
    }

    public void ApplyDefaultsFrom(SplitConfiguration configuration)
    {
        var resolved = configuration.ResolveForFile(FilePath);
        OverrideMaxChunkSizeMB = resolved.MaxChunkSizeMB;
        OverrideAutoDetectRecordTag = resolved.AutoDetectRecordTag;
        OverrideRecordTagName = resolved.RecordTagName ?? string.Empty;
        // Leave OverrideNamingPattern empty by default; empty means "use global/effective".
    }

    public SplitConfigurationOverrides ToOverrides()
    {
        var autoDetectRecordTag = OverrideAutoDetectRecordTag ?? true;
        return new SplitConfigurationOverrides
        {
            MaxChunkSizeMB = OverrideMaxChunkSizeMB,
            NamingPattern = string.IsNullOrWhiteSpace(OverrideNamingPattern) ? null : OverrideNamingPattern.Trim(),
            AutoDetectRecordTag = autoDetectRecordTag,
            RecordTagName = autoDetectRecordTag ? null : (OverrideRecordTagName ?? string.Empty).Trim()
        };
    }

    public void SetProcessedOutputs(IEnumerable<string> outputFiles)
    {
        ProcessedOutputFiles.Clear();

        var outputCount = 0;
        foreach (var file in outputFiles)
        {
            if (!string.IsNullOrWhiteSpace(file))
            {
                ProcessedOutputFiles.Add(new ProcessedOutputItemViewModel(FilePath, file));
                outputCount++;
            }
        }

        IsProcessed = true;
        Status = outputCount switch
        {
            0 => "Processed",
            1 => "Processed (1 output)",
            _ => $"Processed ({outputCount} outputs)"
        };

        this.RaisePropertyChanged(nameof(HasProcessedOutputs));
    }

    public bool RemoveProcessedOutput(string outputPath)
    {
        var normalizedPath = NormalizePath(outputPath);
        var outputItem = ProcessedOutputFiles.FirstOrDefault(item =>
            string.Equals(NormalizePath(item.FullPath), normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (outputItem is null)
            return false;

        var removed = ProcessedOutputFiles.Remove(outputItem);
        if (!removed)
            return false;

        this.RaisePropertyChanged(nameof(HasProcessedOutputs));

        if (ProcessedOutputFiles.Count == 0)
        {
            IsProcessed = false;
            Status = "Ready to process";
        }
        else
        {
            Status = ProcessedOutputFiles.Count == 1
                ? "Processed (1 output)"
                : $"Processed ({ProcessedOutputFiles.Count} outputs)";
        }

        return true;
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}

public sealed class ProcessedOutputItemViewModel
{
    public ProcessedOutputItemViewModel(string sourceFilePath, string fullPath)
    {
        SourceFilePath = NormalizePath(sourceFilePath);
        FullPath = NormalizePath(fullPath);
    }

    public string SourceFilePath { get; }
    public string FullPath { get; }
    public string DisplayName => Path.GetFileName(FullPath);
    public string DirectoryPath => Path.GetDirectoryName(FullPath) ?? string.Empty;

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}
