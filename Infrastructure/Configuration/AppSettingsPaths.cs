using System;
using System.IO;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// Centralized locations for appsettings.json across platforms.
/// </summary>
public static class AppSettingsPaths
{
    private const string AppFolderName = "Storyboard";
    private const string SettingsFileName = "appsettings.json";
    private const string DefaultSettingsFileName = "appsettings.default.json";

    public static string UserSettingsDirectory
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appDataPath, AppFolderName);
        }
    }

    public static string UserSettingsFilePath => Path.Combine(UserSettingsDirectory, SettingsFileName);

    public static string BundledSettingsFilePath => Path.Combine(AppContext.BaseDirectory, SettingsFileName);

    public static string DefaultSettingsFilePath => Path.Combine(AppContext.BaseDirectory, DefaultSettingsFileName);

    public static string EnsureUserSettingsFile()
    {
        Directory.CreateDirectory(UserSettingsDirectory);

        if (!File.Exists(UserSettingsFilePath))
        {
            if (File.Exists(BundledSettingsFilePath))
            {
                File.Copy(BundledSettingsFilePath, UserSettingsFilePath, overwrite: false);
            }
        }

        return UserSettingsFilePath;
    }
}
