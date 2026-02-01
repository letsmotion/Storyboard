using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Storyboard.Views;

public partial class EditCoreContentDialog : Window
{
    public EditCoreContentDialog()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
