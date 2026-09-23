using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MyApp.Licensing;
using NUnit.Framework;
namespace MyApp.Tests;

public class LicenseJwtTests
{
    [Test]
    public void Jwt_roundtrips_offline_and_matches_Electron_verifier()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = key.ExportSubjectPublicKeyInfoPem();
        var claims = new LicenseClaims { Issuer = "acme-studio", Product = "acme-studio", Id = Guid.NewGuid().ToString(),
            Name = "Alex Morgan", Organization = "Northstar Studio", Seats = 3, IssuedAt = 1700000000, UpdatesThrough = "2026-09-21" };
        var token = LicenseJwt.Sign(claims, key.ExportPkcs8PrivateKeyPem());
        var cases = new[] {
            (token, "2026-09-21", "valid"), (token, "2026-09-22", "buildNotCovered"),
            ("", "2026-09-21", "missing"), (token[..^3] + "aaa", "2026-09-21", "invalid"),
            (LicenseJwt.Sign(claims with { Lifetime = true, UpdatesThrough = null }, key.ExportPkcs8PrivateKeyPem()), "2040-01-01", "valid"),
            (LicenseJwt.Sign(claims with { Product = "other" }, key.ExportPkcs8PrivateKeyPem()), "2026-09-21", "invalid"),
            (LicenseJwt.Sign(claims with { Edition = "Free" }, key.ExportPkcs8PrivateKeyPem()), "2026-09-21", "editionNotCovered"),
        };
        var root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "MyApp.slnx"))) root = Directory.GetParent(root)!.FullName;
        var script = Path.Combine(root, "MyApp.Licensing.JavaScript", "verify-fixture.mjs");
        foreach (var (jwt, build, expected) in cases) {
            var actual = LicenseJwt.Verify(jwt, pub, "acme-studio", "acme-studio", build);
            Assert.That(actual.Status, Is.EqualTo(expected));
            var start = new ProcessStartInfo("node", script) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = Process.Start(start)!;
            process.StandardInput.Write(JsonSerializer.Serialize(new { token = jwt, publicKey = pub, issuer = "acme-studio", product = "acme-studio", buildDate = build }));
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEnd();
            Assert.That(process.WaitForExit(10000), Is.True);
            Assert.That(process.ExitCode, Is.Zero, process.StandardError.ReadToEnd());
            using var result = JsonDocument.Parse(output);
            Assert.That(result.RootElement.GetProperty("status").GetString(), Is.EqualTo(expected));
            if (actual.Valid) {
                Assert.That(actual.License!.Name, Is.EqualTo("Alex Morgan"));
                Assert.That(result.RootElement.GetProperty("license").GetProperty("seats").GetInt32(), Is.EqualTo(3));
                Assert.That(result.RootElement.GetProperty("license").GetProperty("organization").GetString(), Is.EqualTo("Northstar Studio"));
            }
        }
        using var wrong = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.That(LicenseJwt.Verify(token, wrong.ExportSubjectPublicKeyInfoPem(), "acme-studio", "acme-studio", "2026-09-21").Valid, Is.False);
        Assert.Throws<FormatException>(() => LicenseJwt.Sign(claims with { Seats = 0 }, key.ExportPkcs8PrivateKeyPem()));
    }
}
