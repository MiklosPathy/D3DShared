using System.Text.Json;

namespace D3DShared;

/// <summary>
/// Base settings class with adapter selection
/// </summary>
public class BaseSettings
{
    public string? SelectedAdapterName { get; set; }

    /// <summary>
    /// Load settings from a JSON file
    /// </summary>
    public static T Load<T>(string filePath) where T : BaseSettings, new()
    {
        try
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<T>(json) ?? new T();
            }
        }
        catch { }
        return new T();
    }

    /// <summary>
    /// Save settings to a JSON file
    /// </summary>
    public static void Save<T>(T settings, string filePath) where T : BaseSettings
    {
        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch { }
    }

    /// <summary>
    /// Get the settings file path for a given app name (in LocalApplicationData)
    /// </summary>
    public static string GetSettingsPath(string appName)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appName,
            "settings.json");
    }

    /// <summary>
    /// Get the settings file path next to the executable
    /// </summary>
    public static string GetLocalSettingsPath(string fileName = "settings.json")
    {
        return Path.Combine(AppContext.BaseDirectory, fileName);
    }
}
