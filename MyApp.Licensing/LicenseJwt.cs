using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyApp.Licensing.Core;

namespace MyApp.Licensing;

public sealed record LicenseClaims
{
    [JsonPropertyName("iss")] public string Issuer { get; init; } = "";
    [JsonPropertyName("aud")] public string Product { get; init; } = "";
    [JsonPropertyName("sub")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("organization")] public string? Organization { get; init; }
    [JsonPropertyName("seats")] public int Seats { get; init; }
    [JsonPropertyName("edition")] public string Edition { get; init; } = "Pro";
    [JsonPropertyName("iat")] public long IssuedAt { get; init; }
    [JsonPropertyName("lifetime")] public bool Lifetime { get; init; }
    [JsonPropertyName("updatesThrough")] public string? UpdatesThrough { get; init; }
    // Optional portal refresh credential; not needed by the offline verifier.
    [JsonPropertyName("refreshKey")] public string? RefreshKey { get; init; }
}

public sealed record LicenseCheck(bool Valid, string Status, LicenseClaims? License);

/// <summary>Offline ES256 JWT licenses. Public keys and build dates are supplied by the app, never the token.</summary>
public static class LicenseJwt
{
    public static string Sign(LicenseClaims claims, string privateKeyPem)
    {
        ValidateClaims(claims);
        using var key = ECDsa.Create(); key.ImportFromPem(privateKeyPem);
        RequireP256(key);
        var header = SignedDocuments.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"ES256\",\"typ\":\"JWT\"}"));
        var payload = SignedDocuments.Encode(JsonSerializer.SerializeToUtf8Bytes(claims));
        var input = header + "." + payload;
        return input + "." + SignedDocuments.Encode(key.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    public static LicenseClaims Read(string token, string publicKeyPem, string issuer, string product)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16000) throw new FormatException("Invalid license size.");
        var parts = token.Split('.');
        if (parts.Length != 3) throw new FormatException("Expected a signed JWT.");
        using var header = Parse(parts[0]);
        var h = header.RootElement;
        if (h.GetProperty("alg").GetString() != "ES256" || h.GetProperty("typ").GetString() != "JWT"
            || h.EnumerateObject().Any(p => p.Name is not ("alg" or "typ"))) throw new FormatException("Unsupported JWT header.");
        var signature = SignedDocuments.Decode(parts[2]);
        if (signature.Length != 64) throw new FormatException("Invalid ES256 signature.");
        using var key = ECDsa.Create(); key.ImportFromPem(publicKeyPem); RequireP256(key);
        if (!key.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), signature, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) throw new CryptographicException("Invalid signature.");
        using var payload = Parse(parts[1]);
        // This perpetual-license profile has no wall-clock expiry or not-before claims.
        if (payload.RootElement.TryGetProperty("exp", out _) || payload.RootElement.TryGetProperty("nbf", out _))
            throw new FormatException("Time-limited JWTs are not perpetual licenses.");
        foreach (var required in new[] { "iss", "aud", "sub", "name", "seats", "edition", "iat", "lifetime" })
            if (!payload.RootElement.TryGetProperty(required, out _)) throw new FormatException("Missing license claim.");
        var claims = payload.RootElement.Deserialize<LicenseClaims>() ?? throw new FormatException("Missing claims.");
        ValidateClaims(claims);
        if (claims.Issuer != issuer || claims.Product != product) throw new FormatException("Wrong issuer or product.");
        return claims;
    }

    public static LicenseCheck Verify(string? token, string publicKeyPem, string issuer, string product, string buildDate)
    {
        var released = Date(buildDate); // Invalid app configuration is a programming error.
        if (string.IsNullOrWhiteSpace(token)) return new(false, "missing", null);
        try
        {
            var license = Read(token.Trim(), publicKeyPem, issuer, product);
            if (license.Edition is not ("Pro" or "Enterprise")) return new(false, "editionNotCovered", license);
            return license.Lifetime || released <= Date(license.UpdatesThrough!)
                ? new(true, "valid", license) : new(false, "buildNotCovered", license);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException)
        { return new(false, "invalid", null); }
    }

    private static JsonDocument Parse(string segment)
    {
        var bytes = SignedDocuments.Decode(segment);
        var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        if (doc.RootElement.ValueKind != JsonValueKind.Object || doc.RootElement.EnumerateObject().GroupBy(p => p.Name).Any(g => g.Count() > 1))
        { doc.Dispose(); throw new FormatException("Expected unique JSON properties."); }
        return doc;
    }
    private static void ValidateClaims(LicenseClaims c)
    {
        if (string.IsNullOrWhiteSpace(c.Issuer) || c.Issuer.Length > 200 || string.IsNullOrWhiteSpace(c.Product) || c.Product.Length > 80
            || !Guid.TryParseExact(c.Id, "D", out _) || string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 200
            || c.Organization?.Length > 200 || c.Seats is < 1 or > 1000000 || c.IssuedAt < 0 || c.IssuedAt > 253402300799
            || c.Edition is not ("Free" or "Pro" or "Enterprise") || (c.Lifetime ? c.UpdatesThrough != null : c.UpdatesThrough == null))
            throw new FormatException("Invalid license claims.");
        if (!c.Lifetime) _ = Date(c.UpdatesThrough!);
    }
    private static DateOnly Date(string value) => DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.None, out var date) ? date : throw new FormatException("Expected YYYY-MM-DD.");
    private static void RequireP256(ECDsa key)
    {
        if (key.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
            throw new CryptographicException("ES256 requires P-256.");
    }
}
