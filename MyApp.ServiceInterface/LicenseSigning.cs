using System.Data;
using System.Security.Cryptography;
using MyApp.Licensing;
using MyApp.Licensing.Core;
using MyApp.ServiceModel;
using ServiceStack.OrmLite;

namespace MyApp.ServiceInterface;

public sealed class LicensingConfig
{
    public string? LicensePrivateKeyPem { get; set; }
    public string? LicensePrivateKeyPath { get; set; }
    public string LicenseIssuer { get; set; } = "acme-studio";
    public string? ShortKeySalt { get; set; }
    public bool GeneratePreviewKeys { get; set; }
    public int RenewalDiscountGraceDays { get; set; } = 60;
    public string? GitHubRepository { get; set; }
    public bool NagLapsedUpdates { get; set; }
}

public sealed class LicenseSigning(LicensingConfig config)
{
    public (string Blob, string KeyId) SignLicense(IDbConnection db, SoftwareLicense license, string shortKey, DateTime atUtc)
    {
        if (license.ProductId != LicenseProduct.Id) throw new InvalidOperationException("Unknown license product.");
        var cutoff = license.UpdatesThroughUtc.HasValue ? DateTime.SpecifyKind(license.UpdatesThroughUtc.Value, DateTimeKind.Utc) : (DateTime?)null;
        _ = new PaidEntitlement(license.ProductId, license.Edition, license.UpdateMode, cutoff);
        if (string.IsNullOrWhiteSpace(config.LicensePrivateKeyPem))
            throw new InvalidOperationException("Configure Licensing:LicensePrivateKeyPem to issue JWT licenses.");
        var claims = new LicenseClaims {
            Issuer = config.LicenseIssuer, Product = license.ProductId, Id = license.Id.ToString("D"),
            Name = license.LicenseeName, Organization = license.LicenseeOrganization, Seats = license.Seats,
            Edition = license.Edition.ToString(), IssuedAt = new DateTimeOffset(atUtc).ToUnixTimeSeconds(),
            Lifetime = license.UpdateMode == UpdateMode.Lifetime,
            UpdatesThrough = cutoff?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), RefreshKey = shortKey,
        };
        return (LicenseJwt.Sign(claims, config.LicensePrivateKeyPem), "jwt-es256");
    }
    public LicenseIdentity ReadLicense(string blob)
    {
        using var key = ECDsa.Create(); key.ImportFromPem(config.LicensePrivateKeyPem ?? "");
        var c = LicenseJwt.Read(blob, key.ExportSubjectPublicKeyInfoPem(), config.LicenseIssuer, LicenseProduct.Id);
        return new LicenseIdentity(c.Id, c.RefreshKey ?? "");
    }

    public void ValidateLicenseIssuance()
    {
        var salt = ReadSecret(config.ShortKeySalt, "Licensing:ShortKeySalt");
        CryptographicOperations.ZeroMemory(salt);
        using var key = ECDsa.Create();
        key.ImportFromPem(config.LicensePrivateKeyPem ?? "");
        var parameters = key.ExportParameters(true);
        try {
            if (parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value || parameters.D == null)
                throw new InvalidOperationException("An ES256 private signing key is required.");
        } finally { if (parameters.D != null) CryptographicOperations.ZeroMemory(parameters.D); }
    }
    public string HashShortKey(string shortKey)
    {
        var salt = ReadSecret(config.ShortKeySalt, "Licensing:ShortKeySalt");
        try { return Licensing.Core.ShortKey.Hash(shortKey, salt); }
        finally { CryptographicOperations.ZeroMemory(salt); }
    }
    private static byte[] ReadSecret(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"Configure {name} in an external secret store.");
        var bytes = Convert.FromBase64String(value);
        if (bytes.Length != 32) throw new InvalidOperationException($"{name} must contain exactly 32 base64-encoded bytes.");
        return bytes;
    }
}

public sealed record LicenseIdentity(string Id, string ShortKey);
