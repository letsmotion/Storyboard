using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Storyboard.Infrastructure.Media;

internal static class FfmpegLocator
{
    public static string GetFfmpegPath()
        => GetToolPath(GetExecutableName("ffmpeg")) ?? "ffmpeg";

    public static string GetFfprobePath()
        => GetToolPath(GetExecutableName("ffprobe")) ?? "ffprobe";

    private static string? GetToolPath(string exeName)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var os = GetOsName();
        var arch = GetArchName();

        var candidates = new List<string>
        {
            Path.Combine(baseDir, "Tools", "ffmpeg", $"{os}-{arch}", exeName),
            Path.Combine(baseDir, "ffmpeg", $"{os}-{arch}", exeName),
            Path.Combine(baseDir, exeName),
            Path.Combine(baseDir, "ffmpeg", exeName),
            Path.Combine(baseDir, "Tools", "ffmpeg", exeName),
        };

        candidates.AddRange(GetPathCandidates(exeName));
        candidates.AddRange(GetOsSpecificCandidates(exeName));

        foreach (var p in candidates)
        {
            try
            {
                if (File.Exists(p))
                    return p;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static IEnumerable<string> GetPathCandidates(string exeName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            yield break;

        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            yield return Path.Combine(dir.Trim(), exeName);
        }
    }

    private static IEnumerable<string> GetOsSpecificCandidates(string exeName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return Path.Combine("/opt/homebrew/bin", exeName);
            yield return Path.Combine("/usr/local/bin", exeName);
            yield return Path.Combine("/usr/bin", exeName);
            yield return Path.Combine("/opt/local/bin", exeName);
            yield break;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            yield return Path.Combine("/usr/local/bin", exeName);
            yield return Path.Combine("/usr/bin", exeName);
            yield return Path.Combine("/snap/bin", exeName);
        }
    }

    private static string GetExecutableName(string baseName)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{baseName}.exe" : baseName;

    private static string GetOsName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "win";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "osx";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";

        return "unknown";
    }

    private static string GetArchName()
        => RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };
}
