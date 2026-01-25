using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Storyboard.Views;

public partial class ResourceLibraryDialog : Window
{
    public ResourceLibraryDialog()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
