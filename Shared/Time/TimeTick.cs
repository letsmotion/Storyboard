using System;

namespace Storyboard.Shared.Time;

/// <summary>
/// Unified timebase helper. Internal unit = millisecond tick.
/// </summary>
public static class TimeTick
{
    public const long TicksPerSecond = 1_000;
    public const long MicrosecondsPerTick = 1_000;
    public const long TicksPerSecondLong = TicksPerSecond;

    public static long FromSeconds(double seconds)
    {
        if (seconds <= 0)
            return 0;
        return (long)Math.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero);
    }

    public static double ToSeconds(long ticks)
    {
        if (ticks <= 0)
            return 0;
        return ticks / (double)TicksPerSecond;
    }

    public static long FromMilliseconds(double milliseconds)
    {
        if (milliseconds <= 0)
            return 0;
        return (long)Math.Round(milliseconds, MidpointRounding.AwayFromZero);
    }

    public static double ToMilliseconds(long ticks)
    {
        if (ticks <= 0)
            return 0;
        return ticks;
    }

    public static long FromMicroseconds(long microseconds)
    {
        if (microseconds <= 0)
            return 0;
        return microseconds / MicrosecondsPerTick;
    }

    public static long ToMicroseconds(long ticks)
    {
        if (ticks <= 0)
            return 0;
        return ticks * MicrosecondsPerTick;
    }

    public static long Clamp(long ticks, long minTicks, long maxTicks)
    {
        if (ticks < minTicks) return minTicks;
        if (ticks > maxTicks) return maxTicks;
        return ticks;
    }
}
