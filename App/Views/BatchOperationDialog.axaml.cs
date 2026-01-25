using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Storyboard.ViewModels.Batch;

namespace Storyboard.Views;

public partial class BatchOperationDialog : Window
{
    private readonly ILogger<BatchOperationDialog> _logger;

    public BatchOperationDialog()
    {
        InitializeComponent();
        _logger = App.Services?.GetService<ILogger<BatchOperationDialog>>()
            ?? NullLogger<BatchOperationDialog>.Instance;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is not BatchOperationViewModel viewModel)
            return;

        viewModel.RefreshShots();
        _logger.LogInformation("BatchOperationDialog: refreshed shots, count={Count}", viewModel.Shots.Count);
    }
}
