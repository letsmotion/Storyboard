using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Linq;

namespace Storyboard.Views;

public partial class DataMigrationDialog : Window
{
    public DataMigrationDialog()
    {
        InitializeComponent();
    }

    private async void BrowseDataDirectory_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择数据库存储位置",
            AllowMultiple = false
        });

        if (folders.Count > 0 && DataContext is ViewModels.SettingsViewModel vm)
        {
            vm.NewDataDirectory = folders[0].Path.LocalPath;
        }
    }

    private async void BrowseOutputDirectory_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择输出文件存储位置",
            AllowMultiple = false
        });

        if (folders.Count > 0 && DataContext is ViewModels.SettingsViewModel vm)
        {
            vm.NewOutputDirectory = folders[0].Path.LocalPath;
        }
    }
}
