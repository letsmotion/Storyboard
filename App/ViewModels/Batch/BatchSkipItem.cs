using Storyboard.Models;

namespace Storyboard.ViewModels.Batch;

public sealed class BatchSkipItem
{
    public BatchSkipItem(ShotItem shot, GenerationJobType operationType, string reason)
    {
        ShotNumber = shot.ShotNumber;
        OperationType = operationType;
        Reason = reason;
    }

    public int ShotNumber { get; }

    public GenerationJobType OperationType { get; }

    public string Reason { get; }

    public string OperationText => OperationType switch
    {
        GenerationJobType.AiParse => "AI 解析",
        GenerationJobType.ImageFirst => "生成首帧",
        GenerationJobType.ImageLast => "生成尾帧",
        GenerationJobType.Video => "生成视频",
        _ => OperationType.ToString()
    };

    public string Title => $"分镜 #{ShotNumber} - {OperationText}";
}
