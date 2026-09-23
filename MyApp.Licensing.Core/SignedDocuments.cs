using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MyApp.Licensing.Core;

public sealed class TrustRoots
{
    private readonly Dictionary<string, ECParameters> license = new Dictionary<string, ECParameters>(StringComparer.Ordinal);
    private readonly Dictionary<string, ECParameters> release = new Dictionary<string, ECParameters>(StringComparer.Ordinal);
    public void Add(string purpose, string id, byte[] x, byte[] y)
    {
        if (x.Length != 32 || y.Length != 32) throw new ArgumentException("P-256 coordinates required.");
        var parameters = new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = (byte[])x.Clone(), Y = (byte[])y.Clone() } };
        (purpose == "License" ? license : purpose == "Release" ? release : throw new ArgumentException("Invalid purpose.")).Add(id, parameters);
    }
    internal ECParameters Get(string purpose, string id) =>
        (purpose == "License" ? license : release).TryGetValue(id, out var key) ? key : throw new FormatException("Untrusted root.");
}

public sealed class VerifiedBuild
{
    public string ReleaseId { get; internal set; } = "";
    public string ProductId { get; internal set; } = "";
    public string Version { get; internal set; } = "";
    public string CatalogHash { get; internal set; } = "";
    public DateTime PublishedAtUtc { get; internal set; }
    public ReleaseChannel Channel { get; internal set; }
}

public static class SignedDocuments
{
    public const string BuildDomain = "MYAPP-BUILD-V1";
    public const string ArtifactDomain = "MYAPP-ARTIFACT-V1";
    public const string UpdateDomain = "MYAPP-UPDATE-V1";
    public const string CertificateDomain = "MYAPP-CERTIFICATE-V1";

    public static ECParameters VerifyLeafCertificate(string certificate, TrustRoots roots, string purpose, string keyId, DateTime signedAtUtc)
    {
        Utc.Require(signedAtUtc);
        if (purpose != "License" && purpose != "Release") throw new FormatException("Unknown signing purpose.");
        var parts = Split(certificate, "MYAPPCERT1");
        var cert = Parse(parts[1]);
        StrictJson.Fields(cert, "v", "root", "kid", "purpose", "x", "y", "nbf", "naf");
        if (StrictJson.Number(cert, "v") != 1 || StrictJson.Text(cert, "purpose", 10) != purpose
            || StrictJson.Text(cert, "kid", 80) != keyId) throw new FormatException("Invalid certificate identity.");
        VerifyBytes(CertificateDomain, parts[1], parts[2], roots.Get(purpose, StrictJson.Text(cert, "root", 80)));
        var from = StrictJson.Date(cert, "nbf"); var through = StrictJson.Date(cert, "naf");
        if (from >= through || signedAtUtc < from || signedAtUtc >= through) throw new FormatException("Signature outside certificate validity.");
        var x = Decode(StrictJson.Text(cert, "x", 44)); var y = Decode(StrictJson.Text(cert, "y", 44));
        if (x.Length != 32 || y.Length != 32) throw new FormatException("Invalid P-256 key.");
        return new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = x, Y = y } };
    }
    public static VerifiedBuild VerifyBuild(string blob, TrustRoots roots)
    {
        var doc = Verify(blob, "MYAPPBUILD1", BuildDomain, "Release", "pub", roots);
        StrictJson.Fields(doc, "v", "kid", "cert", "rid", "prd", "ver", "commit", "pub", "ch", "catalog");
        StrictJson.Text(doc, "commit", 64);
        var channel = StrictJson.Text(doc, "ch", 10);
        if (channel != "Stable" && channel != "Beta") throw new FormatException("Invalid channel.");
        var hash = StrictJson.Text(doc, "catalog", 64);
        ValidateHash(hash);
        return new VerifiedBuild {
            ReleaseId = StrictJson.Text(doc, "rid", 36), ProductId = StrictJson.Text(doc, "prd", 80),
            Version = StrictJson.Text(doc, "ver", 100), CatalogHash = hash, PublishedAtUtc = StrictJson.Date(doc, "pub"),
            Channel = channel == "Stable" ? ReleaseChannel.Stable : ReleaseChannel.Beta,
        };
    }
    public static void VerifyCatalog(VerifiedBuild build, byte[] compiledCatalog)
    {
        using (var sha = SHA256.Create())
            if (Hex(sha.ComputeHash(compiledCatalog)) != build.CatalogHash) throw new FormatException("Feature catalog mismatch.");
    }
    public static void VerifyArtifact(string signature, TrustRoots roots, string releaseId, string platform, byte[] bytes)
    {
        using (var sha = SHA256.Create()) VerifyArtifactHash(signature, roots, releaseId, platform, bytes.LongLength, Hex(sha.ComputeHash(bytes)));
    }
    public static void VerifyArtifactHash(string signature, TrustRoots roots, string releaseId, string platform, long size, string sha256)
    {
        var doc = Verify(signature, "MYAPPART1", ArtifactDomain, "Release", "iat", roots);
        StrictJson.Fields(doc, "v", "kid", "cert", "rid", "platform", "size", "sha256", "iat");
        if (StrictJson.Text(doc, "rid") != releaseId || StrictJson.Text(doc, "platform") != platform
            || StrictJson.Number(doc, "size") != size || StrictJson.Text(doc, "sha256") != sha256)
            throw new FormatException("Artifact identity mismatch.");
    }
    internal static Dictionary<string, object?> Verify(string blob, string prefix, string domain, string purpose, string timestamp, TrustRoots roots)
    {
        var parts = Split(blob, prefix);
        var doc = Parse(parts[1]);
        if (StrictJson.Number(doc, "v") != 1) throw new FormatException("Unsupported version.");
        var key = VerifyLeafCertificate(StrictJson.Text(doc, "cert", 8192), roots, purpose,
            StrictJson.Text(doc, "kid", 80), StrictJson.Date(doc, timestamp));
        VerifyBytes(domain, parts[1], parts[2], key);
        return doc;
    }
    private static string[] Split(string blob, string prefix)
    {
        if (blob == null || blob.Length > 48000) throw new FormatException("Document too large.");
        var parts = blob.Split('.');
        if (parts.Length != 3 || parts[0] != prefix) throw new FormatException("Invalid document type.");
        return parts;
    }
    private static Dictionary<string, object?> Parse(string segment) => StrictJson.Object(new UTF8Encoding(false, true).GetString(Decode(segment)));
    private static void VerifyBytes(string domain, string payload, string signature, ECParameters key)
    {
        var sig = Decode(signature);
        if (sig.Length != 64) throw new FormatException("P1363 signature required.");
        using (var ec = ECDsa.Create())
        using (var sha = SHA256.Create())
        {
            ec.ImportParameters(key);
            var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(domain + "." + payload));
            // ECDsa's default format in .NET is IEEE P1363, including the netstandard API.
            if (!ec.VerifyHash(hash, sig)) throw new CryptographicException("Invalid signature.");
        }
    }
    public static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static byte[] Decode(string text)
    {
        if (text.Length == 0 || text.Length > 44000) throw new FormatException("Invalid base64url length.");
        foreach (var c in text) if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_')) throw new FormatException("Noncanonical base64url.");
        var bytes = Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/') + new string('=', (4 - text.Length % 4) % 4));
        if (Encode(bytes) != text) throw new FormatException("Noncanonical base64url.");
        return bytes;
    }
    public static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    public static void ValidateHash(string hash)
    {
        if (hash.Length != 64) throw new FormatException("SHA-256 required.");
        foreach (var c in hash) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) throw new FormatException("Invalid SHA-256.");
    }
    private static Edition ParseEdition(string value) => value == "Free" ? Edition.Free : value == "Pro" ? Edition.Pro : value == "Enterprise" ? Edition.Enterprise : throw new FormatException("Unknown edition.");
}
