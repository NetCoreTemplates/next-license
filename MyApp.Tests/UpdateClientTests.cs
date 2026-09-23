using System.Net;
using System.Security.Cryptography;
using System.Text;
using MyApp.Licensing;
using MyApp.Licensing.Core;
using NUnit.Framework;
namespace MyApp.Tests;

public class UpdateClientTests
{
    [Test]
    public async Task Signed_feed_rollout_and_artifact_integrity_are_verified()
    {
        using var root = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var leaf = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTime.UtcNow;
        var cert = DocumentSigner.Certify(root, leaf, "release-root", "leaf", "Release", now.AddDays(-1), now.AddDays(1));
        var roots = new TrustRoots(); var pub = root.ExportParameters(false); roots.Add("Release", "release-root", pub.Q.X!, pub.Q.Y!);
        var bytes = Encoding.UTF8.GetBytes("final artifact bytes");
        var hash = SignedDocuments.Hex(SHA256.HashData(bytes));
        var id = Guid.NewGuid().ToString("D");
        var artifact = DocumentSigner.Sign("MYAPPART1", SignedDocuments.ArtifactDomain, new {
            v = 1, kid = "leaf", cert, rid = id, platform = "linux-x64", size = bytes.Length, sha256 = hash, iat = now,
        }, leaf);
        string Feed(int percent, DateTime? issued = null) => DocumentSigner.Sign("MYAPPUPDATE1", SignedDocuments.UpdateDomain, new {
            v = 1, kid = "leaf", cert, iat = issued ?? now, prd = "acme-studio", rid = id, ver = "2.0.0", platform = "linux-x64", ch = "Stable",
            url = "https://github.com/example/app/releases/download/v2/app.bin", sha256 = hash, size = bytes.Length, artifact,
            rollout = percent, minUpdater = "1.0.0", pub = now,
        }, leaf);
        var installation = Guid.NewGuid().ToString("D");
        using var feedHttp = new HttpClient(new BytesHandler(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { manifest = Feed(100) })));
        var offered = await new UpdateClient(feedHttp, roots).Check(new Uri("https://example.invalid/updates/feed"), "acme-studio", "linux-x64", "Stable",
            SemanticVersion.Parse("1.0.0"), SemanticVersion.Parse("1.0.0"), installation);
        Assert.That(offered?.Version, Is.EqualTo("2.0.0"));
        using var staleHttp = new HttpClient(new BytesHandler(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { manifest = Feed(100, now.AddHours(-23)) })));
        Assert.ThrowsAsync<CryptographicException>(() => new UpdateClient(staleHttp, roots, new FixedClock(now.AddHours(2))).Check(
            new Uri("https://example.invalid/updates/feed"), "acme-studio", "linux-x64", "Stable", SemanticVersion.Parse("1.0.0"), SemanticVersion.Parse("1.0.0"), installation));
        Assert.That(UpdateManifest.Verify(Feed(0), roots).OfferedTo(installation), Is.False);
        var update = UpdateManifest.Verify(Feed(100), roots);
        Assert.That(update.OfferedTo(installation), Is.True);
        var partial = UpdateManifest.Verify(Feed(50), roots);
        Assert.That(partial.OfferedTo(installation), Is.EqualTo(partial.OfferedTo(installation)));
        SignedDocuments.VerifyArtifact(artifact, roots, id, "linux-x64", bytes);
        Assert.Throws<FormatException>(() => SignedDocuments.VerifyArtifact(artifact, roots, id, "win-x64", bytes));
        using var http = new HttpClient(new BytesHandler(bytes));
        var client = new UpdateClient(http, roots);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            await client.Download(update, path, 1000);
            Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(bytes));
            Assert.ThrowsAsync<IOException>(() => client.Download(update, path, 1000));
            Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(bytes), "An existing host file must not be deleted.");
        }
        finally { File.Delete(path); }
        using var corruptHttp = new HttpClient(new BytesHandler(Encoding.UTF8.GetBytes("corrupt")));
        Assert.ThrowsAsync<CryptographicException>(() => new UpdateClient(corruptHttp, roots).Download(update, path, 1000));
        Assert.That(File.Exists(path), Is.False);
    }
    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
    private sealed class BytesHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }
}
