using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Infrastructure.Media;
using Storyboard.Messages;
using Storyboard.Models;

namespace Storyboard.ViewModels.Generation;

/// <summary>
/// 音频生成 ViewModel - 处理 TTS 配音生成
/// </summary>
public partial class AudioGenerationViewModel : ObservableObject
{
    private readonly ITtsService _ttsService;
    private readonly IMessenger _messenger;
    private readonly ILogger<AudioGenerationViewModel> _logger;

    public AudioGenerationViewModel(
        ITtsService ttsService,
        IMessenger messenger,
        ILogger<AudioGenerationViewModel> logger)
    {
        _ttsService = ttsService;
        _messenger = messenger;
        _logger = logger;

        // 订阅消息
        _messenger.Register<AudioGenerationRequestedMessage>(this, OnAudioGenerationRequested);
        _messenger.Register<AudioPlayRequestedMessage>(this, OnAudioPlayRequested);
        _messenger.Register<AudioDeleteRequestedMessage>(this, OnAudioDeleteRequested);
    }

    private async void OnAudioGenerationRequested(object recipient, AudioGenerationRequestedMessage message)
    {
        var shot = message.Shot;

        if (string.IsNullOrWhiteSpace(shot.AudioText))
        {
            shot.AudioStatusMessage = "请输入配音文本";
            return;
        }

        try
        {
            shot.IsGeneratingAudio = true;
            shot.AudioStatusMessage = "正在生成配音...";

            _logger.LogInformation("开始为镜头 {ShotNumber} 生成配音", shot.ShotNumber);

            // 生成配音
            var audioPath = await _ttsService.GenerateForShotAsync(
                shotId: shot.Id,
                text: shot.AudioText,
                voice: shot.TtsVoice,
                speed: shot.TtsSpeed
            );

            // 更新镜头信息
            shot.GeneratedAudioPath = audioPath;
            shot.AudioDuration = await TryGetAudioDurationAsync(audioPath);
            shot.AudioStatusMessage = $"配音生成成功！";
            shot.OnPropertyChanged(nameof(shot.HasGeneratedAudio));

            _logger.LogInformation("镜头 {ShotNumber} 配音生成成功: {AudioPath}", shot.ShotNumber, audioPath);

            // 发送完成消息
            _messenger.Send(new AudioGenerationCompletedMessage(shot, true, audioPath));
            _messenger.Send(new MarkUndoableChangeMessage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "镜头 {ShotNumber} 配音生成失败", shot.ShotNumber);
            shot.AudioStatusMessage = $"生成失败：{ex.Message}";
            _messenger.Send(new AudioGenerationCompletedMessage(shot, false, null));
        }
        finally
        {
            shot.IsGeneratingAudio = false;
        }
    }

    private void OnAudioPlayRequested(object recipient, AudioPlayRequestedMessage message)
    {
        var shot = message.Shot;

        if (string.IsNullOrWhiteSpace(shot.GeneratedAudioPath) || !File.Exists(shot.GeneratedAudioPath))
        {
            shot.AudioStatusMessage = "音频文件不存在";
            return;
        }

        try
        {
            _logger.LogInformation("播放镜头 {ShotNumber} 的配音: {AudioPath}", shot.ShotNumber, shot.GeneratedAudioPath);

            // 使用系统默认播放器打开音频文件
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = shot.GeneratedAudioPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(processStartInfo);

            shot.AudioStatusMessage = "正在播放音频...";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "播放音频失败: {AudioPath}", shot.GeneratedAudioPath);
            shot.AudioStatusMessage = $"播放失败：{ex.Message}";
        }
    }

    private void OnAudioDeleteRequested(object recipient, AudioDeleteRequestedMessage message)
    {
        var shot = message.Shot;

        if (string.IsNullOrWhiteSpace(shot.GeneratedAudioPath))
        {
            return;
        }

        try
        {
            _logger.LogInformation("删除镜头 {ShotNumber} 的配音: {AudioPath}", shot.ShotNumber, shot.GeneratedAudioPath);

            // 删除音频文件
            if (File.Exists(shot.GeneratedAudioPath))
            {
                File.Delete(shot.GeneratedAudioPath);
            }

            // 清空镜头音频信息
            shot.GeneratedAudioPath = null;
            shot.AudioDuration = 0;
            shot.AudioStatusMessage = "音频已删除";
            shot.OnPropertyChanged(nameof(shot.HasGeneratedAudio));

            _messenger.Send(new MarkUndoableChangeMessage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除音频失败: {AudioPath}", shot.GeneratedAudioPath);
            shot.AudioStatusMessage = $"删除失败：{ex.Message}";
        }
    }

    private async Task<double> TryGetAudioDurationAsync(string path)
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
            // Ignore duration probing failures and keep fallback 0.
        }

        return 0;
    }
}
