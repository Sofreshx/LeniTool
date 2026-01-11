using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using LeniTool.Desktop.ViewModels;

namespace LeniTool.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var list = this.FindControl<ListBox>("FilesList");
        if (list is not null)
        {
            list.AddHandler(DragDrop.DragOverEvent, OnFilesDragOver);
            list.AddHandler(DragDrop.DropEvent, OnFilesDrop);
        }
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

    private void OnFilesDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;

        e.Handled = true;
    }

    private async void OnFilesDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (DataContext is not MainViewModel vm)
                return;

            var files = e.Data.GetFiles();
            if (files is null)
                return;

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
                vm.AddLogPublic($"Drop failed: {ex.Message}");
        }
    }
}
