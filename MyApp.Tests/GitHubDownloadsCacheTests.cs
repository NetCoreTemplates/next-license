using MyApp.ServiceInterface;
using NUnit.Framework;
using ServiceStack;

namespace MyApp.Tests;

public class GitHubDownloadsCacheTests
{
    private static string Release(string tag) => $$"""
        [{"draft":false,"name":"Release","tag_name":"{{tag}}","html_url":"https://github.com/acme/app/releases/tag/{{tag}}","body":"Notes","published_at":"2026-09-21T00:00:00Z","prerelease":false,"assets":[{"name":"app.zip","browser_download_url":"https://github.com/acme/app/releases/download/{{tag}}/app.zip","size":42}]}]
        """;

    [Test]
    public async Task Disk_cache_survives_restart_and_failed_refresh_without_crossing_repositories()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            await new GitHubDownloadsCache(directory).Get("acme/app", () => Task.FromResult(Release("v1")));
            var restarted = new GitHubDownloadsCache(directory);
            var result = await restarted.Get("acme/app", () => Task.FromException<string>(new HttpRequestException()));
            Assert.That(result.Results.Single().Tag, Is.EqualTo("v1"));
            Assert.ThrowsAsync<HttpError>(() => restarted.Get("other/app", () => Task.FromException<string>(new HttpRequestException())));
            await restarted.Get("acme/app", () => Task.FromResult("invalid"));
            result = await new GitHubDownloadsCache(directory).Get("acme/app", () => Task.FromException<string>(new TaskCanceledException()));
            Assert.That(result.Results.Single().Tag, Is.EqualTo("v1"));
        } finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public async Task Failures_preserve_last_success_and_recovery_replaces_it()
    {
        var cache = new GitHubDownloadsCache();
        await cache.Get("acme/app", () => Task.FromResult(Release("v1")));
        foreach (var error in new Exception[] { new HttpRequestException("rate limited"), new TaskCanceledException(), new System.Text.Json.JsonException() }) {
            var fallback = await cache.Get("acme/app", () => Task.FromException<string>(error));
            Assert.That(fallback.Results.Single().Assets.Single().Url, Does.Contain("/v1/"));
        }
        foreach (var body in new[] { "not json", "{}", "[{\"draft\":false}]" }) {
            var fallback = await cache.Get("acme/app", () => Task.FromResult(body));
            Assert.That(fallback.Results.Single().Tag, Is.EqualTo("v1"));
        }
        var live = await cache.Get("acme/app", () => Task.FromResult(Release("v2")));
        Assert.That(live.Results.Single().Tag, Is.EqualTo("v2"));
        var latest = await cache.Get("acme/app", () => Task.FromException<string>(new HttpRequestException()));
        Assert.That(latest.Results.Single().Tag, Is.EqualTo("v2"));
        Assert.ThrowsAsync<HttpError>(() => cache.Get("other/app", () => Task.FromException<string>(new HttpRequestException())));
    }

    [Test]
    public async Task Successful_empty_response_replaces_old_releases()
    {
        var cache = new GitHubDownloadsCache();
        await cache.Get("acme/app", () => Task.FromResult(Release("v1")));
        await cache.Get("acme/app", () => Task.FromResult("[]"));
        var result = await cache.Get("acme/app", () => Task.FromException<string>(new HttpRequestException()));
        Assert.That(result.Results, Is.Empty);
    }
}
