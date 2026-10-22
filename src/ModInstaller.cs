using System.Diagnostics;
using System.IO.Compression;

namespace BroieroInstaller;

/// <summary>Puts a mod's release zip into the game folder, and takes it out again.</summary>
public static class ModInstaller
{
    public static bool IsGameRunning(ModManifest mod)
    {
        if (string.IsNullOrEmpty(mod.GameExe)) return false;
        var name = Path.GetFileNameWithoutExtension(mod.GameExe);
        var running = Process.GetProcessesByName(name);
        foreach (var p in running) p.Dispose();
        return running.Length > 0;
    }

    /// <summary>True when the files of a hand-made install are present.</summary>
    public static bool LooksInstalledByHand(ModEntry entry)
    {
        if (entry.GamePath is null) return false;
        var target = entry.TargetPath;
        return entry.Manifest.Uninstall.Any(rel => Path.Exists(Path.Combine(target, rel)));
    }

    /// <summary>True when the files the record says were installed are still there.</summary>
    public static bool RecordHolds(InstallRecord record, ModEntry entry)
    {
        if (entry.GamePath is null) return false;
        if (!string.Equals(Path.GetFullPath(record.GamePath), Path.GetFullPath(entry.GamePath), StringComparison.OrdinalIgnoreCase)) return false;
        if (record.Files.Count == 0) return false;
        var target = entry.TargetPath;
        // Not every file: the player may have deleted a README. The first few, and anything that is a dll.
        var key = record.Files.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Take(3).ToList();
        if (key.Count == 0) key = record.Files.Take(1).ToList();
        return key.All(f => File.Exists(Path.Combine(target, f)));
    }

    public static async Task<InstallRecord> InstallAsync(GitHubCatalog catalog, ModEntry entry,
        IProgress<(long done, long total)> download, IProgress<string> status, CancellationToken ct)
    {
        var release = entry.Release ?? throw new InvalidOperationException("no release");
        var gamePath = entry.GamePath ?? throw new InvalidOperationException("no game path");

        var tempDir = Path.Combine(Path.GetTempPath(), "BroieroInstaller", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, release.AssetName);
        try
        {
            await catalog.DownloadAsync(release.AssetUrl, zipPath, download, ct);
            status.Report(L.T("extracting"));

            var target = entry.TargetPath;
            var files = await Task.Run(() => Extract(zipPath, target), ct);
            return new InstallRecord
            {
                Id = entry.Manifest.Id,
                Version = release.Version,
                GamePath = gamePath,
                Files = files,
                InstalledAt = DateTimeOffset.Now,
            };
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* temp */ }
        }
    }

    private static List<string> Extract(string zipPath, string target)
    {
        Directory.CreateDirectory(target);
        var root = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var written = new List<string>();
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // a folder entry
            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(root, relative));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"the zip tries to write outside the game folder: {entry.FullName}");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            written.Add(relative);
        }
        return written;
    }

    public static void Uninstall(ModEntry entry)
    {
        var target = entry.TargetPath;
        if (entry.Record is { Files.Count: > 0 } record)
        {
            foreach (var rel in record.Files)
            {
                var path = Path.Combine(target, rel);
                if (File.Exists(path)) File.Delete(path);
            }
            // Folders the zip created, deepest first, only when nothing else is left in them.
            var dirs = record.Files
                .Select(f => Path.GetDirectoryName(f))
                .Where(d => !string.IsNullOrEmpty(d))
                .SelectMany(Ancestors)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(d => d.Length);
            foreach (var rel in dirs)
            {
                var dir = Path.Combine(target, rel);
                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
        }
        // Whatever a hand-made install left, by the manifest's own list.
        foreach (var rel in entry.Manifest.Uninstall)
        {
            var path = Path.Combine(target, rel);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
        }
    }

    private static IEnumerable<string> Ancestors(string? relativeDir)
    {
        while (!string.IsNullOrEmpty(relativeDir))
        {
            yield return relativeDir;
            relativeDir = Path.GetDirectoryName(relativeDir);
        }
    }
}
