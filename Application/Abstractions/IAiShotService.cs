using Storyboard.Models;

namespace Storyboard.Application.Abstractions;

public interface IAiShotService
{
    Task<AiShotDescription> AnalyzeShotAsync(
        AiShotAnalysisRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiShotDescription>> GenerateShotsFromTextAsync(
        string prompt,
        int? shotCount = null,
        string? creativeGoal = null,
        string? targetAudience = null,
        string? videoTone = null,
        string? keyMessage = null,
        CancellationToken cancellationToken = default);

    Task<AiShotDescription> GenerateIntermediateShotAsync(
        string previousShotContext,
        string? nextShotContext = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiShotSegment>> AnalyzeStoryboardFromContactSheetAsync(
        string contactSheetPath,
        string mappingText,
        VideoMetadata metadata,
        CancellationToken cancellationToken = default);
}
