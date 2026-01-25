using Microsoft.Extensions.Logging;
using SkiaSharp;
using Storyboard.Application.Abstractions;
using Storyboard.Application.Services;
using Storyboard.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Storyboard.Infrastructure.Services;

public sealed class SmartStoryboardService : ISmartStoryboardService
{
    private const int GridColumns = 4;
    private const int GridRows = 4;
    private const int MaxGridFrames = GridColumns * GridRows;

    private readonly IFrameExtractionService _frameExtractionService;
    private readonly IVideoMetadataService _videoMetadataService;
    private readonly IAiShotService _aiShotService;
    private readonly IVideoAnalysisService _fallbackAnalysisService;
    private readonly StoragePathService _storagePathService;
    private readonly ILogger<SmartStoryboardService> _logger;

    public SmartStoryboardService(
        IFrameExtractionService frameExtractionService,
        IVideoMetadataService videoMetadataService,
        IAiShotService aiShotService,
        IVideoAnalysisService fallbackAnalysisService,
        StoragePathService storagePathService,
        ILogger<SmartStoryboardService> logger)
    {
        _frameExtractionService = frameExtractionService;
        _videoMetadataService = videoMetadataService;
        _aiShotService = aiShotService;
        _fallbackAnalysisService = fallbackAnalysisService;
        _storagePathService = storagePathService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ShotItem>> AnalyzeAsync(
        string videoPath,
        string projectId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
            throw new ArgumentException("视频路径为空", nameof(videoPath));

        if (!File.Exists(videoPath))
            throw new FileNotFoundException("Video file not found.", videoPath);

        var metadata = await _videoMetadataService.GetMetadataAsync(videoPath, cancellationToken).ConfigureAwait(false);

        FrameExtractionResult extraction;
        try
        {
            var request = new FrameExtractionRequest(
                videoPath,
                projectId,
                FrameExtractionMode.Keyframe,
                FrameCount: 12,
                TimeIntervalMs: 800,
                DetectionSensitivity: 0.35);
            extraction = await _frameExtractionService.ExtractAsync(request, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "关键帧抽取失败，尝试均匀抽帧");
            var fallbackRequest = new FrameExtractionRequest(
                videoPath,
                projectId,
                FrameExtractionMode.FixedCount,
                FrameCount: MaxGridFrames,
                TimeIntervalMs: 800,
                DetectionSensitivity: 0.35);
            extraction = await _frameExtractionService.ExtractAsync(fallbackRequest, null, cancellationToken).ConfigureAwait(false);
        }

        var frames = extraction.Frames
            .Where(f => File.Exists(f.FilePath))
            .OrderBy(f => f.TimestampSeconds)
            .ToList();

        if (frames.Count == 0)
        {
            _logger.LogWarning("No keyframes extracted, falling back to fast analysis.");
            var fallback = await _fallbackAnalysisService.AnalyzeVideoAsync(videoPath).ConfigureAwait(false);
            return fallback.Shots;
        }

        var selectedFrames = SelectFrames(frames, MaxGridFrames);
        var mappingText = BuildMappingText(selectedFrames);
        var contactSheetPath = BuildContactSheet(selectedFrames, projectId);

        try
        {
            var segments = await _aiShotService.AnalyzeStoryboardFromContactSheetAsync(
                contactSheetPath,
                mappingText,
                metadata,
                cancellationToken).ConfigureAwait(false);

            var shots = BuildShotsFromSegments(segments, metadata.DurationSeconds);
            if (shots.Count == 0)
            {
                _logger.LogWarning("AI analysis returned no shots; falling back to fast analysis.");
                var fallback = await _fallbackAnalysisService.AnalyzeVideoAsync(videoPath).ConfigureAwait(false);
                return fallback.Shots;
            }

            return shots;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI storyboard analysis failed; falling back to fast analysis.");
            var fallback = await _fallbackAnalysisService.AnalyzeVideoAsync(videoPath).ConfigureAwait(false);
            return fallback.Shots;
        }
    }

    private static List<ExtractedFrame> SelectFrames(List<ExtractedFrame> frames, int targetCount)
    {
        if (frames.Count <= targetCount)
            return frames;

        var selected = new List<ExtractedFrame>(targetCount);
        for (var i = 0; i < targetCount; i++)
        {
            var index = (int)Math.Round(i * (frames.Count - 1) / (double)(targetCount - 1));
            index = Math.Clamp(index, 0, frames.Count - 1);
            selected.Add(frames[index]);
        }
        return selected;
    }

    private static string BuildMappingText(List<ExtractedFrame> frames)
    {
        var lines = new List<string>
        {
            "Grid is 4x4, indexed left-to-right, top-to-bottom as 1-16."
        };

        for (var i = 0; i < frames.Count && i < MaxGridFrames; i++)
        {
            lines.Add($"{i + 1} = {frames[i].TimestampSeconds.ToString("0.###", CultureInfo.InvariantCulture)}s");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildContactSheet(List<ExtractedFrame> frames, string projectId)
    {
        var tileWidth = 320;
        var tileHeight = 180;
        var width = tileWidth * GridColumns;
        var height = tileHeight * GridRows;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);

        for (var i = 0; i < GridColumns * GridRows; i++)
        {
            var col = i % GridColumns;
            var row = i / GridColumns;
            var x = col * tileWidth;
            var y = row * tileHeight;
            var cellRect = new SKRect(x, y, x + tileWidth, y + tileHeight);

            if (i < frames.Count)
            {
                DrawFrame(canvas, frames[i].FilePath, cellRect);
            }

            DrawIndexBadge(canvas, i + 1, x + 6, y + 6);
        }

        var outputDir = _storagePathService.GetFramesOutputDirectory(projectId);
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"ai_contact_sheet_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
        return outputPath;
    }

    private static void DrawFrame(SKCanvas canvas, string path, SKRect dest)
    {
        try
        {
            using var frame = SKBitmap.Decode(path);
            if (frame == null)
                return;

            var src = ComputeCoverRect(frame.Width, frame.Height, dest.Width, dest.Height);
            canvas.DrawBitmap(frame, src, dest);
        }
        catch
        {
            // ignore bad frames
        }
    }

    private static SKRect ComputeCoverRect(int srcWidth, int srcHeight, float destWidth, float destHeight)
    {
        var scale = Math.Max(destWidth / srcWidth, destHeight / srcHeight);
        var cropWidth = destWidth / scale;
        var cropHeight = destHeight / scale;
        var x = (srcWidth - cropWidth) / 2f;
        var y = (srcHeight - cropHeight) / 2f;
        return new SKRect(x, y, x + cropWidth, y + cropHeight);
    }

    private static void DrawIndexBadge(SKCanvas canvas, int index, float x, float y)
    {
        using var bgPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 180),
            IsAntialias = true
        };
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 18,
            IsAntialias = true
        };

        var text = index.ToString(CultureInfo.InvariantCulture);
        var bounds = new SKRect();
        textPaint.MeasureText(text, ref bounds);
        var padding = 6f;
        var rect = new SKRect(x, y, x + bounds.Width + padding * 2, y + bounds.Height + padding * 2);
        canvas.DrawRoundRect(rect, 4, 4, bgPaint);
        canvas.DrawText(text, x + padding, y + bounds.Height + padding, textPaint);
    }

    private static IReadOnlyList<ShotItem> BuildShotsFromSegments(
        IReadOnlyList<AiShotSegment> segments,
        double durationSeconds)
    {
        var list = new List<ShotItem>();
        var ordered = segments
            .Where(s => s.EndTimeSeconds > s.StartTimeSeconds)
            .OrderBy(s => s.StartTimeSeconds)
            .ToList();

        var previousEnd = 0.0;
        var index = 1;

        foreach (var segment in ordered)
        {
            var start = Math.Clamp(segment.StartTimeSeconds, 0, Math.Max(0, durationSeconds));
            var end = Math.Clamp(segment.EndTimeSeconds, 0, Math.Max(0, durationSeconds));

            if (start < previousEnd)
                start = previousEnd;

            if (end <= start)
                end = Math.Min(durationSeconds, start + Math.Max(0.5, segment.Shot.DurationSeconds ?? 0.5));

            var duration = Math.Max(0.5, end - start);

            var shot = new ShotItem(index++)
            {
                StartTime = start,
                EndTime = end,
                Duration = duration
            };
            shot.ApplyAiAnalysisResult(segment.Shot);

            list.Add(shot);
            previousEnd = end;
        }

        return list;
    }
}
