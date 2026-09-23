using System.Collections.Concurrent;
using System.Text.Json;
using MyApp.ServiceModel;
using ServiceStack;

namespace MyApp.ServiceInterface;

/// <summary>Keeps the last fully validated GitHub response for each repository including across server restarts when a cache directory is configured.</summary>
public sealed class GitHubDownloadsCache
{
    public static GitHubDownloadsCache Shared { get; } = new(Path.Combine("App_Data", "github-downloads"));
    private readonly string? directory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.OrdinalIgnoreCase);

    public GitHubDownloadsCache(string? directory = null) => this.directory = directory;
    private readonly ConcurrentDictionary<string, string> lastSuccessful = new(StringComparer.OrdinalIgnoreCase);

    public async Task<GitHubDownloadsResponse> Get(string repo, Func<Task<string>> fetch)
    {
        var gate = gates.GetOrAdd(repo, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try {
            return await FetchOrFallback(repo, fetch);
        } finally { gate.Release(); }
    }

    private string CachePath(string repo) => Path.Combine(directory!,
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(repo.ToLowerInvariant()))) + ".json");

    private async Task<GitHubDownloadsResponse> FetchOrFallback(string repo, Func<Task<string>> fetch)
    {
        try {
            var body = await fetch();
            var result = Parse(repo, body);
            lastSuccessful[repo] = body;
            if (directory != null) {
                var path = CachePath(repo);
                try {
                    Directory.CreateDirectory(directory);
                    await File.WriteAllTextAsync(path + ".tmp", body);
                    File.Move(path + ".tmp", path, overwrite: true);
                } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
                    // A read-only disk must not prevent serving a valid live response or memory fallback.
                }
            }
            return result;
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException
            or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) {
            if (lastSuccessful.TryGetValue(repo, out var body)) return Parse(repo, body);
            if (directory != null) {
                try {
                    body = await File.ReadAllTextAsync(CachePath(repo));
                    var result = Parse(repo, body);
                    lastSuccessful[repo] = body;
                    return result;
                } catch (Exception diskError) when (diskError is IOException or UnauthorizedAccessException
                    or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { }
            }
            throw HttpError.BadRequest("GitHub releases are unavailable and no cached downloads exist yet. Check repository visibility and API rate limits.");
        }
    }

    private static GitHubDownloadsResponse Parse(string repo, string body)
    {
        var result = new GitHubDownloadsResponse { Repository = repo };
        using var json = JsonDocument.Parse(body);
        foreach (var r in json.RootElement.EnumerateArray()) {
            if (r.GetProperty("draft").GetBoolean()) continue;
            var release = new GitHubDownloadRelease { Name = r.GetProperty("name").GetString() ?? "", Tag = r.GetProperty("tag_name").GetString() ?? "", Url = r.GetProperty("html_url").GetString() ?? "", Notes = r.GetProperty("body").GetString() ?? "", PublishedAt = r.GetProperty("published_at").GetDateTime(), Prerelease = r.GetProperty("prerelease").GetBoolean() };
            foreach (var a in r.GetProperty("assets").EnumerateArray()) {
                var url = a.GetProperty("browser_download_url").GetString() ?? "";
                if (url.StartsWith($"https://github.com/{repo}/releases/download/", StringComparison.OrdinalIgnoreCase)) release.Assets.Add(new() { Name = a.GetProperty("name").GetString() ?? "", Url = url, Size = a.GetProperty("size").GetInt64() });
            }
            result.Results.Add(release);
        }
        return result;
    }
}
