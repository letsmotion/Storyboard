using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace Storyboard.Models;

public partial class ResourceLibraryItem : ObservableObject
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string? _thumbnailPath;

    [ObservableProperty]
    private DateTimeOffset _createdAt = DateTimeOffset.Now;

    public string DisplayName => Path.GetFileName(FilePath);
}
