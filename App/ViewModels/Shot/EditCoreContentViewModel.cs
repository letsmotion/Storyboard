using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Storyboard.Models;

namespace Storyboard.ViewModels.Shot;

/// <summary>
/// ViewModel for editing CoreContent in a dialog
/// </summary>
public partial class EditCoreContentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _textInput = string.Empty;

    [ObservableProperty]
    private bool _isConfirmed;

    public ShotItem? TargetShot { get; set; }

    [RelayCommand]
    private void Confirm()
    {
        IsConfirmed = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        IsConfirmed = false;
    }
}
