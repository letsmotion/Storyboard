using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Storyboard.Application.Abstractions;
using Storyboard.AI.Core;

namespace Storyboard.ViewModels.Audio;

/// <summary>
/// TTS 配音 ViewModel 示例
/// 演示如何使用 TTS 服务为镜头生成配音
/// </summary>
public partial class TtsAudioViewModel : ObservableObject
{
    private readonly ITtsService _ttsService;

    [ObservableProperty]
    private string _audioText = string.Empty;

    [ObservableProperty]
    private string _selectedVoice = "alloy";

    [ObservableProperty]
    private double _speed = 1.0;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string? _generatedAudioPath;

    [ObservableProperty]
    private double _audioDuration;

    public List<string> AvailableVoices { get; } = new()
    {
        "alloy",
        "echo",
        "fable",
        "onyx",
        "nova",
        "shimmer"
    };

    public TtsAudioViewModel(ITtsService ttsService)
    {
        _ttsService = ttsService;
    }

    /// <summary>
    /// 生成配音
    /// </summary>
    [RelayCommand]
    private async Task GenerateAudioAsync()
    {
        if (string.IsNullOrWhiteSpace(AudioText))
        {
            StatusMessage = "请输入配音文本";
            return;
        }

        try
        {
            IsGenerating = true;
            StatusMessage = "正在生成配音...";

            var result = await _ttsService.GenerateAsync(
                text: AudioText,
                voice: SelectedVoice,
                speed: Speed,
                responseFormat: "mp3"
            );

            GeneratedAudioPath = $"临时音频文件，大小：{result.AudioBytes.Length / 1024.0:F2} KB";
            AudioDuration = result.DurationSeconds;
            StatusMessage = $"配音生成成功！时长：{result.DurationSeconds:F2} 秒";
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成失败：{ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// 为镜头生成配音
    /// </summary>
    [RelayCommand]
    private async Task GenerateForShotAsync(long shotId)
    {
        if (string.IsNullOrWhiteSpace(AudioText))
        {
            StatusMessage = "请输入配音文本";
            return;
        }

        try
        {
            IsGenerating = true;
            StatusMessage = $"正在为镜头 {shotId} 生成配音...";

            var audioPath = await _ttsService.GenerateForShotAsync(
                shotId: shotId,
                text: AudioText,
                voice: SelectedVoice,
                speed: Speed
            );

            GeneratedAudioPath = audioPath;
            StatusMessage = $"配音已保存到：{audioPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成失败：{ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// 批量生成配音
    /// </summary>
    [RelayCommand]
    private async Task GenerateBatchAsync()
    {
        // 示例：为多个镜头生成配音
        var shotTexts = new Dictionary<long, string>
        {
            { 1, "第一个镜头的配音文本" },
            { 2, "第二个镜头的配音文本" },
            { 3, "第三个镜头的配音文本" }
        };

        try
        {
            IsGenerating = true;
            StatusMessage = "正在批量生成配音...";

            var progress = new Progress<(int Current, int Total, long ShotId)>(p =>
            {
                StatusMessage = $"进度：{p.Current}/{p.Total}，正在处理镜头 {p.ShotId}";
            });

            var results = await _ttsService.GenerateBatchAsync(
                shotTexts: shotTexts,
                voice: SelectedVoice,
                speed: Speed,
                progress: progress
            );

            StatusMessage = $"批量生成完成！成功生成 {results.Count} 个音频文件";
        }
        catch (Exception ex)
        {
            StatusMessage = $"批量生成失败：{ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// 获取可用的提供商信息
    /// </summary>
    [RelayCommand]
    private void GetProviderInfo()
    {
        var providers = _ttsService.GetAvailableProviders();
        var info = "可用的 TTS 提供商：\n";

        foreach (var provider in providers)
        {
            info += $"\n提供商：{provider.DisplayName}\n";
            info += $"支持的模型：{string.Join(", ", provider.SupportedModels)}\n";
            info += $"支持的音色：{string.Join(", ", provider.SupportedVoices)}\n";
            info += $"支持的格式：{string.Join(", ", provider.SupportedFormats)}\n";
        }

        StatusMessage = info;
    }
}
