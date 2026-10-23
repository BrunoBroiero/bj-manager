using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BJManager;

/// <summary>Finds where Steam keeps its games, through the registry and libraryfolders.vdf.</summary>
public static class SteamLibrary
{
    public static string? FindSteamRoot()
    {
        foreach (var (hive, key, value) in new[]
        {
            (Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            (Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
        })
        {
            try
            {
                using var k = hive.OpenSubKey(key);
                if (k?.GetValue(value) is string path && Directory.Exists(path))
                    return Path.GetFullPath(path);
            }
            catch
            {
                // A missing or unreadable key is just a miss.
            }
        }
        return null;
    }

    /// <summary>Every Steam library root, the main one first.</summary>
    public static IReadOnlyList<string> Libraries()
    {
        var result = new List<string>();
        var root = FindSteamRoot();
        if (root is null) return result;
        result.Add(root);

        var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            try
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                {
                    var path = m.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(path) && !result.Contains(path, StringComparer.OrdinalIgnoreCase))
                        result.Add(Path.GetFullPath(path));
                }
            }
            catch
            {
                // An unreadable vdf leaves us with the main library only.
            }
        }
        return result;
    }

    /// <summary>The game's folder, or null when it is not installed in any library.</summary>
    public static string? FindGame(ModManifest mod)
    {
        foreach (var lib in Libraries())
        {
            var steamapps = Path.Combine(lib, "steamapps");
            var manifest = Path.Combine(steamapps, $"appmanifest_{mod.SteamAppId}.acf");
            if (File.Exists(manifest))
            {
                try
                {
                    var m = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                    if (m.Success)
                    {
                        var dir = Path.Combine(steamapps, "common", m.Groups[1].Value);
                        if (LooksLikeGame(dir, mod)) return dir;
                    }
                }
                catch
                {
                    // Fall through to the folder-name guess.
                }
            }
            if (!string.IsNullOrEmpty(mod.InstallDir))
            {
                var guess = Path.Combine(steamapps, "common", mod.InstallDir);
                if (LooksLikeGame(guess, mod)) return guess;
            }
        }
        return null;
    }

    public static bool LooksLikeGame(string folder, ModManifest mod) =>
        Directory.Exists(folder) &&
        (string.IsNullOrEmpty(mod.GameExe) || File.Exists(Path.Combine(folder, mod.GameExe)));
}
