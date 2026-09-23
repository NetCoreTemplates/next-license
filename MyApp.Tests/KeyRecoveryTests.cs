using System.Security.Cryptography;
using MyApp.Licensing;
using NUnit.Framework;
namespace MyApp.Tests;
public class KeyRecoveryTests
{
    [Test]
    public void Restored_private_pem_verifies_existing_tokens_and_issues_new_tokens()
    {
        string backup, publicKey, original;
        var claims = new LicenseClaims { Issuer = "acme-studio", Product = "acme-studio",
            Id = Guid.NewGuid().ToString("D"), Name = "Recovery fixture", Seats = 3,
            Edition = "Pro", Lifetime = true, IssuedAt = 1790000000 };
        using (var key = ECDsa.Create(ECCurve.NamedCurves.nistP256)) {
            backup = key.ExportPkcs8PrivateKeyPem();
            publicKey = key.ExportSubjectPublicKeyInfoPem();
            original = LicenseJwt.Sign(claims, backup);
        }
        using var restored = ECDsa.Create();
        restored.ImportFromPem(backup);
        var replacement = LicenseJwt.Sign(claims with { IssuedAt = claims.IssuedAt + 1 }, restored.ExportPkcs8PrivateKeyPem());
        foreach (var token in new[] { original, replacement }) {
            var result = LicenseJwt.Verify(token, publicKey, "acme-studio", "acme-studio", "2026-09-21");
            Assert.That(result.Valid, Is.True);
            Assert.That(result.License!.Seats, Is.EqualTo(3));
        }
    }
}
