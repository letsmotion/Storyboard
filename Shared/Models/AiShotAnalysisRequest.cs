namespace Storyboard.Models;

public sealed record AiShotAnalysisRequest(
    string? MaterialImagePath,
    string? ExistingShotType,
    string? ExistingCoreContent,
    string? ExistingActionCommand,
    string? ExistingSceneSettings,
    string? ExistingFirstFramePrompt,
    string? ExistingLastFramePrompt,
    string? ContextSummary = null); // 上下文摘要，用于保持风格连贯
