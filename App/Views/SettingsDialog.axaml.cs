using Avalonia.Controls;
using Storyboard.ViewModels;
using System.ComponentModel;

namespace Storyboard.Views;

public partial class SettingsDialog : Window
{
    private SettingsViewModel? _currentViewModel;

    public SettingsDialog()
    {
        InitializeComponent();

        // 监听 DataContext 变化
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        // 如果之前有 ViewModel，取消订阅
        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // 订阅新的 ViewModel
        if (DataContext is SettingsViewModel viewModel)
        {
            _currentViewModel = viewModel;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        else
        {
            _currentViewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 当 IsDialogOpen 变为 false 时，关闭窗口
        if (e.PropertyName == nameof(SettingsViewModel.IsDialogOpen))
        {
            if (sender is SettingsViewModel viewModel && !viewModel.IsDialogOpen)
            {
                Close();
            }
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // 取消订阅，避免内存泄漏
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}
