using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Storyboard.Models;
using System;

namespace Storyboard.ViewModels.Shot;

/// <summary>
/// 批量插入分镜对话框 ViewModel
/// </summary>
public partial class BatchInsertShotViewModel : ObservableObject
{
    [ObservableProperty]
    private string _textInput = string.Empty;

    [ObservableProperty]
    private bool _isConfirmed = false;

    [ObservableProperty]
    private int _expectedShotCount = 0;

    [ObservableProperty]
    private ShotItem? _anchorShot;

    [ObservableProperty]
    private bool _insertAfter;

    partial void OnTextInputChanged(string value)
    {
        // 简单估算:按换行符分割,每个非空行算一个分镜
        if (string.IsNullOrWhiteSpace(value))
        {
            ExpectedShotCount = 0;
            return;
        }

        var lines = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        ExpectedShotCount = lines.Length;
    }

    [RelayCommand]
    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(TextInput))
            return;

        IsConfirmed = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        IsConfirmed = false;
    }
}
