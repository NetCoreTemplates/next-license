using System.Data;
using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;
namespace MyApp.ServiceInterface;
public class LicenseNotificationServices : Service
{
    public object Get(GetLicenseNotificationPreferences request) => Db.SingleById<LicenseNotificationPreferences>(GetSession().UserAuthId)
        ?? new LicenseNotificationPreferences { UserId = GetSession().UserAuthId };
    public void Post(SaveLicenseNotificationPreferences request) => Db.Save(new LicenseNotificationPreferences {
        UserId = GetSession().UserAuthId, UpdateReminders = request.UpdateReminders, ReleaseAnnouncements = request.ReleaseAnnouncements, WinBack = request.WinBack,
    });
    public void Post(ResendLicense request)
    {
        var license = Db.Single<SoftwareLicense>(x => x.Id == request.Id && x.UserId == GetSession().UserAuthId)
            ?? throw HttpError.NotFound("License not found.");
        if (license.Status != LicenseStatus.Active) throw HttpError.Conflict("Revoked licenses cannot be resent.");
        using var tx = Db.OpenTransaction();
        LicenseNotifications.Queue(Db, $"resend:{license.Id}:{DateTime.UtcNow:yyyyMMddHH}", license, "Delivery", "Your Acme Studio license",
            "Your current signed license file is available in My licenses. Sign in to download it securely.");
        tx.Commit();
    }
}
public static class LicenseNotifications
{
    public static void Queue(IDbConnection db, string id, SoftwareLicense license, string type, string subject, string body)
    {
        if (!db.Exists<LicenseNotification>(x => x.Id == id)) db.Insert(new LicenseNotification { Id = id, UserId = license.UserId, LicenseId = license.Id,
            Type = type, Subject = subject, Body = body, CreatedAtUtc = DateTime.UtcNow });
    }
    public static void Schedule(IDbConnection db, DateTime now, IReadOnlyList<GitHubDownloadRelease>? publishedReleases = null)
    {
        using var tx = db.OpenTransaction();
        var stable = (publishedReleases ?? []).Where(x => !x.Prerelease && x.PublishedAt <= now).ToArray();
        var releases = stable.Where(x => x.PublishedAt >= now.AddDays(-7));
        foreach (var license in db.Select<SoftwareLicense>(x => x.Status == LicenseStatus.Active && !x.UserId.StartsWith("deleted:")))
        {
            var preferences = db.SingleById<LicenseNotificationPreferences>(license.UserId) ?? new();
            if (license.UpdateMode == Licensing.Core.UpdateMode.ThroughDate && license.UpdatesThroughUtc is { } cutoff)
            {
                var days = (cutoff.Date - now.Date).Days;
                if (preferences.UpdateReminders && days is 30 or 7 or 0)
                    Queue(db, $"reminder:{license.Id}:{cutoff:yyyyMMdd}:{days}", license, "Reminder", "Your Acme Studio version update window",
                        $"Your license covers app versions released through {cutoff:yyyy-MM-dd} UTC. Covered versions keep working forever. Visit My licenses to renew or upgrade.");
                if (preferences.WinBack && days == -14)
                {
                    var versions = stable.Where(x => x.PublishedAt > cutoff)
                        .Select(x => x.Tag).ToArray();
                    if (versions.Length > 0) Queue(db, $"winback:{license.Id}:{cutoff:yyyyMMdd}", license, "WinBack", "New versions for your next project",
                        "Renew to unlock Pro in these versions: " + string.Join(", ", versions) + ". Your covered versions remain yours.");
                }
            }
            if (preferences.ReleaseAnnouncements)
                foreach (var release in releases)
                {
                    var releaseKey = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(release.Url)));
                    Queue(db, $"release:{license.UserId}:{releaseKey}", license, "Release", $"Acme Studio {release.Tag} is available",
                        "A new public build is available from Downloads. " + release.Notes);
                }
        }
        tx.Commit();
    }
}
