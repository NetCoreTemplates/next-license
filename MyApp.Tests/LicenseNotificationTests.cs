using MyApp.Migrations;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using MyApp.Licensing.Core;
using NUnit.Framework;
using ServiceStack.OrmLite;
namespace MyApp.Tests;
[Category("Database")]
public class LicenseNotificationTests
{
    [Test]
    public void Reminders_are_idempotent_and_exclude_lifetime_and_opt_out()
    {
        using var db = DatabaseTestRun.CreateFactory().OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        var now = DateTime.UtcNow;
        foreach (var user in new[] { "dated", "lifetime", "optout" })
            db.Insert(new SoftwareLicense { Id = Guid.NewGuid(), UserId = user, ShortKeyHash = user,
                UpdateMode = user == "lifetime" ? UpdateMode.Lifetime : UpdateMode.ThroughDate,
                UpdatesThroughUtc = user == "lifetime" ? null : now.AddDays(30) });
        db.Insert(new LicenseNotificationPreferences { UserId = "optout", UpdateReminders = false });
        LicenseNotifications.Schedule(db, now); LicenseNotifications.Schedule(db, now);
        Assert.That(db.Count<LicenseNotification>(), Is.EqualTo(1));
        Assert.That(db.Select<LicenseNotification>()[0].UserId, Is.EqualTo("dated"));
    }
    [Test]
    public void GitHub_release_notifications_are_stable_only_opt_in_and_deduplicated()
    {
        using var db = DatabaseTestRun.CreateFactory().OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        var now = DateTime.UtcNow;
        foreach (var user in new[] { "optin", "default" })
            db.Insert(new SoftwareLicense { Id = Guid.NewGuid(), UserId = user, ShortKeyHash = user, UpdateMode = UpdateMode.Lifetime });
        db.Insert(new LicenseNotificationPreferences { UserId = "optin", ReleaseAnnouncements = true });
        GitHubDownloadRelease[] releases = [
            new() { Tag = "v1.0.0", PublishedAt = now.AddDays(-1), Url = "https://github.com/example/app/releases/tag/v1.0.0", Notes = new string('x', 4000) },
            new() { Tag = "v2.0.0-beta", PublishedAt = now, Prerelease = true },
            new() { Tag = "v0.9.0", PublishedAt = now.AddDays(-8) },
        ];
        LicenseNotifications.Schedule(db, now, releases);
        LicenseNotifications.Schedule(db, now, releases);
        var notifications = db.Select<LicenseNotification>();
        Assert.That(notifications, Has.Count.EqualTo(1));
        Assert.That(notifications[0].UserId, Is.EqualTo("optin"));
        Assert.That(notifications[0].Subject, Does.Contain("v1.0.0"));
        Assert.That(notifications[0].Body.Length, Is.GreaterThan(4000));
    }
}
