using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Services;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Resources;

public partial class ResourceLibraryViewModel : ObservableObject
{
    private readonly StoragePathService _storagePathService;
    private readonly IMessenger _messenger;
    private readonly ILogger<ResourceLibraryViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ResourceLibraryItem> _items = new();

    public ResourceLibraryViewModel(
        StoragePathService storagePathService,
        IMessenger messenger,
        ILogger<ResourceLibraryViewModel> logger)
    {
        _storagePathService = storagePathService;
        _messenger = messenger;
        _logger = logger;

        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            var libraryDir = _storagePathService.GetResourceLibraryDirectory();
            Directory.CreateDirectory(libraryDir);

            var files = Directory
                .GetFiles(libraryDir)
                .Where(IsImageFile)
                .Select(path => new FileInfo(path))
                .OrderByDescending(f => f.CreationTimeUtc)
                .Select(f => new ResourceLibraryItem
                {
                    FilePath = f.FullName,
                    ThumbnailPath = f.FullName,
                    CreatedAt = new DateTimeOffset(f.CreationTimeUtc)
                })
                .ToList();

            Items = new ObservableCollection<ResourceLibraryItem>(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh resource library.");
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        try
        {
            var paths = await PickImagePathsAsync();
            if (paths == null || paths.Count == 0)
                return;

            var libraryDir = _storagePathService.GetResourceLibraryDirectory();
            Directory.CreateDirectory(libraryDir);

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    continue;

                var fileName = Path.GetFileName(path);
                var name = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                var destPath = Path.Combine(libraryDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmssfff}{ext}");

                File.Copy(path, destPath, overwrite: false);
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import resource library items.");
        }
    }

    [RelayCommand]
    private void UseAsFirstFrame(ResourceLibraryItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.FilePath))
            return;

        _messenger.Send(new ResourceLibraryAssetSelectedMessage(item.FilePath, true));
    }

    [RelayCommand]
    private void UseAsLastFrame(ResourceLibraryItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.FilePath))
            return;

        _messenger.Send(new ResourceLibraryAssetSelectedMessage(item.FilePath, false));
    }

    private async Task<IReadOnlyList<string>?> PickImagePathsAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        var mainWindow = desktop.MainWindow;
        if (mainWindow == null)
            return null;

        var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import resource library images",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image files")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" }
                },
                new FilePickerFileType("All files")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        return files?
            .Select(f => f.Path.LocalPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp";
    }
}
