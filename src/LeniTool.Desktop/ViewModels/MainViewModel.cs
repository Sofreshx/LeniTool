using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    private bool _isConfigExpanded = true;
    private string _statusText = "Ready";
    private FileItemViewModel? _selectedFile;

    public MainViewModel()
    {
        _configService = new ConfigurationService();
        _configuration = new SplitConfiguration();

        Files = new ObservableCollection<FileItemViewModel>();
        LogEntries = new ObservableCollection<string>();

        LoadConfigCommand = ReactiveCommand.CreateFromTask(LoadConfigurationAsync);
        SaveConfigCommand = ReactiveCommand.CreateFromTask(SaveConfigurationAsync);
        ClearFilesCommand = ReactiveCommand.Create(() => Files.Clear());
        ProcessFilesCommand = ReactiveCommand.CreateFromTask(async () => await ProcessFilesAsync());
        ToggleConfigCommand = ReactiveCommand.Create(() => { IsConfigExpanded = !IsConfigExpanded; });

        var hasSelection = this.WhenAnyValue(x => x.SelectedFile).Select(f => f is not null);
        AnalyzeSelectedFileCommand = ReactiveCommand.CreateFromTask(AnalyzeSelectedFileAsync, hasSelection);
        SaveSelectedFileOverrideCommand = ReactiveCommand.CreateFromTask(SaveSelectedFileOverrideAsync, hasSelection);
        SaveSelectedExtensionOverrideCommand = ReactiveCommand.CreateFromTask(SaveSelectedExtensionOverrideAsync, hasSelection);

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

    public double MaxInputFileSizeGB
    {
        get => Configuration.MaxInputFileSize / 1024d;
        set
        {
            Configuration.MaxInputFileSize = Math.Max(0, value) * 1024d;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(MaxInputFileSizeGB));
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

    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public bool IsConfigExpanded
    {
        get => _isConfigExpanded;
        set => this.RaiseAndSetIfChanged(ref _isConfigExpanded, value);
    }

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

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> LoadConfigCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveConfigCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ProcessFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleConfigCommand { get; }

    public ReactiveCommand<Unit, Unit> AnalyzeSelectedFileCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSelectedFileOverrideCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSelectedExtensionOverrideCommand { get; }

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
            this.RaisePropertyChanged(nameof(MaxInputFileSizeGB));
            this.RaisePropertyChanged(nameof(AutoAnalyzeThresholdMB));
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
            var outputDir = string.IsNullOrWhiteSpace(OutputDirectory) || !Path.IsPathRooted(OutputDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LeniTool", "Output")
                : OutputDirectory;

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

            var filePaths = Files
                .Where(f => !f.IsRejected)
                .Select(f => f.FilePath)
                .ToList();

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
                    file.Status = result.Success ? $"Complete - {result.ChunkCount} chunks" : $"Error: {result.ErrorMessage}";
                    file.Progress = result.Success ? 100 : 0;
                }

                if (result.Success)
                {
                    AddLog($"✓ {Path.GetFileName(result.OriginalFilePath)}: {result.ChunkCount} chunks in {result.ProcessingTime.TotalSeconds:F2}s");
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

    private async Task AnalyzeSelectedFileAsync()
    {
        if (SelectedFile is null)
            return;

        await AnalyzeFileAsync(SelectedFile);
    }

    private async Task SaveSelectedFileOverrideAsync()
    {
        if (SelectedFile is null)
            return;

        var key = SafeGetFullPath(SelectedFile.FilePath);
        Configuration.FileOverrides ??= new Dictionary<string, SplitConfigurationOverrides>(StringComparer.OrdinalIgnoreCase);
        Configuration.FileOverrides[key] = SelectedFile.ToOverrides();

        await _configService.SaveConfigurationAsync(Configuration);
        AddLog($"Saved override for file: {SelectedFile.FileName}");
    }

    private async Task SaveSelectedExtensionOverrideAsync()
    {
        if (SelectedFile is null)
            return;

        var ext = (Path.GetExtension(SelectedFile.FilePath) ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext))
        {
            AddLog("Unable to determine file extension");
            return;
        }

        Configuration.ExtensionProfiles ??= new Dictionary<string, SplitConfigurationOverrides>(StringComparer.OrdinalIgnoreCase);
        Configuration.ExtensionProfiles[ext] = SelectedFile.ToOverrides();

        await _configService.SaveConfigurationAsync(Configuration);
        AddLog($"Saved override as default for {ext}");
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
    private double _overrideMaxChunkSizeMb = 4.5;
    private bool _overrideAutoDetectRecordTag = true;
    private string _overrideRecordTagName = string.Empty;

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

    public bool OverrideAutoDetectRecordTag
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

    public bool IsManualRecordTag => !OverrideAutoDetectRecordTag;

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

    public string StatusBadgeText
    {
        get
        {
            if (IsRejected)
                return "Rejected";
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
    }

    public SplitConfigurationOverrides ToOverrides()
    {
        return new SplitConfigurationOverrides
        {
            MaxChunkSizeMB = OverrideMaxChunkSizeMB,
            AutoDetectRecordTag = OverrideAutoDetectRecordTag,
            RecordTagName = OverrideAutoDetectRecordTag ? null : (OverrideRecordTagName ?? string.Empty).Trim()
        };
    }
}
