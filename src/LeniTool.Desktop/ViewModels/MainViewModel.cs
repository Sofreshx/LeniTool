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

    private const long AutoAnalyzeMaxBytes = 15L * 1024L * 1024L;

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
            var vm = new FileItemViewModel
            {
                FileName = fileInfo.Name,
                FilePath = fullPath,
                Status = fileInfo.Length <= AutoAnalyzeMaxBytes ? "Analyzing..." : "Added (large file - click Re-analyze)"
            };
            vm.ApplyDefaultsFrom(Configuration);
            Files.Add(vm);
            SelectedFile ??= vm;
            addedAny = true;

            if (fileInfo.Length <= AutoAnalyzeMaxBytes)
                await AnalyzeFileAsync(vm);
        }

        if (addedAny)
            AddLog($"Added {Files.Count} file(s)");
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

            var filePaths = Files.Select(f => f.FilePath).ToList();
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

    #endregion
}

public class FileItemViewModel : ReactiveObject
{
    private string _status = string.Empty;
    private double _progress;
    private AnalysisResult? _analysis;
    private bool _isAnalyzing;
    private double _overrideMaxChunkSizeMb = 4.5;
    private bool _overrideAutoDetectRecordTag = true;
    private string _overrideRecordTagName = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set => this.RaiseAndSetIfChanged(ref _isAnalyzing, value);
    }

    public AnalysisResult? Analysis
    {
        get => _analysis;
        set
        {
            this.RaiseAndSetIfChanged(ref _analysis, value);
            this.RaisePropertyChanged(nameof(FileSizeBytes));
            this.RaisePropertyChanged(nameof(FileSizeDisplay));
            this.RaisePropertyChanged(nameof(EncodingDisplay));
            this.RaisePropertyChanged(nameof(CandidateRecords));
            this.RaisePropertyChanged(nameof(EstimatedPartCount));
        }
    }

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
