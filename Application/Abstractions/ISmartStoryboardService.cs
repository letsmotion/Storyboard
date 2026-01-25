using Storyboard.Models;

namespace Storyboard.Application.Abstractions;

public interface ISmartStoryboardService
{
    Task<IReadOnlyList<ShotItem>> AnalyzeAsync(
        string videoPath,
        string projectId,
        CancellationToken cancellationToken = default);
}
