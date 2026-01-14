using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LeniTool.Desktop.ViewModels;

namespace LeniTool.Desktop.Views;

public partial class MainWindow : Window
{
    private Border? _dropOverlay;
    private Border? _dropOverlayBox;
    private TextBlock? _dropOverlayTitle;
    private TextBlock? _dropOverlaySubtitle;
    private int _dragDepth;

    public MainWindow()
    {
        InitializeComponent();

        _dropOverlay = this.FindControl<Border>("DropOverlay");
        _dropOverlayBox = this.FindControl<Border>("DropOverlayBox");
        _dropOverlayTitle = this.FindControl<TextBlock>("DropOverlayTitle");
        _dropOverlaySubtitle = this.FindControl<TextBlock>("DropOverlaySubtitle");

        AddHandler(DragDrop.DragEnterEvent, OnWindowDragEnter, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragLeaveEvent, OnWindowDragLeave, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DropEvent, OnWindowDrop, RoutingStrategies.Tunnel);
    }

    private async void OnAddFilesClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainViewModel vm)
                return;

            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider is null)
            {
                vm.AddLogPublic("File picker unavailable (no StorageProvider)");
                return;
            }

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                Title = "Select files",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Supported") { Patterns = new[] { "*.html", "*.htm", "*.txt" } },
                    new FilePickerFileType("All") { Patterns = new[] { "*" } }
                }
            });

            var paths = files
                .Select(f => f.TryGetLocalPath())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Cast<string>()
                .ToList();

            await vm.AddFilesFromPathsAsync(paths);
        }
        catch (Exception ex)
        {
            if (DataContext is MainViewModel vm)
                vm.AddLogPublic($"Add files failed: {ex.Message}");
        }
    }

    private async void OnBrowseOutputDirectoryClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not MainViewModel vm)
                return;

            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider is null)
            {
                vm.AddLogPublic("Folder picker unavailable (no StorageProvider)");
                return;
            }

            IStorageFolder? startFolder = null;
            if (!string.IsNullOrWhiteSpace(vm.OutputDirectory))
            {
                try
                {
                    var currentPath = Path.GetFullPath(vm.OutputDirectory);
                    if (Directory.Exists(currentPath))
                        startFolder = await storageProvider.TryGetFolderFromPathAsync(currentPath);
                }
                catch
                {
                    // Ignore invalid paths; we'll just let the picker decide the start location.
                }
            }

            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select output directory",
                AllowMultiple = false,
                SuggestedStartLocation = startFolder
            });

            var selectedFolder = folders.FirstOrDefault();
            var selectedPath = selectedFolder?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(selectedPath))
                return;

            vm.OutputDirectory = selectedPath;
        }
        catch (Exception ex)
        {
            if (DataContext is MainViewModel vm)
                vm.AddLogPublic($"Browse output directory failed: {ex.Message}");
        }
    }

    private void OnWindowDragEnter(object? sender, DragEventArgs e)
    {
        _dragDepth++;
        UpdateOverlayAndEffects(e, showOverlay: true);
        e.Handled = true;
    }

    private void OnWindowDragLeave(object? sender, DragEventArgs e)
    {
        _dragDepth = Math.Max(0, _dragDepth - 1);
        if (_dragDepth == 0)
            SetOverlayVisible(false);

        e.Handled = true;
    }

    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        UpdateOverlayAndEffects(e, showOverlay: true);
        e.Handled = true;
    }

    private async void OnWindowDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (DataContext is not MainViewModel vm)
                return;

            _dragDepth = 0;
            SetOverlayVisible(false);

            var allPaths = ExtractFilePaths(e);
            if (allPaths.Count == 0)
                return;

            var supportedPaths = FilterSupportedExistingFiles(allPaths, out var skippedUnsupportedCount, out var skippedNonExistingCount);
            if (skippedNonExistingCount > 0)
                vm.AddLogPublic($"Skipped {skippedNonExistingCount} item(s): not local files");
            if (skippedUnsupportedCount > 0)
                vm.AddLogPublic($"Skipped {skippedUnsupportedCount} file(s): unsupported extension (supported: .txt, .html, .htm)");

            if (supportedPaths.Count == 0)
                return;

            await vm.AddFilesFromPathsAsync(supportedPaths);
        }
        catch (Exception ex)
        {
            if (DataContext is MainViewModel vm)
                vm.AddLogPublic($"Drop failed: {ex.Message}");
        }
        finally
        {
            SetOverlayVisible(false);
        }
    }

    private void UpdateOverlayAndEffects(DragEventArgs e, bool showOverlay)
    {
        var allPaths = ExtractFilePaths(e);

        // Reject early if drag data doesn't look like file paths.
        if (allPaths.Count == 0)
        {
            e.DragEffects = DragDropEffects.None;
            if (showOverlay)
                SetOverlayState(isAllowed: false, message: "Drop files to add", detail: "Unsupported drag content (no files)");
            return;
        }

        var supportedPaths = FilterSupportedExistingFiles(allPaths, out var skippedUnsupportedCount, out var skippedNonExistingCount);
        if (supportedPaths.Count == 0)
        {
            e.DragEffects = DragDropEffects.None;
            if (showOverlay)
            {
                var detail = skippedNonExistingCount > 0
                    ? "Items must be local files"
                    : "Supported: .txt, .html, .htm";
                SetOverlayState(isAllowed: false, message: "Unsupported file type", detail: detail);
            }
            return;
        }

        // By default, allow drop if we have at least one supported file.
        e.DragEffects = DragDropEffects.Copy;

        var maxBytes = (DataContext as MainViewModel)?.Configuration?.MaxInputFileSizeBytes ?? 0;
        if (maxBytes > 0)
        {
            var tooLargeCount = CountTooLargeFiles(supportedPaths, maxBytes);
            if (showOverlay)
            {
                if (tooLargeCount > 0)
                {
                    SetOverlayState(
                        isAllowed: false,
                        message: "Some file(s) will be rejected",
                        detail: $"Max input size: {FormatBytes(maxBytes)}");
                }
                else
                {
                    var detail = skippedUnsupportedCount > 0
                        ? $"{skippedUnsupportedCount} unsupported file(s) will be ignored"
                        : "Supported: .txt, .html, .htm";
                    SetOverlayState(isAllowed: true, message: "Drop files to add", detail: detail);
                }
            }
        }
        else
        {
            if (showOverlay)
            {
                var detail = skippedUnsupportedCount > 0
                    ? $"{skippedUnsupportedCount} unsupported file(s) will be ignored"
                    : "Supported: .txt, .html, .htm";
                SetOverlayState(isAllowed: true, message: "Drop files to add", detail: detail);
            }
        }
    }

    private void SetOverlayVisible(bool isVisible)
    {
        if (_dropOverlay is not null)
            _dropOverlay.IsVisible = isVisible;
    }

    private void SetOverlayState(bool isAllowed, string message, string detail)
    {
        SetOverlayVisible(true);

        if (_dropOverlayTitle is not null)
            _dropOverlayTitle.Text = message;
        if (_dropOverlaySubtitle is not null)
            _dropOverlaySubtitle.Text = detail;

        if (_dropOverlayBox is not null)
        {
            _dropOverlayBox.Classes.Remove("accept");
            _dropOverlayBox.Classes.Remove("reject");
            _dropOverlayBox.Classes.Add(isAllowed ? "accept" : "reject");
        }
    }

    private static IReadOnlyList<string> ExtractFilePaths(DragEventArgs e)
    {
        var paths = new List<string>();

        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files is not null)
            {
                foreach (var file in files)
                {
                    var path = file.TryGetLocalPath();
                    if (!string.IsNullOrWhiteSpace(path))
                        paths.Add(path);
                }
            }
        }

        if (e.Data.Contains(DataFormats.FileNames))
        {
            var obj = e.Data.Get(DataFormats.FileNames);
            switch (obj)
            {
                case IEnumerable<string> names:
                    paths.AddRange(names.Where(n => !string.IsNullOrWhiteSpace(n)));
                    break;
                case string single when !string.IsNullOrWhiteSpace(single):
                    paths.Add(single);
                    break;
            }
        }

        return paths
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> FilterSupportedExistingFiles(
        IReadOnlyList<string> allPaths,
        out int skippedUnsupportedCount,
        out int skippedNonExistingCount)
    {
        skippedUnsupportedCount = 0;
        skippedNonExistingCount = 0;

        var supported = new List<string>();

        foreach (var path in allPaths)
        {
            if (!File.Exists(path))
            {
                skippedNonExistingCount++;
                continue;
            }

            var ext = Path.GetExtension(path) ?? string.Empty;
            if (!string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(ext, ".html", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(ext, ".htm", StringComparison.OrdinalIgnoreCase))
            {
                skippedUnsupportedCount++;
                continue;
            }

            supported.Add(path);
        }

        return supported;
    }

    private static int CountTooLargeFiles(IReadOnlyList<string> paths, long maxBytes)
    {
        var count = 0;
        foreach (var path in paths)
        {
            try
            {
                if (new FileInfo(path).Length > maxBytes)
                    count++;
            }
            catch
            {
                // If we can't stat the file, don't block dropping.
            }
        }

        return count;
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
}
