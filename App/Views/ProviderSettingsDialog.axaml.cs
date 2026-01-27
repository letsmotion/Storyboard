using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Storyboard.AI.Core;
using Storyboard.ViewModels;
using System;

namespace Storyboard.Views;

public partial class ProviderSettingsDialog : Window
{
    private ApiKeyViewModel? _viewModel;

    public ProviderSettingsDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnDialogClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as ApiKeyViewModel;
        if (_viewModel != null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnProviderCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control sourceControl && sourceControl.FindAncestorOfType<ToggleSwitch>() is not null)
            return;

        if (sender is not Control control)
            return;

        if (control.Tag is not string tag)
            return;

        if (DataContext is not ApiKeyViewModel vm)
            return;

        if (Enum.TryParse(tag, out AIProviderType provider))
        {
            vm.SelectedProvider = provider;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel = null;
        }
    }
}
