using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BJManager;

/// <summary>Finds the mods: every public repository of the owner that carries a mod.json at its
/// root, and the latest release of each.</summary>
public sealed class GitHubCatalog
{
    public const string Owner = "BrunoBroiero";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly HttpClient _http;

    public GitHubCatalog(HttpClient http) => _http = http;

    public static HttpClient NewHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BJManager", SelfUpdater.CurrentVersion));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return http;
    }

    public async Task<List<ModManifest>> DiscoverAsync(CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"https://api.github.com/users/{Owner}/repos?per_page=100&type=owner", ct);
        var candidates = new List<(string name, string branch)>();
        foreach (var repo in doc.RootElement.EnumerateArray())
        {
            if (repo.TryGetProperty("fork", out var fork) && fork.GetBoolean()) continue;
            if (repo.TryGetProperty("archived", out var archived) && archived.GetBoolean()) continue;
            var name = repo.GetProperty("name").GetString()!;
            var branch = repo.TryGetProperty("default_branch", out var b) ? b.GetString() ?? "HEAD" : "HEAD";
            candidates.Add((name, branch));
        }

        var tasks = candidates.Select(c => FetchManifestAsync(c.name, c.branch, ct)).ToArray();
        var manifests = await Task.WhenAll(tasks);
        return manifests.Where(m => m is not null).Select(m => m!).OrderBy(m => m.Game, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private async Task<ModManifest?> FetchManifestAsync(string repo, string branch, CancellationToken ct)
    {
        // raw.githubusercontent.com does not count against the API rate limit.
        var url = $"https://raw.githubusercontent.com/{Owner}/{repo}/{branch}/mod.json";
        using var response = await _http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        try
        {
            var manifest = JsonSerializer.Deserialize<ModManifest>(await response.Content.ReadAsStringAsync(ct), JsonOptions);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id) || manifest.SteamAppId <= 0) return null;
            manifest.Repo = $"{Owner}/{repo}";
            manifest.RepoUrl = $"https://github.com/{Owner}/{repo}";
            return manifest;
        }
        catch (JsonException)
        {
            // A malformed mod.json hides that mod rather than breaking the list.
            return null;
        }
    }

    /// <summary>The latest release whose assets include one matching the manifest's pattern, or null.</summary>
    public async Task<ReleaseInfo?> LatestReleaseAsync(ModManifest mod, CancellationToken ct) =>
        await LatestReleaseAsync(mod.Repo, mod.Asset, ct);

    public async Task<ReleaseInfo?> LatestReleaseAsync(string repo, string assetPattern, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"https://api.github.com/repos/{repo}/releases/latest", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await ThrowIfRateLimitedAsync(response, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var pattern = "^" + Regex.Escape(assetPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (!Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase)) continue;
            return new ReleaseInfo
            {
                Tag = tag,
                Version = Versions.Normalize(tag),
                AssetName = name,
                AssetUrl = asset.GetProperty("browser_download_url").GetString() ?? "",
                AssetSize = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                HtmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "",
            };
        }
        return null;
    }

    /// <summary>Downloads a file to disk, reporting (done, total) bytes.</summary>
    public async Task DownloadAsync(string url, string destination, IProgress<(long done, long total)>? progress, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? -1;
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
        var buffer = new byte[1 << 16];
        long done = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            done += read;
            progress?.Report((done, total));
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct);
        await ThrowIfRateLimitedAsync(response, ct);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
    }

    private static async Task ThrowIfRateLimitedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests &&
            response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.FirstOrDefault() == "0")
        {
            var reset = response.Headers.TryGetValues("X-RateLimit-Reset", out var r) && long.TryParse(r.FirstOrDefault(), out var unix)
                ? DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("t")
                : "?";
            _ = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(L.Lang == "es"
                ? $"GitHub limitó las consultas desde esta conexión hasta las {reset}."
                : $"GitHub is rate-limiting this connection until {reset}.");
        }
    }
}
