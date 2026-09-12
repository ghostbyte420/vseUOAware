using System.Text.Json;

namespace vseUOAware.Services;

/// <summary>
/// Remembers the last UO folder path you told us about, so we don't
/// have to ask you to type it every single time. Saved as a tiny JSON
/// file on your computer, in your user's "app data" folder.
/// </summary>
internal static class DirectoryService
{
    // This is WHERE we save the remembered folder path on disk.
    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "vseUOAware",
        "uo-path.json");

    private sealed class SavedConfig
    {
        public string? UoDirectory { get; set; }
    }

    /// <summary>Reads the saved folder path, or null if we've never saved one.</summary>
    public static string? LoadSavedDirectory()
    {
        if (!File.Exists(ConfigFilePath))
            return null;

        var json = File.ReadAllText(ConfigFilePath);
        var config = JsonSerializer.Deserialize<SavedConfig>(json);
        return config?.UoDirectory;
    }

    /// <summary>Saves a new folder path so we remember it next time.</summary>
    public static void SaveDirectory(string directoryPath)
    {
        var folder = Path.GetDirectoryName(ConfigFilePath)!;
        Directory.CreateDirectory(folder); // Make sure the folder to save INTO exists.

        var config = new SavedConfig { UoDirectory = directoryPath };
        var json = JsonSerializer.Serialize(config);
        File.WriteAllText(ConfigFilePath, json);
    }
}