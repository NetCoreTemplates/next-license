using System;
using System.Security.Cryptography;
using System.Text;
namespace MyApp.Licensing.Core;

public sealed class UpdateManifest
{
    public string ProductId { get; private set; } = "";
    public string ReleaseId { get; private set; } = "";
    public string Version { get; private set; } = "";
    public string Platform { get; private set; } = "";
    public string Channel { get; private set; } = "";
    public string Url { get; private set; } = "";
    public string Sha256 { get; private set; } = "";
    public long SizeBytes { get; private set; }
    public string ArtifactSignature { get; private set; } = "";
    public int RolloutPercent { get; private set; }
    public string MinimumUpdaterVersion { get; private set; } = "";
    public DateTime PublishedAtUtc { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }
    public static UpdateManifest Verify(string blob, TrustRoots roots)
    {
        var doc = SignedDocuments.Verify(blob, "MYAPPUPDATE1", SignedDocuments.UpdateDomain, "Release", "iat", roots);
        StrictJson.Fields(doc, "v", "kid", "cert", "iat", "prd", "rid", "ver", "platform", "ch", "url", "sha256", "size", "artifact", "rollout", "minUpdater", "pub");
        var rollout = StrictJson.Number(doc, "rollout");
        var size = StrictJson.Number(doc, "size");
        var url = StrictJson.Text(doc, "url", 2048);
        if (rollout < 0 || rollout > 100 || size < 1 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || !string.IsNullOrEmpty(uri.UserInfo)) throw new FormatException("Invalid update policy.");
        var hash = StrictJson.Text(doc, "sha256", 64); SignedDocuments.ValidateHash(hash);
        return new UpdateManifest {
            ProductId = StrictJson.Text(doc, "prd", 80), ReleaseId = StrictJson.Text(doc, "rid", 36),
            Version = StrictJson.Text(doc, "ver", 100), Platform = StrictJson.Text(doc, "platform", 40),
            Channel = StrictJson.Text(doc, "ch", 10), Url = url, Sha256 = hash, SizeBytes = size,
            ArtifactSignature = StrictJson.Text(doc, "artifact", 8192), RolloutPercent = (int)rollout,
            MinimumUpdaterVersion = StrictJson.Text(doc, "minUpdater", 40), PublishedAtUtc = StrictJson.Date(doc, "pub"),
            IssuedAtUtc = StrictJson.Date(doc, "iat"),
        };
    }
    public bool OfferedTo(string installationId)
    {
        if (!Guid.TryParseExact(installationId, "D", out _)) throw new ArgumentException("A random installation UUID is required.");
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(installationId.ToLowerInvariant() + ":" + ReleaseId));
            var bucket = ((uint)hash[0] << 24 | (uint)hash[1] << 16 | (uint)hash[2] << 8 | hash[3]) % 10000;
            return bucket < RolloutPercent * 100;
        }
    }
}
