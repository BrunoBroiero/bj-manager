using System.Text.Json.Serialization;

namespace BJManager;

/// <summary>The mod.json a mod repository carries at its root. Everything the installer needs to
/// know about a mod lives here, so publishing a new mod means publishing a new repo with one.</summary>
public sealed class ModManifest
{
    public int Schema { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Game { get; set; } = "";
    public int SteamAppId { get; set; }
    /// <summary>Folder name under steamapps\common, the fallback when the app manifest is missing.</summary>
    public string InstallDir { get; set; } = "";
    /// <summary>Executable at the game's root; proves a hand-picked folder is the right one.</summary>
    public string GameExe { get; set; } = "";
    /// <summary>Where the release zip is extracted, relative to the game folder ("" for the root).</summary>
    public string Target { get; set; } = "";
    /// <summary>Wildcard the release asset's name must match, e.g. "SamShoReader-*.zip".</summary>
    public string Asset { get; set; } = "*.zip";
    /// <summary>Files and folders (relative to Target) that make up a hand-made install.</summary>
    public List<string> Uninstall { get; set; } = new();
    /// <summary>Short description by language code ("en", "es", ...).</summary>
    public Dictionary<string, string> Description { get; set; } = new();

    [JsonIgnore] public string Repo { get; set; } = "";
    [JsonIgnore] public string RepoUrl { get; set; } = "";

    public string DescriptionFor(string lang) =>
        Description.TryGetValue(lang, out var d) ? d :
        Description.TryGetValue("en", out var e) ? e :
        Description.Values.FirstOrDefault() ?? "";
}

public sealed class ReleaseInfo
{
    public string Tag { get; init; } = "";
    public string Version { get; init; } = "";
    public string AssetName { get; init; } = "";
    public string AssetUrl { get; init; } = "";
    public long AssetSize { get; init; }
    public string HtmlUrl { get; init; } = "";
}

/// <summary>What the installer wrote and where, so an update replaces exactly that and an uninstall
/// removes exactly that.</summary>
public sealed class InstallRecord
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public string GamePath { get; set; } = "";
    public List<string> Files { get; set; } = new();
    public DateTimeOffset InstalledAt { get; set; }
}

public sealed class AppSettings
{
    /// <summary>Game folders the player chose by hand, by mod id.</summary>
    public Dictionary<string, string> GamePaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, InstallRecord> Installed { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public enum InstallState
{
    GameNotFound,
    NoRelease,
    NotInstalled,
    Installed,
    UpdateAvailable,
    InstalledByHand,
}

public sealed class ModEntry
{
    public required ModManifest Manifest { get; init; }
    public ReleaseInfo? Release { get; set; }
    public string? GamePath { get; set; }
    public InstallRecord? Record { get; set; }
    public InstallState State { get; set; }

    public string TargetPath => GamePath is null ? "" : Path.GetFullPath(Path.Combine(GamePath, Manifest.Target));
}

public static class Versions
{
    public static string Normalize(string tagOrVersion) => tagOrVersion.TrimStart('v', 'V').Trim();

    /// <summary>Positive when a is newer than b.</summary>
    public static int Compare(string a, string b)
    {
        a = Normalize(a);
        b = Normalize(b);
        if (Version.TryParse(a, out var va) && Version.TryParse(b, out var vb)) return va.CompareTo(vb);
        return string.CompareOrdinal(a, b);
    }
}
