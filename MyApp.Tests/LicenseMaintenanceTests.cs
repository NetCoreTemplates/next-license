using System.Security.Cryptography;
using MyApp.Licensing;
using MyApp.Licensing.Core;
using MyApp.Migrations;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using NUnit.Framework;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Host;
using ServiceStack.OrmLite;
using ServiceStack.Testing;
namespace MyApp.Tests;

[NonParallelizable]
[Category("Database")]
public class LicenseMaintenanceTests
{
    [Test]
    public void Support_changes_preserve_identity_reject_stale_replays_and_keep_offline_history()
    {
        var factory = DatabaseTestRun.CreateFactory();
        using var db = factory.OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        using var leaf = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTime.UtcNow;
        var config = new LicensingConfig { LicensePrivateKeyPem = leaf.ExportPkcs8PrivateKeyPem(),
            ShortKeySalt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
        using var host = new BasicAppHost { ConfigureContainer = c => c.Register<IDbConnectionFactory>(factory) }.Init();
        var request = new BasicRequest();
        request.Items[Keywords.Session] = new AuthUserSession { UserAuthId = "admin", IsAuthenticated = true, Roles = ["Admin"] };
        var signing = new LicenseSigning(config);
        Assert.Throws<InvalidOperationException>(() => signing.SignLicense(db,
            new SoftwareLicense { ProductId = "another-product" }, "unused", now));
        using var issue = new LicenseServices(signing) { Request = request };
        using var service = new LicenseMaintenanceServices(signing) { Request = request };
        var original = (LicenseBlobResponse)issue.Post(new IssueLicense { UserId = "buyer", LicenseeName = "Buyer", Edition = Edition.Pro,
            Seats = 2, UpdateMode = UpdateMode.ThroughDate, UpdatesThroughUtc = now.AddMonths(12) });
        using var refresh = new ActivationServices(signing, new ActivationThrottle()) { Request = request };
        Assert.That(((LicenseBlobResponse)refresh.Post(new ActivateLicense { Blob = original.Blob })).Blob, Is.EqualTo(original.Blob));
        var claims = LicenseJwt.Read(original.Blob!, leaf.ExportSubjectPublicKeyInfoPem(), "acme-studio", "acme-studio");
        var id = Guid.Parse(claims.Id);
        var extend = new ExtendUpdatesThrough { Id = id, ExpectedBlobVersion = 1, UpdatesThroughUtc = now.AddMonths(13), Reason = "Support credit" };
        service.Post(extend);
        Assert.Throws<HttpError>(() => service.Post(extend));
        Assert.Throws<HttpError>(() => service.Post(new ExtendUpdatesThrough { Id = id, ExpectedBlobVersion = 2, UpdatesThroughUtc = now, Reason = "Shorten" }));
        service.Post(new UpgradeToLifetimeUpdates { Id = id, ExpectedBlobVersion = 2, Reason = "Lifetime grant" });
        var reissued = (LicenseBlobResponse)service.Post(new ReissueLicense { Id = id, ExpectedBlobVersion = 3, RotateShortKey = true, Reason = "Leaked short key" });
        var latest = LicenseJwt.Read(reissued.Blob!, leaf.ExportSubjectPublicKeyInfoPem(), "acme-studio", "acme-studio");
        Assert.That(latest.Id, Is.EqualTo(claims.Id));
        Assert.That(latest.RefreshKey, Is.Not.EqualTo(claims.RefreshKey));
        Assert.That(latest.Lifetime, Is.True);
        Assert.That(latest.Seats, Is.EqualTo(2));
        Assert.Throws<HttpError>(() => refresh.Post(new ActivateLicense { Blob = original.Blob }));
        Assert.That(((LicenseBlobResponse)refresh.Post(new ActivateLicense { Key = latest.RefreshKey! })).Blob, Is.EqualTo(reissued.Blob));
        Assert.That(db.Count<LicenseBlob>(), Is.EqualTo(4));
        Assert.That(db.Count<LicensingAuditEvent>(), Is.EqualTo(4));
        Assert.That(LicenseJwt.Read(original.Blob!, leaf.ExportSubjectPublicKeyInfoPem(), "acme-studio", "acme-studio").Lifetime, Is.False);
    }
}
