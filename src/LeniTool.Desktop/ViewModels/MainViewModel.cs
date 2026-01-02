using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
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

    public MainViewModel()
    {
        _configService = new ConfigurationService();
        _configuration = new SplitConfiguration();

        Files = new ObservableCollection<FileItemViewModel>();
        LogEntries = new ObservableCollection<string>();

        LoadConfigCommand = ReactiveCommand.CreateFromTask(LoadConfigurationAsync);
        SaveConfigCommand = ReactiveCommand.CreateFromTask(SaveConfigurationAsync);
        AddFilesCommand = ReactiveCommand.CreateFromTask(AddFilesAsync);
        ClearFilesCommand = ReactiveCommand.Create(() => Files.Clear());
        ProcessFilesCommand = ReactiveCommand.CreateFromTask(async () => await ProcessFilesAsync());
        ToggleConfigCommand = ReactiveCommand.Create(() => { IsConfigExpanded = !IsConfigExpanded; });

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

    public ObservableCollection<FileItemViewModel> Files { get; }
    public ObservableCollection<string> LogEntries { get; }

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> LoadConfigCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveConfigCommand { get; }
    public ReactiveCommand<Unit, Unit> AddFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ProcessFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleConfigCommand { get; }

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

    private async Task AddFilesAsync()
    {
        // This will be implemented with Avalonia file dialog
        AddLog("Add files functionality - coming soon");
        await Task.CompletedTask;
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
}
