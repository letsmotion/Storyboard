using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.Views;

public partial class StorageLocationDialog : Window
{
    public bool UseCustomLocation { get; private set; }
    public string? CustomDataPath { get; private set; }
    public string? CustomOutputPath { get; private set; }

    public StorageLocationDialog()
    {
        InitializeComponent();
        InitializeDefaultPath();
    }

    private void InitializeDefaultPath()
    {
        var defaultPath = Path.Combine(AppContext.BaseDirectory, "Data");
        DefaultLocationPath.Text = $"路径: {defaultPath}";
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择数据存储位置",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var selectedPath = folders[0].Path.LocalPath;
                CustomLocationTextBox.Text = selectedPath;

                // 自动选中自定义位置选项
                CustomLocationRadio.IsChecked = true;
            }
        }
        catch (Exception ex)
        {
            // 简单的错误处理
            await ShowErrorDialog($"选择文件夹时出错: {ex.Message}");
        }
    }

    private async void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (CustomLocationRadio.IsChecked == true)
            {
                // 使用自定义位置
                var customPath = CustomLocationTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(customPath))
                {
                    await ShowErrorDialog("请选择自定义存储位置");
                    return;
                }

                if (!Directory.Exists(customPath))
                {
                    await ShowErrorDialog("选择的文件夹不存在");
                    return;
                }

                UseCustomLocation = true;
                CustomDataPath = Path.Combine(customPath, "StoryboardData");
                CustomOutputPath = Path.Combine(customPath, "StoryboardOutput");

                // 创建目录
                Directory.CreateDirectory(CustomDataPath);
                Directory.CreateDirectory(CustomOutputPath);
            }
            else
            {
                // 使用默认位置
                UseCustomLocation = false;
                CustomDataPath = null;
                CustomOutputPath = null;
            }

            Close(true);
        }
        catch (Exception ex)
        {
            await ShowErrorDialog($"确认时出错: {ex.Message}");
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async Task ShowErrorDialog(string message)
    {
        var dialog = new Window
        {
            Title = "错误",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 16
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var button = new Button
        {
            Content = "确定",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Width = 80
        };
        button.Click += (s, e) => dialog.Close();
        panel.Children.Add(button);

        dialog.Content = panel;
        await dialog.ShowDialog(this);
    }
}
