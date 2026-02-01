using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Application.Services;
using Storyboard.Infrastructure.Media;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Storyboard.Infrastructure.Services;

public sealed class FinalRenderService : IFinalRenderService
{
    private readonly ILogger<FinalRenderService> _logger;
    private readonly StoragePathService _storagePathService;

    public FinalRenderService(
        ILogger<FinalRenderService> logger,
        StoragePathService storagePathService)
    {
        _logger = logger;
        _storagePathService = storagePathService;
    }

    public async Task<string> RenderAsync(IReadOnlyList<string> clipPaths, CancellationToken cancellationToken, IProgress<double>? progress = null, VideoExportSettings? settings = null)
    {
        if (clipPaths == null || clipPaths.Count == 0)
            throw new ArgumentException("没有可用于合成的视频片段", nameof(clipPaths));

        var missing = clipPaths.Where(p => string.IsNullOrWhiteSpace(p) || !File.Exists(p)).ToList();
        if (missing.Count > 0)
            throw new FileNotFoundException($"存在缺失的视频片段，无法合成：\n{string.Join("\n", missing)}");

        // Use provided settings or defaults
        settings ??= new VideoExportSettings();

        var outputDir = _storagePathService.GetFinalRenderOutputDirectory();
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, $"final_{DateTime.Now:yyyyMMdd_HHmmss}.{settings.Format}");
        var listFile = Path.Combine(outputDir, $"concat_{Guid.NewGuid():N}.txt");

        try
        {
            var sb = new StringBuilder();
            foreach (var p in clipPaths)
            {
                var abs = Path.GetFullPath(p);
                sb.Append("file '");
                sb.Append(abs.Replace("'", "'\\''"));
                sb.AppendLine("'");
            }
            // Use UTF8 without BOM - ffmpeg doesn't expect BOM in concat files
            await File.WriteAllTextAsync(listFile, sb.ToString(), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);

            progress?.Report(0);

            // Get total duration of all clips to calculate progress
            var totalDuration = await GetTotalDurationAsync(clipPaths, cancellationToken).ConfigureAwait(false);

            // Always re-encode to ensure output matches specified settings
            // Copy mode would preserve original properties which may be inconsistent
            var reencodeArgs = BuildReencodeArgs(listFile, outputPath, settings);
            var (exitCode, stdout, stderr) = await RunProcessWithProgressAsync(
                FfmpegLocator.GetFfmpegPath(),
                reencodeArgs,
                totalDuration,
                progress,
                cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
            {
                // Parse common ffmpeg errors and provide user-friendly Chinese messages
                var errorMessage = ParseFfmpegError(stderr);
                _logger.LogError("ffmpeg 合成失败。退出码: {ExitCode}, 错误: {Error}", exitCode, stderr);
                throw new InvalidOperationException(errorMessage);
            }

            if (!File.Exists(outputPath))
                throw new InvalidOperationException("合成完成但未找到输出文件。");

            progress?.Report(1);
            return outputPath;
        }
        finally
        {
            try { if (File.Exists(listFile)) File.Delete(listFile); } catch { }
        }
    }

    private async Task<double> GetTotalDurationAsync(IReadOnlyList<string> clipPaths, CancellationToken cancellationToken)
    {
        double totalSeconds = 0;
        foreach (var clipPath in clipPaths)
        {
            try
            {
                // Use ffprobe to get duration of each clip
                var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{clipPath}\"";
                var (exitCode, stdout, _) = await RunProcessCaptureAsync(
                    FfmpegLocator.GetFfprobePath(),
                    args,
                    cancellationToken).ConfigureAwait(false);

                if (exitCode == 0 && double.TryParse(stdout.Trim(), out var duration))
                {
                    totalSeconds += duration;
                    _logger.LogInformation("视频片段时长: {ClipPath} = {Duration:F2}秒", Path.GetFileName(clipPath), duration);
                }
                else
                {
                    _logger.LogWarning("无法获取视频时长: {ClipPath}, 退出码: {ExitCode}, 输出: {Output}",
                        clipPath, exitCode, stdout);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "无法获取视频时长: {ClipPath}", clipPath);
            }
        }

        _logger.LogInformation("所有视频片段总时长: {TotalDuration:F2}秒", totalSeconds);
        return totalSeconds;
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessWithProgressAsync(
        string fileName,
        string arguments,
        double totalDurationSeconds,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("启动FFmpeg进程，总时长: {TotalDuration:F2}秒", totalDurationSeconds);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stdout.AppendLine(e.Data);
        };

        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stderr.AppendLine(e.Data);

            // Log all ffmpeg output at Info level for debugging
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                // Don't spam with progress lines, but log everything else
                if (!e.Data.StartsWith("frame=") &&
                    !e.Data.StartsWith("fps=") &&
                    !e.Data.StartsWith("stream_") &&
                    !e.Data.StartsWith("bitrate=") &&
                    !e.Data.StartsWith("total_size=") &&
                    !e.Data.StartsWith("out_time_ms=") &&
                    !e.Data.StartsWith("dup_frames=") &&
                    !e.Data.StartsWith("drop_frames=") &&
                    !e.Data.StartsWith("speed=") &&
                    !e.Data.StartsWith("progress="))
                {
                    _logger.LogInformation("FFmpeg: {Output}", e.Data);
                }
            }

            // Parse progress from ffmpeg output
            // Format: "out_time_us=12345678"
            if (progress != null && totalDurationSeconds > 0 && e.Data.StartsWith("out_time_us="))
            {
                var timeStr = e.Data.Substring("out_time_us=".Length);
                if (long.TryParse(timeStr, out var timeMicroseconds))
                {
                    var currentSeconds = timeMicroseconds / 1_000_000.0;
                    var progressPercent = Math.Min(currentSeconds / totalDurationSeconds, 0.99); // Cap at 99% until complete

                    // Only log progress updates when percentage changes to avoid spam
                    var currentPercent = (int)(progressPercent * 100);
                    if (currentPercent % 5 == 0) // Log every 5%
                    {
                        _logger.LogInformation("进度更新: {Current:F1}s / {Total:F1}s = {Percent}%",
                            currentSeconds, totalDurationSeconds, currentPercent);
                    }
                    progress.Report(progressPercent);
                }
            }
        };

        if (!proc.Start())
            throw new InvalidOperationException($"无法启动进程: {fileName}");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        _logger.LogInformation("FFmpeg进程已启动，等待完成...");
        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("FFmpeg进程已完成，退出码: {ExitCode}", proc.ExitCode);
        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessCaptureAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stdout.AppendLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stderr.AppendLine(e.Data);
        };

        if (!proc.Start())
            throw new InvalidOperationException($"无法启动进程: {fileName}");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private string BuildReencodeArgs(string listFile, string outputPath, VideoExportSettings settings)
    {
        // Use concat filter instead of concat demuxer for better compatibility
        // This approach re-encodes everything, which is slower but more reliable
        // when videos have different properties (codecs, frame rates, etc.)

        // Read the list file to get video paths
        var videoInputs = new StringBuilder();
        var scaleFilters = new StringBuilder();
        var concatInputs = new StringBuilder();
        var lines = File.ReadAllLines(listFile);
        int inputCount = 0;

        // Determine target resolution
        var targetResolution = settings.Resolution ?? "1920x1080";

        foreach (var line in lines)
        {
            if (line.StartsWith("file '") && line.EndsWith("'"))
            {
                var path = line.Substring(6, line.Length - 7).Replace("'\\''", "'");
                videoInputs.Append($"-i \"{path}\" ");

                // Scale each video to target resolution and normalize SAR to 1:1 before concatenating
                // setsar=1 ensures all videos have the same Sample Aspect Ratio
                scaleFilters.Append($"[{inputCount}:v]scale={targetResolution},setsar=1[v{inputCount}];");

                // Add scaled video and audio to concat inputs
                concatInputs.Append($"[v{inputCount}][{inputCount}:a]");

                inputCount++;
            }
        }

        if (inputCount == 0)
        {
            throw new InvalidOperationException("没有找到有效的视频输入");
        }

        // Build filter_complex: scale each input, then concat
        var filterComplex = $"{scaleFilters}{concatInputs}concat=n={inputCount}:v=1:a=1[outv][outa]";

        // Build ffmpeg command with concat filter
        var args = $"-y -hide_banner -loglevel warning -progress pipe:2 " +
                   $"{videoInputs}" +
                   $"-filter_complex \"{filterComplex}\" " +
                   $"-map \"[outv]\" -map \"[outa]\" " +
                   $"-c:v libx264 -preset veryfast -crf 20 " +
                   $"-r {settings.Fps} " +
                   $"-c:a aac -movflags +faststart \"{outputPath}\"";

        _logger.LogInformation("重编码参数: {Args}", args);
        return args;
    }

    private string ParseFfmpegError(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
            return "视频合成失败，未知错误。";

        var lowerError = stderr.ToLowerInvariant();

        // Check for common error patterns
        if (lowerError.Contains("no such file") || lowerError.Contains("does not exist"))
            return "视频合成失败：找不到输入文件。请确保所有视频片段都已生成。";

        if (lowerError.Contains("permission denied") || lowerError.Contains("access denied"))
            return "视频合成失败：没有文件访问权限。请检查输出目录的写入权限。";

        if (lowerError.Contains("disk full") || lowerError.Contains("no space left"))
            return "视频合成失败：磁盘空间不足。请清理磁盘空间后重试。";

        if (lowerError.Contains("invalid") && lowerError.Contains("resolution"))
            return "视频合成失败：分辨率格式无效。请使用正确的格式（例如：1920x1080）。";

        if (lowerError.Contains("invalid") && lowerError.Contains("framerate"))
            return "视频合成失败：帧率设置无效。请输入有效的帧率数值。";

        if (lowerError.Contains("codec") && lowerError.Contains("not found"))
            return "视频合成失败：缺少视频编码器。请确保 ffmpeg 已正确安装。";

        if (lowerError.Contains("invalid data") || lowerError.Contains("corrupt"))
            return "视频合成失败：输入视频文件已损坏。请重新生成视频片段。";

        if (lowerError.Contains("out of memory") || lowerError.Contains("cannot allocate"))
            return "视频合成失败：内存不足。请关闭其他程序后重试。";

        if (lowerError.Contains("unknown encoder"))
            return "视频合成失败：不支持的编码格式。请使用 mp4 格式。";

        // Generic error with first line of stderr
        var firstLine = stderr.Split('\n', '\r').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        if (!string.IsNullOrWhiteSpace(firstLine))
            return $"视频合成失败：{firstLine.Trim()}";

        return "视频合成失败。请检查日志文件获取详细信息。";
    }
}
