using MyApp.Migrations;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using NUnit.Framework;
using ServiceStack.OrmLite;
namespace MyApp.Tests;
[Category("Database")]
public class LicenseAccountDeletionTests
{
    [Test]
    public void Deletion_removes_activation_and_links_but_preserves_commercial_evidence()
    {
        var factory = DatabaseTestRun.CreateFactory();
        using var db = factory.OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        var id = Guid.NewGuid();
        db.Insert(new SoftwareLicense { Id = id, UserId = "buyer", ShortKeyHash = "hash" });
        db.Insert(new LicenseOrder { Id = Guid.NewGuid(), OrderNumber = "order", UserId = "buyer" });
        db.Insert(new LicenseNotificationPreferences { UserId = "buyer" });
        new LicenseAccountDeletion(factory).RemovePersonalAccess("buyer");
        Assert.That(db.Count<LicenseNotificationPreferences>(), Is.Zero);
        Assert.That(db.Select<SoftwareLicense>()[0].UserId, Does.StartWith("deleted:"));
        Assert.That(db.Select<LicenseOrder>()[0].UserId, Is.EqualTo(db.Select<SoftwareLicense>()[0].UserId));
        Assert.That(db.Count<LicenseOrder>(), Is.EqualTo(1));
    }
}
