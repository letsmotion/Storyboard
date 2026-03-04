using Storyboard.Models;
using System.Collections.Generic;

namespace Storyboard.Messages;

public enum ShotUpdateSource
{
    Unknown = 0,
    User = 1,
    Timeline = 2
}

// 镜头添加消息
public record ShotAddedMessage(ShotItem Shot, ShotUpdateSource Source = ShotUpdateSource.Unknown);

// 镜头删除消息
public record ShotDeletedMessage(ShotItem Shot, ShotUpdateSource Source = ShotUpdateSource.Unknown);

// 镜头更新消息
public record ShotUpdatedMessage(ShotItem Shot, ShotUpdateSource Source = ShotUpdateSource.Unknown);

// 镜头移动消息
public record ShotMovedMessage(ShotItem Shot, int FromIndex, int ToIndex);

// 镜头编号重映射消息（用于时间轴内更新关联）
public record ShotNumbersRemappedMessage(IReadOnlyDictionary<int, int> Map);

// 镜头选中消息
public record ShotSelectedMessage(ShotItem? Shot);

// 镜头复制请求消息
public record ShotDuplicateRequestedMessage(ShotItem Shot);

// 镜头删除请求消息
public record ShotDeleteRequestedMessage(ShotItem Shot);

// 批量插入分镜请求消息
public record BatchInsertShotRequestedMessage(ShotItem AnchorShot, bool insertAfter);

// 显示批量插入对话框消息
public record ShowBatchInsertDialogMessage(ShotItem AnchorShot, bool insertAfter);

// 编辑核心内容请求消息
public record EditCoreContentRequestedMessage(ShotItem Shot);

// 抽帧完成消息
public record FramesExtractedMessage(IReadOnlyList<ShotItem> Shots);

// 查询所有镜头消息（用于跨ViewModel数据访问）
public class GetAllShotsQuery
{
    public IReadOnlyList<ShotItem>? Shots { get; set; }
}

// 查询当前项目ID消息
public class GetCurrentProjectIdQuery
{
    public string? ProjectId { get; set; }
}

// 查询当前项目信息消息
public class GetProjectInfoQuery
{
    public ProjectInfo? ProjectInfo { get; set; }
}

// 查询当前项目路径消息
public class GetCurrentProjectPathQuery
{
    public string? ProjectPath { get; set; }
}

// 从图片创建分镜完成消息
public record ShotsCreatedFromImagesMessage(int SuccessCount);
