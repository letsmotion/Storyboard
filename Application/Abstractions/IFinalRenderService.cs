namespace Storyboard.Application.Abstractions;

public record VideoExportSettings(
    string Resolution = "1920x1080",
    int Fps = 30,
    string Format = "mp4");

public interface IFinalRenderService
{
    Task<string> RenderAsync(IReadOnlyList<string> clipPaths, CancellationToken cancellationToken, IProgress<double>? progress = null, VideoExportSettings? settings = null);
}
