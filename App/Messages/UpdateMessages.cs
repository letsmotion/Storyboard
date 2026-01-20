using Velopack;

namespace Storyboard.Messages;

/// <summary>
/// 发现可用更新时发送的消息
/// </summary>
public record UpdateAvailableMessage(UpdateInfo UpdateInfo);

/// <summary>
/// 更新下载进度消息
/// </summary>
public record UpdateDownloadProgressMessage(int Progress);

/// <summary>
/// 更新下载完成消息
/// </summary>
public record UpdateDownloadCompletedMessage(UpdateInfo UpdateInfo);
