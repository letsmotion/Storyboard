using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Storyboard.AI.Core;

namespace Storyboard.Infrastructure.Media;

/// <summary>
/// TTS 语音合成请求
/// </summary>
public sealed record TtsGenerationRequest(
    string Text,
    string Model,
    string Voice,
    double Speed = 1.0,
    string ResponseFormat = "mp3",
    string? OutputPath = null);

/// <summary>
/// TTS 语音合成结果
/// </summary>
public sealed record TtsGenerationResult(
    byte[] AudioBytes,
    string FileExtension,
    string? ModelUsed,
    double DurationSeconds = 0);

/// <summary>
/// TTS 提供商接口
/// </summary>
public interface ITtsProvider
{
    TtsProviderType ProviderType { get; }
    string DisplayName { get; }
    bool IsConfigured { get; }
    IReadOnlyList<string> SupportedModels { get; }
    IReadOnlyList<string> SupportedVoices { get; }
    IReadOnlyList<string> SupportedFormats { get; }
    IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations { get; }
    Task<TtsGenerationResult> GenerateAsync(TtsGenerationRequest request, CancellationToken cancellationToken = default);
}
