using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using LibVLCSharp.Shared;

namespace Storyboard.Views;

internal static class VlcCoreInitializer
{
    private static int _initialized;

    public static void EnsureInitialized()
    {
        if (Volatile.Read(ref _initialized) == 1)
            return;

        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var systemVlcPaths = new[]
                {
                    "/Applications/VLC.app/Contents/MacOS/lib",
                    "/opt/homebrew/lib",
                    "/usr/local/lib",
                    "/opt/homebrew/Cellar/libvlc/3.0.21/lib"
                };

                foreach (var path in systemVlcPaths)
                {
                    if (IsMacVlcPath(path))
                    {
                        Core.Initialize(path);
                        Debug.WriteLine($"[LibVLC] Using system VLC: {path}");
                        MarkInitialized();
                        return;
                    }
                }

                throw new InvalidOperationException(
                    "VLC not found. Please run: brew install --cask vlc\n" +
                    $"Checked paths: {string.Join(", ", systemVlcPaths)}");
            }

            if (OperatingSystem.IsWindows())
            {
                var baseDir = AppContext.BaseDirectory;
                var arch = GetWindowsArchFolder();
                var candidates = new[]
                {
                    Path.Combine(baseDir, "libvlc", arch),
                    Path.Combine(baseDir, "libvlc"),
                    Path.Combine(baseDir, "runtimes", arch, "native")
                };

                foreach (var path in candidates)
                {
                    if (IsWindowsVlcPath(path))
                    {
                        Core.Initialize(path);
                        Debug.WriteLine($"[LibVLC] Using bundled VLC: {path}");
                        MarkInitialized();
                        return;
                    }
                }
            }

            Core.Initialize();
            Debug.WriteLine("[LibVLC] Initialized with default resolver");
            MarkInitialized();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("VLC", StringComparison.OrdinalIgnoreCase))
                throw;

            MarkInitialized();
            Debug.WriteLine("[LibVLC] Core already initialized");
        }
    }

    private static void MarkInitialized()
        => Interlocked.Exchange(ref _initialized, 1);

    private static bool IsMacVlcPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var libvlc = Path.Combine(path, "libvlc.dylib");
        var libvlccore = Path.Combine(path, "libvlccore.dylib");
        return File.Exists(libvlc) && File.Exists(libvlccore);
    }

    private static bool IsWindowsVlcPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var libvlc = Path.Combine(path, "libvlc.dll");
        var libvlccore = Path.Combine(path, "libvlccore.dll");
        return File.Exists(libvlc) && File.Exists(libvlccore);
    }

    private static string GetWindowsArchFolder()
        => RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.X86 => "win-x86",
            Architecture.Arm64 => "win-arm64",
            _ => "win-x64"
        };
}
