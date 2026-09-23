using System.Net.Http.Json;
using System.Security.Cryptography;
using MyApp.Licensing.Core;
namespace MyApp.Licensing;

public sealed class UpdateClient(HttpClient http, TrustRoots roots, TimeProvider? clock = null)
{
    public async Task<UpdateManifest?> Check(Uri feed, string product, string platform, string channel, SemanticVersion current,
        SemanticVersion updaterVersion, string installation, CancellationToken token = default)
    {
        if (feed.Scheme != "https") throw new ArgumentException("Update feeds require HTTPS.", nameof(feed));
        using var response = await http.GetAsync(feed, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var bytes = await ReadBounded(response.Content, 64000, token);
        var envelope = System.Text.Json.JsonSerializer.Deserialize<FeedResponse>(bytes, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (envelope?.Manifest == null) return null;
        var manifest = UpdateManifest.Verify(envelope.Manifest, roots);
        // Update freshness is deliberately separate from the clock-free paid entitlement evaluator.
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (manifest.IssuedAtUtc < now.AddHours(-24) || manifest.IssuedAtUtc > now.AddMinutes(5))
            throw new CryptographicException("Update feed is stale or issued in the future.");
        if (manifest.ProductId != product || manifest.Platform != platform || manifest.Channel != channel)
            throw new CryptographicException("Update identity mismatch.");
        if (!SemanticVersion.TryParse(manifest.Version, out var target) || target!.CompareTo(current) <= 0
            || !SemanticVersion.TryParse(manifest.MinimumUpdaterVersion, out var minimum) || updaterVersion.CompareTo(minimum) < 0
            || !manifest.OfferedTo(installation)) return null;
        return manifest;
    }
    /// <summary>Returns a verified file. The host must revalidate immediately before installing if the directory is not protected.</summary>
    public async Task Download(UpdateManifest update, string newFile, long maximumBytes, CancellationToken token = default)
    {
        if (update.SizeBytes > maximumBytes || maximumBytes < 1) throw new InvalidOperationException("Artifact exceeds the host download limit.");
        // Validate the detached identity before requesting remote bytes.
        SignedDocuments.VerifyArtifactHash(update.ArtifactSignature, roots, update.ReleaseId, update.Platform, update.SizeBytes, update.Sha256);
        var created = false;
        try
        {
            await using var file = new FileStream(newFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            using var response = await http.GetAsync(update.Url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[65536]; long length = 0; int read;
            while ((read = await stream.ReadAsync(buffer, token)) > 0)
            {
                length += read;
                if (length > update.SizeBytes) throw new CryptographicException("Artifact is larger than its signed size.");
                hash.AppendData(buffer, 0, read); await file.WriteAsync(buffer.AsMemory(0, read), token);
            }
            if (length != update.SizeBytes || Convert.ToHexStringLower(hash.GetHashAndReset()) != update.Sha256)
                throw new CryptographicException("Artifact hash mismatch.");
        }
        catch { if (created) File.Delete(newFile); throw; }
    }
    private static async Task<byte[]> ReadBounded(HttpContent content, int limit, CancellationToken token)
    {
        await using var stream = await content.ReadAsStreamAsync(token); using var output = new MemoryStream();
        var buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0) { if (output.Length + read > limit) throw new FormatException("Feed too large."); output.Write(buffer, 0, read); }
        return output.ToArray();
    }
    private sealed class FeedResponse { public string? Manifest { get; set; } }
}
