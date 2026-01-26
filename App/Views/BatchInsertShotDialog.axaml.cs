using Avalonia.Controls;
using Avalonia.Interactivity;
using Storyboard.ViewModels.Shot;

namespace Storyboard.Views;

public partial class BatchInsertShotDialog : Window
{
    public BatchInsertShotDialog()
    {
        InitializeComponent();
        DataContext = new BatchInsertShotViewModel();
    }

    public BatchInsertShotDialog(BatchInsertShotViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public BatchInsertShotViewModel? ViewModel => DataContext as BatchInsertShotViewModel;

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
