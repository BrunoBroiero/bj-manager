using System.Text.Json;

namespace BJManager;

/// <summary>settings.json under %AppData%\BJManager: hand-picked game folders and what was installed.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BJManager");
    public static string FilePath => Path.Combine(Folder, "settings.json");

    /// <summary>Where the app kept its settings while it was called Broiero Installer.</summary>
    private static string LegacyFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BroieroInstaller", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath) && File.Exists(LegacyFilePath))
            {
                Directory.CreateDirectory(Folder);
                File.Move(LegacyFilePath, FilePath);
            }
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
        }
        catch
        {
            // A broken settings file starts over; nothing in it is irreplaceable.
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Folder);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
