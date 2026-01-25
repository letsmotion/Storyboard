namespace Storyboard.Models;

public sealed record AiShotSegment(
    double StartTimeSeconds,
    double EndTimeSeconds,
    AiShotDescription Shot);
