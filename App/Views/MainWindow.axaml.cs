using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Storyboard;
using Storyboard.ViewModels;
using System.ComponentModel;

namespace Storyboard.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel viewModel)
            return;

        System.Diagnostics.Debug.WriteLine($"MainWindow: PropertyChanged - {e.PropertyName}");

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsNewProjectDialogOpen):
                System.Diagnostics.Debug.WriteLine($"MainWindow: IsNewProjectDialogOpen = {viewModel.IsNewProjectDialogOpen}");
                if (viewModel.IsNewProjectDialogOpen)
                {
                    var dialog = new CreateProjectDialog { DataContext = viewModel };
                    await dialog.ShowDialog(this);
                    viewModel.IsNewProjectDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsExportDialogOpen):
                if (viewModel.IsExportDialogOpen)
                {
                    var dialog = new ExportDialog { DataContext = viewModel };
                    await dialog.ShowDialog(this);
                    viewModel.IsExportDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsProviderSettingsDialogOpen):
                if (viewModel.IsProviderSettingsDialogOpen)
                {
                    var providerVm = App.Services.GetRequiredService<ApiKeyViewModel>();
                    var dialog = new ProviderSettingsDialog { DataContext = providerVm };
                    await dialog.ShowDialog(this);
                    viewModel.IsProviderSettingsDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsTextToShotDialogOpen):
                if (viewModel.IsTextToShotDialogOpen)
                {
                    var dialog = new TextToShotDialog { DataContext = viewModel };
                    await dialog.ShowDialog(this);
                    viewModel.IsTextToShotDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsBatchOperationDialogOpen):
                if (viewModel.IsBatchOperationDialogOpen)
                {
                    var dialog = new BatchOperationDialog { DataContext = viewModel.BatchOperation };
                    await dialog.ShowDialog(this);
                    viewModel.IsBatchOperationDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsResourceLibraryDialogOpen):
                if (viewModel.IsResourceLibraryDialogOpen)
                {
                    await viewModel.ResourceLibrary.RefreshAsync();
                    var dialog = new ResourceLibraryDialog { DataContext = viewModel.ResourceLibrary };
                    await dialog.ShowDialog(this);
                    viewModel.IsResourceLibraryDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsBatchInsertDialogOpen):
                if (viewModel.IsBatchInsertDialogOpen)
                {
                    var batchInsertVm = App.Services.GetRequiredService<ViewModels.Shot.BatchInsertShotViewModel>();
                    batchInsertVm.AnchorShot = viewModel.BatchInsertAnchorShot;
                    batchInsertVm.InsertAfter = viewModel.BatchInsertAfter;
                    var dialog = new BatchInsertShotDialog { DataContext = batchInsertVm };
                    await dialog.ShowDialog(this);

                    // 如果用户确认,则调用批量生成逻辑
                    if (batchInsertVm.IsConfirmed && !string.IsNullOrWhiteSpace(batchInsertVm.TextInput))
                    {
                        var lines = batchInsertVm.TextInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        await viewModel.BatchGenerateShotsAsync(batchInsertVm.AnchorShot, batchInsertVm.InsertAfter, lines);
                    }

                    viewModel.IsBatchInsertDialogOpen = false;
                }
                break;

            case nameof(MainViewModel.IsEditCoreContentDialogOpen):
                if (viewModel.IsEditCoreContentDialogOpen)
                {
                    var editVm = new ViewModels.Shot.EditCoreContentViewModel
                    {
                        TargetShot = viewModel.EditingCoreContentShot,
                        TextInput = viewModel.EditingCoreContentShot?.CoreContent ?? string.Empty
                    };
                    var dialog = new EditCoreContentDialog { DataContext = editVm };
                    await dialog.ShowDialog(this);

                    if (editVm.IsConfirmed && editVm.TargetShot != null)
                    {
                        editVm.TargetShot.CoreContent = editVm.TextInput;
                    }

                    viewModel.IsEditCoreContentDialogOpen = false;
                }
                break;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
        base.OnClosing(e);
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnToggleMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnLeftSplitterDragCompleted(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            var grid = this.FindControl<Grid>("WorkspaceGrid");
            if (grid?.ColumnDefinitions.Count > 0)
            {
                var actualWidth = grid.ColumnDefinitions[0].ActualWidth;
                viewModel.LeftSidebarWidth = actualWidth;
            }
        }
    }

    private void OnRightSplitterDragCompleted(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            var grid = this.FindControl<Grid>("WorkspaceGrid");
            if (grid?.ColumnDefinitions.Count > 4)
            {
                var actualWidth = grid.ColumnDefinitions[4].ActualWidth;
                viewModel.RightPanelWidth = actualWidth;
            }
        }
    }
}
