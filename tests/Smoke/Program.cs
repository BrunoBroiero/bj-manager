// Smoke test, no window: discover the mods, find the games, install the first release that has
// one into a fake game folder, then uninstall it.  Usage: dotnet run -- <fake game folder>
using BroieroInstaller;

var fakeGame = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "BroieroInstaller-smoke");
using var http = GitHubCatalog.NewHttpClient();
var catalog = new GitHubCatalog(http);

Console.WriteLine($"installer version: {SelfUpdater.CurrentVersion}");
Console.WriteLine($"steam root: {SteamLibrary.FindSteamRoot()}");
foreach (var lib in SteamLibrary.Libraries()) Console.WriteLine($"library: {lib}");

var mods = await catalog.DiscoverAsync(CancellationToken.None);
Console.WriteLine($"{mods.Count} mods");
ModEntry? toInstall = null;
foreach (var m in mods)
{
    var rel = await catalog.LatestReleaseAsync(m, CancellationToken.None);
    var game = SteamLibrary.FindGame(m);
    Console.WriteLine($"- {m.Id} ({m.Repo}) game={game ?? "(not found)"} release={(rel is null ? "none" : $"{rel.Version} {rel.AssetName} {rel.AssetSize}B")}");
    if (rel is not null && toInstall is null) toInstall = new ModEntry { Manifest = m, Release = rel };
}
if (toInstall is null) { Console.WriteLine("nothing to install"); return 1; }

Directory.CreateDirectory(fakeGame);
File.WriteAllText(Path.Combine(fakeGame, toInstall.Manifest.GameExe), "fake");
toInstall.GamePath = fakeGame;
Console.WriteLine($"installing {toInstall.Manifest.Id} into {toInstall.TargetPath}");
var record = await ModInstaller.InstallAsync(catalog, toInstall,
    new Progress<(long done, long total)>(p => { if (p.done == p.total) Console.WriteLine($"  downloaded {p.done} bytes"); }),
    new Progress<string>(s => Console.WriteLine($"  {s}")), CancellationToken.None);
Console.WriteLine($"  {record.Files.Count} files written, version {record.Version}");
toInstall.Record = record;
Console.WriteLine($"  record holds: {ModInstaller.RecordHolds(record, toInstall)}  by hand: {ModInstaller.LooksInstalledByHand(toInstall)}");
foreach (var f in record.Files.Take(5)) Console.WriteLine($"    {f}");

ModInstaller.Uninstall(toInstall);
var left = Directory.EnumerateFileSystemEntries(fakeGame, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(fakeGame, p)).ToList();
Console.WriteLine($"after uninstall, left in fake game folder: {string.Join(", ", left)}");
Console.WriteLine($"  by hand now: {ModInstaller.LooksInstalledByHand(toInstall)}");
var selfUpdate = await SelfUpdater.CheckAsync(catalog, CancellationToken.None);
Console.WriteLine($"self-update available: {(selfUpdate is null ? "no" : selfUpdate.Version)}");
return left.Count == 1 ? 0 : 2;
