using System.IO;
using System.Text.Json;

namespace CH552G_PadConfig_Win.Services;

/// <summary>
/// Application settings persistence (stores last profile path)
/// Saved to %AppData%\CH552G_PadConfig\settings.json
/// </summary>
public class AppSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CH552G_PadConfig"
    );

    private static readonly string SettingsFilePath = Path.Combine(
        SettingsDirectory,
        "settings.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string LastProfilePath { get; set; } = string.Empty;
    public bool AutoLoadLastProfile { get; set; } = true;
    public int WindowWidth { get; set; } = 900;
    public int WindowHeight { get; set; } = 700;

    /// <summary>
    /// Save settings to disk
    /// </summary>
    public bool Save()
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(SettingsDirectory);

            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Load settings from disk, or create default if not found
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                return settings ?? new AppSettings();
            }
        }
        catch
        {
            // Fall through to default
        }

        return new AppSettings();
    }
}
