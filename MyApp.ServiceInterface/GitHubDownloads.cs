using Microsoft.Extensions.Caching.Memory;
using MyApp.ServiceModel;
using ServiceStack;
using System.Text.RegularExpressions;
namespace MyApp.ServiceInterface;

public static class GitHubDownloads
{
    public static async Task<GitHubDownloadsResponse> Get(string? repo, IMemoryCache? cache = null) {
        var result = new GitHubDownloadsResponse { Repository = repo };
        if (string.IsNullOrWhiteSpace(repo)) return result;
        if (!Regex.IsMatch(repo, "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw HttpError.BadRequest("Configure Licensing__GitHubRepository as owner/repository.");
        var cacheKey = "software-github:" + repo;
        if (cache != null && cache.TryGetValue<GitHubDownloadsResponse>(cacheKey, out var cached) && cached != null) return cached;
        result = await GitHubDownloadsCache.Shared.Get(repo, async () => {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 4_000_000 };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Software-Licensing/1.0");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.GetAsync($"https://api.github.com/repos/{repo}/releases?per_page=100");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        });
        cache?.Set(cacheKey, result, TimeSpan.FromMinutes(2));
        return result;
    }
}
