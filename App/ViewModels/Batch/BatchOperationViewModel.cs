using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Storyboard.Application.Abstractions;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Messaging;

namespace Storyboard.ViewModels.Batch;

public partial class BatchOperationViewModel : ObservableObject
{
    private readonly IMessenger _messenger;
    private readonly IJobQueueService _jobQueue;
    private readonly ILogger<BatchOperationViewModel> _logger;
    private readonly Dictionary<(GenerationJobType, int), Queue<BatchJobItem>> _pendingByKey = new();
    private DateTimeOffset? _batchStartedAt;
    private string? _currentProjectId;
    private string? _batchProjectId;  // 记录批量操作开始时的项目 ID

    public ObservableCollection<ShotItem> Shots { get; } = new();
    public ObservableCollection<BatchJobItem> BatchJobs { get; } = new();
    public ObservableCollection<BatchSkipItem> SkippedItems { get; } = new();

    [ObservableProperty]
    private int _selectedShotsCount;

    [ObservableProperty]
    private bool _isAiAnalysisSelected = true;

    [ObservableProperty]
    private bool _isFirstFrameSelected = true;

    [ObservableProperty]
    private bool _isLastFrameSelected = true;

    [ObservableProperty]
    private bool _isVideoSelected;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _overallProgressText = "总体进度: 0%";

    [ObservableProperty]
    private string _taskCounterText = "0 / 0";

    [ObservableProperty]
    private bool _isRunning;

    public bool HasSelectedShots => SelectedShotsCount > 0;

    public string SelectedShotsCountText => $"{SelectedShotsCount} 已选择";

    public bool HasOperationsSelected => IsAiAnalysisSelected || IsFirstFrameSelected || IsLastFrameSelected || IsVideoSelected;

    public bool CanExecute => HasSelectedShots && HasOperationsSelected && !IsRunning;

    public string ExecuteButtonText => IsRunning ? "执行中..." : "开始执行";

    public bool HasSkippedItems => SkippedItems.Count > 0;

    public string SkippedCountText => $"已跳过 {SkippedItems.Count} 项";

    public BatchOperationViewModel(
        IMessenger messenger,
        IJobQueueService jobQueue,
        ILogger<BatchOperationViewModel> logger)
    {
        _messenger = messenger;
        _jobQueue = jobQueue;
        _logger = logger;

        _jobQueue.Jobs.CollectionChanged += OnJobsCollectionChanged;
        BatchJobs.CollectionChanged += OnBatchJobsCollectionChanged;
        SkippedItems.CollectionChanged += OnSkippedItemsCollectionChanged;

        // 监听项目切换消息
        _messenger.Register<ProjectOpenedMessage>(this, OnProjectChanged);
        _messenger.Register<ProjectClosedMessage>(this, OnProjectChanged);
        _messenger.Register<ProjectCreatedMessage>(this, OnProjectChanged);
    }

    public void RefreshShots()
    {
        _logger.LogInformation("批量操作: 刷新分镜列表");

        // 获取当前项目 ID
        var projectQuery = new GetCurrentProjectIdQuery();
        _messenger.Send(projectQuery);
        _currentProjectId = projectQuery.ProjectId;

        if (string.IsNullOrWhiteSpace(_currentProjectId))
        {
            _logger.LogWarning("批量操作: 没有打开的项目");
            foreach (var shot in Shots)
            {
                shot.PropertyChanged -= OnShotPropertyChanged;
            }
            Shots.Clear();
            UpdateSelectionState();
            return;
        }

        _logger.LogInformation("批量操作: 当前项目 ID = {ProjectId}", _currentProjectId);

        foreach (var shot in Shots)
        {
            shot.PropertyChanged -= OnShotPropertyChanged;
        }

        Shots.Clear();

        var query = new GetAllShotsQuery();
        _messenger.Send(query);
        if (query.Shots == null)
        {
            _logger.LogWarning("批量操作: 查询分镜失败 (Shots is null)");
            UpdateSelectionState();
            return;
        }

        // 添加所有分镜（GetAllShotsQuery 已经过滤了当前项目）
        foreach (var shot in query.Shots)
        {
            Shots.Add(shot);
            shot.PropertyChanged += OnShotPropertyChanged;
        }

        _logger.LogInformation("批量操作: 刷新完成，共 {Count} 个分镜", Shots.Count);
        UpdateSelectionState();
    }

    [RelayCommand]
    private void SelectAllShots()
    {
        foreach (var shot in Shots)
            shot.IsChecked = true;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var shot in Shots)
            shot.IsChecked = false;
    }

    [RelayCommand]
    private void ExecuteBatch()
    {
        if (!CanExecute)
        {
            _logger.LogWarning("批量操作: 无法执行 (CanExecute=false)");
            return;
        }

        // 验证项目 ID
        if (string.IsNullOrWhiteSpace(_currentProjectId))
        {
            _logger.LogError("批量操作: 无法执行，没有打开的项目");
            return;
        }

        var selectedShots = Shots.Where(s => s.IsChecked).ToList();
        if (selectedShots.Count == 0)
        {
            _logger.LogWarning("批量操作: 没有选中的分镜");
            return;
        }

        // 记录批量操作的项目 ID
        _batchProjectId = _currentProjectId;
        _logger.LogInformation("批量操作开始: 项目 ID = {ProjectId}, 选中 {Count} 个分镜", _batchProjectId, selectedShots.Count);

        BatchJobs.Clear();
        SkippedItems.Clear();
        _pendingByKey.Clear();

        foreach (var shot in selectedShots)
        {
            TryAddJobItem(shot, GenerationJobType.AiParse, IsAiAnalysisSelected, CanQueueAiAnalysis);
            TryAddJobItem(shot, GenerationJobType.ImageFirst, IsFirstFrameSelected, CanQueueFirstFrame);
            TryAddJobItem(shot, GenerationJobType.ImageLast, IsLastFrameSelected, CanQueueLastFrame);
            TryAddJobItem(shot, GenerationJobType.Video, IsVideoSelected, CanQueueVideo);
        }

        _logger.LogInformation("批量操作: 创建了 {JobCount} 个任务, 跳过 {SkipCount} 个",
            BatchJobs.Count, SkippedItems.Count);

        if (BatchJobs.Count == 0)
        {
            _logger.LogWarning("批量操作: 所有任务都被跳过");
            UpdateProgressMetrics();
            return;
        }

        _batchStartedAt = DateTimeOffset.Now;

        foreach (var item in BatchJobs)
        {
            var key = (item.OperationType, item.ShotNumber);
            if (!_pendingByKey.TryGetValue(key, out var queue))
            {
                queue = new Queue<BatchJobItem>();
                _pendingByKey[key] = queue;
            }
            queue.Enqueue(item);
        }

        _logger.LogInformation("批量操作: 开始分发 {Count} 个任务", BatchJobs.Count);

        foreach (var item in BatchJobs)
        {
            _logger.LogInformation("分发任务: Shot {ShotNumber}, Type {Type}",
                item.ShotNumber, item.OperationType);
            DispatchOperation(item);
        }

        AttachExistingJobs();
        UpdateProgressMetrics();

        _logger.LogInformation("批量操作: 任务分发完成");
    }

    private void TryAddJobItem(
        ShotItem shot,
        GenerationJobType jobType,
        bool isEnabled,
        Func<ShotItem, (bool CanQueue, string? Reason)> canQueue)
    {
        if (!isEnabled)
            return;

        var (canRun, reason) = canQueue(shot);
        if (!canRun)
        {
            if (!string.IsNullOrWhiteSpace(reason))
                SkippedItems.Add(new BatchSkipItem(shot, jobType, reason));
            _logger.LogInformation("批量任务跳过: Shot {ShotNumber}, Type {Type}, Reason {Reason}", shot.ShotNumber, jobType, reason ?? "n/a");
            return;
        }

        BatchJobs.Add(new BatchJobItem(shot, jobType));
    }

    private void DispatchOperation(BatchJobItem item)
    {
        switch (item.OperationType)
        {
            case GenerationJobType.AiParse:
                _messenger.Send(new AiParseRequestedMessage(item.Shot));
                break;
            case GenerationJobType.ImageFirst:
                _messenger.Send(new ImageGenerationRequestedMessage(item.Shot, true));
                break;
            case GenerationJobType.ImageLast:
                _messenger.Send(new ImageGenerationRequestedMessage(item.Shot, false));
                break;
            case GenerationJobType.Video:
                _messenger.Send(new VideoGenerationRequestedMessage(item.Shot));
                break;
        }
    }

    private void AttachExistingJobs()
    {
        if (!_batchStartedAt.HasValue)
            return;

        _logger.LogInformation("AttachExistingJobs: 检查现有任务队列，当前队列中有 {Count} 个任务", _jobQueue.Jobs.Count);

        foreach (var job in _jobQueue.Jobs)
        {
            _logger.LogDebug("检查任务: Type {Type}, ShotNumber {ShotNumber}, CreatedAt {CreatedAt}",
                job.Type, job.ShotNumber, job.CreatedAt);
            TryAttachJob(job);
        }
    }

    private void OnJobsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems == null)
            return;

        _logger.LogInformation("JobQueue变化: 新增 {Count} 个任务", e.NewItems.Count);

        foreach (var item in e.NewItems)
        {
            if (item is GenerationJob job)
            {
                _logger.LogInformation("新任务加入队列: Type {Type}, ShotNumber {ShotNumber}, JobId {JobId}",
                    job.Type, job.ShotNumber, job.Id);
                TryAttachJob(job);
            }
        }
    }

    private void TryAttachJob(GenerationJob job)
    {
        if (!_batchStartedAt.HasValue)
        {
            _logger.LogDebug("TryAttachJob: 批处理未开始，跳过关联");
            return;
        }

        if (!job.ShotNumber.HasValue)
        {
            _logger.LogDebug("TryAttachJob: Job没有ShotNumber，跳过关联");
            return;
        }

        if (job.CreatedAt < _batchStartedAt.Value.AddSeconds(-1))
        {
            _logger.LogDebug("TryAttachJob: Job创建时间早于批处理开始时间，跳过关联 (JobCreated: {JobCreated}, BatchStarted: {BatchStarted})",
                job.CreatedAt, _batchStartedAt.Value);
            return;
        }

        var key = (job.Type, job.ShotNumber.Value);
        if (!_pendingByKey.TryGetValue(key, out var queue) || queue.Count == 0)
        {
            _logger.LogWarning("TryAttachJob: 找不到对应的BatchJobItem (Type: {Type}, ShotNumber: {ShotNumber})",
                job.Type, job.ShotNumber.Value);
            return;
        }

        var item = queue.Dequeue();
        item.Job = job;

        _logger.LogInformation("成功关联任务: Shot {ShotNumber}, Type {Type}, JobId {JobId}",
            job.ShotNumber.Value, job.Type, job.Id);

        if (queue.Count == 0)
            _pendingByKey.Remove(key);
    }

    private void OnBatchJobsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is BatchJobItem jobItem)
                    jobItem.PropertyChanged += OnBatchJobItemPropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is BatchJobItem jobItem)
                    jobItem.PropertyChanged -= OnBatchJobItemPropertyChanged;
            }
        }

        UpdateProgressMetrics();
    }

    private void OnSkippedItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasSkippedItems));
        OnPropertyChanged(nameof(SkippedCountText));
    }

    private void OnBatchJobItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BatchJobItem.Status)
            or nameof(BatchJobItem.Progress)
            or nameof(BatchJobItem.IsCompleted))
        {
        UpdateProgressMetrics();
    }
    }

    private void OnShotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShotItem.IsChecked))
            UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        SelectedShotsCount = Shots.Count(s => s.IsChecked);

        OnPropertyChanged(nameof(HasSelectedShots));
        OnPropertyChanged(nameof(SelectedShotsCountText));
        OnPropertyChanged(nameof(CanExecute));
    }

    private void UpdateProgressMetrics()
    {
        var total = BatchJobs.Count;
        if (total == 0)
        {
            OverallProgress = 0;
            TaskCounterText = "0 / 0";
            OverallProgressText = "总体进度: 0%";
            IsRunning = false;
            return;
        }

        var progressSum = BatchJobs.Sum(j => j.Progress);
        var completed = BatchJobs.Count(j => j.IsCompleted);
        OverallProgress = Math.Clamp(progressSum / total, 0, 1);
        TaskCounterText = $"{completed} / {total}";
        OverallProgressText = $"总体进度: {OverallProgress:P0}";
        IsRunning = BatchJobs.Any(j => !j.IsCompleted);
    }

    private static (bool CanQueue, string? Reason) CanQueueAiAnalysis(ShotItem shot)
    {
        if (shot.IsAiParsing)
            return (false, "正在解析");
        if (string.IsNullOrWhiteSpace(shot.MaterialFilePath))
            return (false, "缺少素材");
        if (!File.Exists(shot.MaterialFilePath))
            return (false, "素材不存在");
        return (true, null);
    }

    private static (bool CanQueue, string? Reason) CanQueueFirstFrame(ShotItem shot)
    {
        if (shot.IsFirstFrameGenerating)
            return (false, "正在生成首帧");
        if (!string.IsNullOrWhiteSpace(shot.FirstFrameImagePath))
            return (false, "已生成首帧");
        if (string.IsNullOrWhiteSpace(shot.FirstFramePrompt))
            return (false, "缺少首帧提示词");
        return (true, null);
    }

    private static (bool CanQueue, string? Reason) CanQueueLastFrame(ShotItem shot)
    {
        if (shot.IsLastFrameGenerating)
            return (false, "正在生成尾帧");
        if (!string.IsNullOrWhiteSpace(shot.LastFrameImagePath))
            return (false, "已生成尾帧");
        if (string.IsNullOrWhiteSpace(shot.LastFramePrompt))
            return (false, "缺少尾帧提示词");
        return (true, null);
    }

    private static (bool CanQueue, string? Reason) CanQueueVideo(ShotItem shot)
    {
        if (shot.IsVideoGenerating)
            return (false, "正在生成视频");

        if (!string.IsNullOrWhiteSpace(shot.GeneratedVideoPath) && File.Exists(shot.GeneratedVideoPath))
            return (false, "已生成视频");

        if (string.IsNullOrWhiteSpace(shot.VideoPrompt))
            return (false, "缺少视频提示词");

        // 检查参考图可用性（如果启用了参考图）
        if (shot.UseFirstFrameReference)
        {
            var hasFirstFrame = !string.IsNullOrWhiteSpace(shot.FirstFrameImagePath)
                && File.Exists(shot.FirstFrameImagePath);
            if (!hasFirstFrame)
                return (false, "启用了首帧参考但首帧图片不存在");
        }

        if (shot.UseLastFrameReference)
        {
            var hasLastFrame = !string.IsNullOrWhiteSpace(shot.LastFrameImagePath)
                && File.Exists(shot.LastFrameImagePath);
            if (!hasLastFrame)
                return (false, "启用了尾帧参考但尾帧图片不存在");
        }

        return (true, null);
    }

    partial void OnIsAiAnalysisSelectedChanged(bool value) => OnOperationSelectionChanged();
    partial void OnIsFirstFrameSelectedChanged(bool value) => OnOperationSelectionChanged();
    partial void OnIsLastFrameSelectedChanged(bool value) => OnOperationSelectionChanged();
    partial void OnIsVideoSelectedChanged(bool value) => OnOperationSelectionChanged();

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanExecute));
        OnPropertyChanged(nameof(ExecuteButtonText));
    }

    private void OnOperationSelectionChanged()
    {
        OnPropertyChanged(nameof(HasOperationsSelected));
        OnPropertyChanged(nameof(CanExecute));
    }

    /// <summary>
    /// 处理项目切换事件
    /// </summary>
    private void OnProjectChanged(object recipient, object message)
    {
        if (IsRunning)
        {
            _logger.LogWarning("批量操作: 项目已切换，但批量任务仍在执行 (当前项目: {CurrentProject}, 批量项目: {BatchProject})",
                _currentProjectId, _batchProjectId);
            // 注意：不自动取消任务，因为任务可能已经在执行中
            // 用户应该等待任务完成或手动取消
        }

        // 更新当前项目 ID
        if (message is ProjectOpenedMessage openedMsg)
        {
            _currentProjectId = openedMsg.ProjectId;
            _logger.LogInformation("批量操作: 项目已打开 - {ProjectId}", _currentProjectId);
        }
        else if (message is ProjectCreatedMessage createdMsg)
        {
            _currentProjectId = createdMsg.ProjectId;
            _logger.LogInformation("批量操作: 项目已创建 - {ProjectId}", _currentProjectId);
        }
        else if (message is ProjectClosedMessage)
        {
            _currentProjectId = null;
            _logger.LogInformation("批量操作: 项目已关闭");
        }

        // 清空当前列表（如果不在执行中）
        if (!IsRunning)
        {
            foreach (var shot in Shots)
            {
                shot.PropertyChanged -= OnShotPropertyChanged;
            }
            Shots.Clear();
            BatchJobs.Clear();
            SkippedItems.Clear();
            UpdateSelectionState();
        }
    }

    public void SetOperationSelection(bool aiAnalysis, bool firstFrame, bool lastFrame, bool video)
    {
        IsAiAnalysisSelected = aiAnalysis;
        IsFirstFrameSelected = firstFrame;
        IsLastFrameSelected = lastFrame;
        IsVideoSelected = video;
    }
}
