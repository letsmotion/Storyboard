using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Storyboard.ViewModels;

namespace Storyboard.Views;

public partial class ExportDialog : Window
{
    public ExportDialog()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnExportCapCutClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (!viewModel.CanExportVideo)
            return;

        var storageProvider = StorageProvider;
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择剪映草稿导出位置",
            AllowMultiple = false
        });

        if (folders.Count == 0)
            return;

        var selectedFolder = folders[0].Path.LocalPath;
        await viewModel.ExportToCapCutCommand.ExecuteAsync(selectedFolder);
        Close();
    }

    private async void OnExportVideoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (!viewModel.CanExportVideo)
            return;

        var storageProvider = StorageProvider;
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出视频",
            SuggestedFileName = $"{viewModel.ProjectName}_成片.mp4",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("MP4 文件") { Patterns = new[] { "*.mp4" } }
            }
        });

        if (file == null)
            return;

        await viewModel.ExportVideoAsync(file.Path.LocalPath);
        Close();
    }
}
