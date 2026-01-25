using CommunityToolkit.Mvvm.ComponentModel;
using Storyboard.Models;
using System.ComponentModel;

namespace Storyboard.ViewModels.Batch;

public sealed class BatchJobItem : ObservableObject
{
    private GenerationJob? _job;

    public BatchJobItem(ShotItem shot, GenerationJobType operationType)
    {
        Shot = shot;
        OperationType = operationType;
    }

    public ShotItem Shot { get; }

    public GenerationJobType OperationType { get; }

    public GenerationJob? Job
    {
        get => _job;
        set
        {
            if (ReferenceEquals(_job, value))
                return;

            if (_job != null)
                _job.PropertyChanged -= OnJobPropertyChanged;

            _job = value;

            if (_job != null)
                _job.PropertyChanged += OnJobPropertyChanged;

            OnPropertyChanged();
            NotifyJobStateChanged();
        }
    }

    public int ShotNumber => Shot.ShotNumber;

    public string OperationText => OperationType switch
    {
        GenerationJobType.AiParse => "AI 解析",
        GenerationJobType.ImageFirst => "生成首帧",
        GenerationJobType.ImageLast => "生成尾帧",
        GenerationJobType.Video => "生成视频",
        _ => OperationType.ToString()
    };

    public string Title => $"分镜 #{ShotNumber} - {OperationText}";

    public string IconData => OperationType switch
    {
        GenerationJobType.AiParse => "M12 2l1.5 4.5L18 8l-4.5 1.5L12 14l-1.5-4.5L6 8l4.5-1.5z",
        GenerationJobType.ImageFirst => "M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z",
        GenerationJobType.ImageLast => "M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z",
        GenerationJobType.Video => "M8 5v14l11-7z",
        _ => "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 5h2v6h-2V7zm0 8h2v2h-2v-2z"
    };

    public GenerationJobStatus Status => Job?.Status ?? GenerationJobStatus.Queued;

    public string StatusText => Job?.StatusText ?? "等待";

    public double Progress => Job?.Progress ?? 0;

    public bool IsCompleted => Status is GenerationJobStatus.Succeeded
        or GenerationJobStatus.Failed
        or GenerationJobStatus.Canceled;

    public string? Error => string.IsNullOrWhiteSpace(Job?.Error) ? null : Job!.Error;

    public bool HasError => !string.IsNullOrWhiteSpace(Job?.Error);

    private void OnJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyJobStateChanged();
    }

    private void NotifyJobStateChanged()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(HasError));
    }
}
