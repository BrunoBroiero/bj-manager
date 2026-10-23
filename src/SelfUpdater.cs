using System.Diagnostics;
using System.Reflection;

namespace BJManager;

/// <summary>Keeps the installer itself current from its own GitHub releases.</summary>
public static class SelfUpdater
{
    public const string Repo = "BrunoBroiero/bj-manager";
    public const string AssetName = "BJManager.exe";

    public static string CurrentVersion
    {
        get
        {
            var info = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(info))
            {
                var plus = info.IndexOf('+');
                return plus > 0 ? info[..plus] : info;
            }
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    /// <summary>The newer release, or null when this is the latest (or nothing is published).</summary>
    public static async Task<ReleaseInfo?> CheckAsync(GitHubCatalog catalog, CancellationToken ct)
    {
        var latest = await catalog.LatestReleaseAsync(Repo, AssetName, ct);
        if (latest is null) return null;
        return Versions.Compare(latest.Version, CurrentVersion) > 0 ? latest : null;
    }

    /// <summary>Downloads the new exe and hands over to a script that swaps it in once this process
    /// has exited, then starts the new one. Returns only on failure; on success the app exits.</summary>
    public static async Task ApplyAsync(GitHubCatalog catalog, ReleaseInfo release, IProgress<(long done, long total)> progress, CancellationToken ct)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("cannot locate the running exe");
        var tempDir = Path.Combine(Path.GetTempPath(), "BJManager");
        Directory.CreateDirectory(tempDir);
        var newExe = Path.Combine(tempDir, $"BJManager-{release.Version}.exe");
        await catalog.DownloadAsync(release.AssetUrl, newExe, progress, ct);

        var script = Path.Combine(tempDir, "update.cmd");
        File.WriteAllText(script, string.Join("\r\n", new[]
        {
            "@echo off",
            "set n=0",
            ":retry",
            "set /a n+=1",
            "timeout /t 1 /nobreak >nul",
            $"copy /y \"{newExe}\" \"{exe}\" >nul 2>&1",
            "if errorlevel 1 (",
            "  if %n% lss 30 goto retry",
            "  exit /b 1",
            ")",
            $"del \"{newExe}\" >nul 2>&1",
            $"start \"\" \"{exe}\" --updated",
            "del \"%~f0\"",
            "",
        }));

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = tempDir,
        });
        Application.Exit();
    }
}
