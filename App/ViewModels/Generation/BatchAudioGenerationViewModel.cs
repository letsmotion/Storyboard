using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;
using Storyboard.Messages;
using Storyboard.Models;

namespace Storyboard.ViewModels.Generation;

/// <summary>
/// 批量配音生成 ViewModel
/// </summary>
public partial class BatchAudioGenerationViewModel : ObservableObject
{
    private readonly ITtsService _ttsService;
    private readonly IMessenger _messenger;
    private readonly ILogger<BatchAudioGenerationViewModel> _logger;
    private CancellationTokenSource? _cancellationTokenSource;

    private static readonly string[] OpenAiVoices = { "alloy", "echo", "fable", "onyx", "nova", "shimmer" };
    private static readonly string[] QwenVoices = { "Cherry", "alexa", "arwen", "bethany", "daniel", "donna", "emily", "emma", "erika", "gabriel", "geralt", "giulia", "hani", "heather", "helen", "jacob", "jessica", "jiaxi", "jinli", "julie", "kanying", "lily", "lucas", "marc", "maria", "mason", "meng", "michael", "mila", "ray", "rachel", "richard", "riley", "rose", "sarah", "seth", "shawn", "sophia", "stefan", "stella", "summer", "taylor", "thomas", "tom", "xiaobing", "xiaoxiao", "xiaoyi", "yating", "yunjian", "yunxi", "yunxia", "yunyang", "zhenda", "zhuoming" };
    private static readonly string[] VolcengineVoices = { "zh_female_vv_yingjian_soungis", "zh_male_vv_yingjian_soungis", "zh_female_vv_shengcheng_jingying", "zh_male_vv_shengcheng_jingying", "zh_female_vv_shichang_jingying", "zh_male_vv_shichang_jingying", "zh_female_vv_xiaoyuan_jingying", "zh_male_vv_xiaoyuan_jingying", "zh_female_vv_badao_jingying", "zh_male_vv_badao_jingying", "zh_female_vv_changjiang_jingying", "zh_male_vv_changjiang_jingying", "zh_female_vv_zhongjiao_jingying", "zh_male_vv_zhongjiao_jingying", "zh_female_vv_yujie_jingying", "zh_male_vv_yujie_jingying" };

    // 选中的镜头
    private List<ShotItem> _selectedShots = new();

    // 语音选项
    [ObservableProperty] private ObservableCollection<string> _voiceOptions = new(OpenAiVoices);
    [ObservableProperty] private string _selectedVoice = "alloy";
    [ObservableProperty] private ObservableCollection<string> _modelOptions = new(ShotItem.TtsModelOptions);
    [ObservableProperty] private string _selectedModel = string.Empty;

    public string VoiceHint
    {
        get
        {
            var m = SelectedModel?.ToLowerInvariant() ?? "";
            if (m.Contains("qwen") || m.Contains("千问")) return "千问音色，如 Cherry, alexa, emily 等";
            if (m.Contains("volc") || m.Contains("火山") || m.Contains("doubao")) return "火山引擎音色 ID，如 zh_female_vv_yingjian_soungis";
            return "alloy: 中性 | echo: 男性 | fable: 英式 | onyx: 深沉 | nova: 女性 | shimmer: 柔和";
        }
    }

    private TtsProviderType _currentProvider = TtsProviderType.NewApi;
    [ObservableProperty] private double _speed = 1.0;

    // 生成选项
    [ObservableProperty] private bool _skipExistingAudio = true;
    [ObservableProperty] private bool _useExistingText = true;

    // 进度状态
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private int _completedCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _currentStatus = string.Empty;

    // 结果统计
    [ObservableProperty] private bool _showResults;
    [ObservableProperty] private int _successCount;
    [ObservableProperty] private int _failureCount;
    [ObservableProperty] private int _skippedCount;
    [ObservableProperty] private ObservableCollection<string> _failureMessages = new();

    // 计算属性
    public int SelectedShotsCount => _selectedShots.Count;
    public bool HasFailures => FailureCount > 0;
    public bool HasSkipped => SkippedCount > 0;
    public bool CanGenerate => !IsGenerating && _selectedShots.Count > 0;
    public string GenerateButtonText => IsGenerating ? "生成中..." : "开始生成";
    public string CancelButtonText => IsGenerating ? "取消" : "关闭";

    public BatchAudioGenerationViewModel(
        ITtsService ttsService,
        IMessenger messenger,
        ILogger<BatchAudioGenerationViewModel> logger)
    {
        _ttsService = ttsService;
        _messenger = messenger;
        _logger = logger;
    }

    /// <summary>
    /// 设置要生成配音的镜头列表，并根据当前默认 TTS provider 更新音色列表
    /// </summary>
    public void SetShots(IEnumerable<ShotItem> shots)
    {
        _selectedShots = shots.ToList();
        OnPropertyChanged(nameof(SelectedShotsCount));
        OnPropertyChanged(nameof(CanGenerate));

        // 用第一个镜头的 model 初始化，或用默认 provider 的 model
        var firstShot = _selectedShots.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstShot?.TtsModel))
        {
            SelectedModel = firstShot.TtsModel;
        }
        else
        {
            var provider = _ttsService.GetDefaultProvider();
            _currentProvider = provider.ProviderType;
            SelectedModel = _currentProvider switch
            {
                TtsProviderType.Volcengine => "doubao-e2-audio-160k",
                TtsProviderType.Qwen => "qwen3-tts-instruct-flash",
                _ => "gpt-4o-mini-tts"
            };
        }
    }

    partial void OnSelectedModelChanged(string value)
    {
        var voices = ShotItem.GetVoiceOptionsForModel(value);
        VoiceOptions.Clear();
        foreach (var v in voices)
            VoiceOptions.Add(v);

        if (!VoiceOptions.Contains(SelectedVoice))
            SelectedVoice = VoiceOptions.FirstOrDefault() ?? "alloy";

        OnPropertyChanged(nameof(VoiceHint));
    }

    /// <summary>
    /// 开始批量生成
    /// </summary>
    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (IsGenerating || _selectedShots.Count == 0)
            return;

        try
        {
            IsGenerating = true;
            ShowResults = false;
            Progress = 0;
            CompletedCount = 0;
            TotalCount = _selectedShots.Count;
            SuccessCount = 0;
            FailureCount = 0;
            SkippedCount = 0;
            FailureMessages.Clear();

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            _logger.LogInformation("开始批量生成配音，共 {Count} 个镜头", TotalCount);

            for (int i = 0; i < _selectedShots.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    _logger.LogInformation("批量生成已取消");
                    CurrentStatus = "生成已取消";
                    break;
                }

                var shot = _selectedShots[i];
                CurrentStatus = $"正在处理镜头 {shot.ShotNumber}...";

                try
                {
                    // 检查是否跳过已有配音
                    if (SkipExistingAudio && shot.HasGeneratedAudio)
                    {
                        _logger.LogInformation("跳过镜头 {ShotNumber}（已有配音）", shot.ShotNumber);
                        SkippedCount++;
                        CompletedCount++;
                        Progress = (CompletedCount / (double)TotalCount) * 100;
                        continue;
                    }

                    // 确定配音文本
                    string text;
                    if (UseExistingText && !string.IsNullOrWhiteSpace(shot.AudioText))
                    {
                        text = shot.AudioText;
                    }
                    else if (!string.IsNullOrWhiteSpace(shot.SceneDescription))
                    {
                        text = shot.SceneDescription;
                    }
                    else
                    {
                        _logger.LogWarning("镜头 {ShotNumber} 没有可用的文本", shot.ShotNumber);
                        FailureCount++;
                        FailureMessages.Add($"镜头 {shot.ShotNumber}: 没有可用的文本");
                        CompletedCount++;
                        Progress = (CompletedCount / (double)TotalCount) * 100;
                        continue;
                    }

                    // 更新镜头状态
                    shot.IsGeneratingAudio = true;
                    shot.AudioStatusMessage = "正在生成配音...";

                    // 生成配音
                    var audioPath = await _ttsService.GenerateForShotAsync(
                        shotId: (long)shot.ShotNumber,
                        text: text,
                        model: SelectedModel,
                        voice: SelectedVoice,
                        speed: Speed,
                        cancellationToken: token
                    );

                    // 更新镜头信息
                    shot.GeneratedAudioPath = audioPath;
                    shot.AudioDuration = await TryGetAudioDurationAsync(audioPath);
                    shot.TtsVoice = SelectedVoice;
                    shot.TtsModel = SelectedModel;
                    shot.TtsSpeed = Speed;
                    if (UseExistingText || string.IsNullOrWhiteSpace(shot.AudioText))
                    {
                        shot.AudioText = text;
                    }
                    shot.AudioStatusMessage = "配音生成成功";
                    shot.NotifyPropertyChanged(nameof(shot.HasGeneratedAudio));

                    SuccessCount++;
                    _logger.LogInformation("镜头 {ShotNumber} 配音生成成功", shot.ShotNumber);

                    // 发送完成消息
                    _messenger.Send(new AudioGenerationCompletedMessage(shot, true, audioPath));
                }
                catch (OperationCanceledException)
                {
                    shot.IsGeneratingAudio = false;
                    shot.AudioStatusMessage = "生成已取消";
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "镜头 {ShotNumber} 配音生成失败", shot.ShotNumber);
                    shot.AudioStatusMessage = $"生成失败：{ex.Message}";
                    FailureCount++;
                    FailureMessages.Add($"镜头 {shot.ShotNumber}: {ex.Message}");
                    _messenger.Send(new AudioGenerationCompletedMessage(shot, false, null));
                }
                finally
                {
                    shot.IsGeneratingAudio = false;
                    CompletedCount++;
                    Progress = (CompletedCount / (double)TotalCount) * 100;
                }
            }

            // 显示结果
            ShowResults = true;
            CurrentStatus = token.IsCancellationRequested ? "生成已取消" : "生成完成";

            _logger.LogInformation(
                "批量生成完成：成功 {Success}，失败 {Failure}，跳过 {Skipped}",
                SuccessCount, FailureCount, SkippedCount
            );

            // 发送撤销标记
            if (SuccessCount > 0)
            {
                _messenger.Send(new MarkUndoableChangeMessage());
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("批量生成被用户取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量生成过程中发生错误");
            CurrentStatus = $"生成失败：{ex.Message}";
        }
        finally
        {
            IsGenerating = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            OnPropertyChanged(nameof(CanGenerate));
            OnPropertyChanged(nameof(GenerateButtonText));
            OnPropertyChanged(nameof(CancelButtonText));
        }
    }

    /// <summary>
    /// 取消生成或关闭对话框
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        if (IsGenerating)
        {
            _logger.LogInformation("用户请求取消批量生成");
            _cancellationTokenSource?.Cancel();
        }
    }

    partial void OnIsGeneratingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGenerate));
        OnPropertyChanged(nameof(GenerateButtonText));
        OnPropertyChanged(nameof(CancelButtonText));
    }

    private static async Task<double> TryGetAudioDurationAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return 0;

        try
        {
            var ffprobePath = FfmpegLocator.GetFfprobePath();
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v error -print_format json -show_format \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                return 0;

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
                return 0;

            using var doc = JsonDocument.Parse(stdout);
            if (doc.RootElement.TryGetProperty("format", out var format) &&
                format.TryGetProperty("duration", out var durationElement) &&
                double.TryParse(durationElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) &&
                duration > 0)
            {
                return duration;
            }
        }
        catch
        {
            // Ignore probing failures and keep duration as 0.
        }

        return 0;
    }
}
