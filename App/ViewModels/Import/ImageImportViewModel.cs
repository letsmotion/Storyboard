using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Application.Services;
using Storyboard.Domain.Entities;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;

namespace Storyboard.ViewModels.Import;

/// <summary>
/// 图片导入 ViewModel - 负责图片文件选择和批量创建分镜
/// </summary>
public partial class ImageImportViewModel : ObservableObject
{
    private readonly IShotRepository _shotRepository;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly StoragePathService _storagePathService;
    private readonly IMessenger _messenger;
    private readonly ILogger<ImageImportViewModel> _logger;

    private string? _currentProjectId;

    [ObservableProperty]
    private ObservableCollection<ImageImportItem> _selectedImages = new();

    [ObservableProperty]
    private bool _hasImages;

    [ObservableProperty]
    private int _imageCount;

    [ObservableProperty]
    private string _imageCountText = "未选择图片";

    [ObservableProperty]
    private bool _isCreatingShots;

    [ObservableProperty]
    private string? _errorMessage;

    public ImageImportViewModel(
        IShotRepository shotRepository,
        IUnitOfWorkFactory unitOfWorkFactory,
        StoragePathService storagePathService,
        IMessenger messenger,
        ILogger<ImageImportViewModel> logger)
    {
        _shotRepository = shotRepository;
        _unitOfWorkFactory = unitOfWorkFactory;
        _storagePathService = storagePathService;
        _messenger = messenger;
        _logger = logger;

        // 订阅项目打开/关闭消息
        _messenger.Register<ProjectCreatedMessage>(this, OnProjectCreated);
        _messenger.Register<ProjectOpenedMessage>(this, OnProjectOpened);
        _messenger.Register<ProjectDataLoadedMessage>(this, OnProjectDataLoaded);
        _messenger.Register<ProjectClosedMessage>(this, OnProjectClosed);
    }

    [RelayCommand]
    private async Task ImportImages()
    {
        _logger.LogInformation("ImportImages command called");

        // 清空之前的错误消息
        ErrorMessage = null;

        var paths = await PickImagePathsAsync();

        if (paths == null || paths.Length == 0)
        {
            _logger.LogWarning("No image files selected");
            return;
        }

        _logger.LogInformation("Selected {Count} image files", paths.Length);

        // 清空之前的选择
        SelectedImages.Clear();

        foreach (var path in paths)
        {
            var item = await CreateImageImportItemAsync(path);
            SelectedImages.Add(item);
        }

        UpdateImageCount();
    }

    [RelayCommand]
    private void ClearImages()
    {
        SelectedImages.Clear();
        UpdateImageCount();
        ErrorMessage = null;
        _logger.LogInformation("Cleared all selected images");
    }

    [RelayCommand(CanExecute = nameof(CanCreateShots))]
    private async Task CreateShots()
    {
        if (string.IsNullOrWhiteSpace(_currentProjectId))
        {
            ErrorMessage = "请先创建或打开项目";
            _logger.LogWarning("Cannot create shots: no project opened");
            return;
        }

        if (!SelectedImages.Any(i => i.IsValid))
        {
            ErrorMessage = "没有有效的图片可以创建分镜";
            _logger.LogWarning("Cannot create shots: no valid images");
            return;
        }

        IsCreatingShots = true;
        ErrorMessage = null;

        try
        {
            // 复制图片到项目存储目录
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var importDir = Path.Combine(
                _storagePathService.GetOutputDirectory(),
                "shots",
                "imported-images",
                timestamp);

            Directory.CreateDirectory(importDir);
            _logger.LogInformation("Created import directory: {Dir}", importDir);

            // 获取当前最大分镜编号
            var query = new GetAllShotsQuery();
            _messenger.Send(query);
            var existingShots = query.Shots ?? new List<ShotItem>();
            var nextShotNumber = existingShots.Any() ? existingShots.Max(s => s.ShotNumber) + 1 : 1;

            var createdShots = new List<Domain.Entities.Shot>();
            var validImages = SelectedImages.Where(i => i.IsValid).ToList();

            foreach (var image in validImages)
            {
                try
                {
                    // 复制图片文件
                    var fileName = Path.GetFileName(image.FilePath);
                    var destPath = Path.Combine(importDir, fileName);
                    File.Copy(image.FilePath, destPath, overwrite: true);

                    // 创建分镜实体
                    var shot = new Domain.Entities.Shot
                    {
                        ProjectId = _currentProjectId,
                        ShotNumber = nextShotNumber++,
                        FirstFrameImagePath = destPath,
                        MaterialFilePath = destPath,  // 同时设置为素材路径,便于AI分析
                        UseFirstFrameReference = true,
                        Duration = 3.5  // 默认时长3.5秒
                    };

                    createdShots.Add(shot);
                    image.ShotNumber = shot.ShotNumber;

                    _logger.LogInformation("Prepared shot {ShotNumber} from image {FileName}",
                        shot.ShotNumber, fileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process image: {Path}", image.FilePath);
                    image.IsValid = false;
                    image.ValidationError = $"处理失败: {ex.Message}";
                }
            }

            if (createdShots.Count == 0)
            {
                ErrorMessage = "没有成功创建任何分镜";
                _logger.LogWarning("No shots were created");
                return;
            }

            // 批量插入数据库
            var uow = await _unitOfWorkFactory.CreateAsync();
            await using (uow)
            {
                await _shotRepository.AddRangeAsync(createdShots);
                await uow.SaveChangesAsync();
            }

            _logger.LogInformation("Successfully created {Count} shots", createdShots.Count);

            // 发送消息通知刷新
            _messenger.Send(new ShotsCreatedFromImagesMessage(createdShots.Count));

            // 清空图片列表
            SelectedImages.Clear();
            UpdateImageCount();

            // 显示成功提示
            ErrorMessage = null;
            _logger.LogInformation("Image import completed: {Count} shots created", createdShots.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create shots from images");
            ErrorMessage = $"创建分镜失败: {ex.Message}";
        }
        finally
        {
            IsCreatingShots = false;
        }
    }

    private bool CanCreateShots()
    {
        return HasImages && !IsCreatingShots && !string.IsNullOrWhiteSpace(_currentProjectId);
    }

    private async Task<string[]?> PickImagePathsAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            _logger.LogWarning("ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime");
            return null;
        }

        var mainWindow = desktop.MainWindow;
        if (mainWindow == null)
        {
            _logger.LogWarning("MainWindow is null");
            return null;
        }

        var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择图片文件",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("图片文件")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" }
                },
                new FilePickerFileType("所有文件")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        return files?.Select(f => f.Path.LocalPath).ToArray();
    }

    private async Task<ImageImportItem> CreateImageImportItemAsync(string filePath)
    {
        var item = new ImageImportItem
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            // 验证文件存在
            if (!File.Exists(filePath))
            {
                item.IsValid = false;
                item.ValidationError = "文件不存在";
                return item;
            }

            // 验证文件格式
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (!new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" }.Contains(ext))
            {
                item.IsValid = false;
                item.ValidationError = "不支持的图片格式";
                return item;
            }

            // 获取文件大小
            var fileInfo = new FileInfo(filePath);
            item.FileSize = FormatFileSize(fileInfo.Length);

            // 验证文件大小 (50MB限制)
            if (fileInfo.Length > 50 * 1024 * 1024)
            {
                item.IsValid = false;
                item.ValidationError = "文件过大 (>50MB)";
                return item;
            }

            // 获取图片尺寸
            await Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(filePath);
                    using var bitmap = SKBitmap.Decode(stream);
                    if (bitmap != null)
                    {
                        item.ImageSize = $"{bitmap.Width}x{bitmap.Height}";
                    }
                    else
                    {
                        item.ImageSize = "未知";
                    }
                }
                catch
                {
                    item.ImageSize = "未知";
                }
            });

            item.IsValid = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create ImageImportItem for {Path}", filePath);
            item.IsValid = false;
            item.ValidationError = $"处理失败: {ex.Message}";
        }

        return item;
    }

    private void UpdateImageCount()
    {
        ImageCount = SelectedImages.Count;
        HasImages = ImageCount > 0;
        ImageCountText = HasImages ? $"已选择 {ImageCount} 张图片" : "未选择图片";
        CreateShotsCommand.NotifyCanExecuteChanged();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private void OnProjectCreated(object recipient, ProjectCreatedMessage message)
    {
        _currentProjectId = message.ProjectId;
        SelectedImages.Clear();
        UpdateImageCount();
        ErrorMessage = null;
        _logger.LogInformation("Project created: {ProjectId}", message.ProjectId);
    }

    private void OnProjectOpened(object recipient, ProjectOpenedMessage message)
    {
        _currentProjectId = message.ProjectId;
        _logger.LogInformation("Project opened: {ProjectId}", message.ProjectId);
    }

    private void OnProjectDataLoaded(object recipient, ProjectDataLoadedMessage message)
    {
        _currentProjectId = message.ProjectState.Id;
        _logger.LogInformation("Project data loaded: {ProjectId}", _currentProjectId);
    }

    private void OnProjectClosed(object recipient, ProjectClosedMessage message)
    {
        _currentProjectId = null;
        SelectedImages.Clear();
        UpdateImageCount();
        ErrorMessage = null;
        _logger.LogInformation("Project closed");
    }
}
