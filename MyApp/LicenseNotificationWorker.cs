using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using ServiceStack.Data;
using ServiceStack.OrmLite;
namespace MyApp;
public class LicenseNotificationWorker(IDbConnectionFactory factory, IServiceScopeFactory scopes, IConfiguration configuration,
    LicensingConfig licensing, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, LicenseStripeConfig site, LicenseAccountDeletion deletion, ILogger<LicenseNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var db = factory.OpenDbConnection();
                using var scope = scopes.CreateScope();
                var identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var owners = db.Select<SoftwareLicense>().Select(x => x.UserId).Concat(db.Select<LicenseOrder>().Select(x => x.UserId)).Distinct();
                foreach (var owner in owners.Where(x => !x.StartsWith("deleted:", StringComparison.Ordinal)))
                    if (!await identity.Users.AnyAsync(x => x.Id == owner, stoppingToken)) deletion.RemovePersonalAccess(owner);
                IReadOnlyList<GitHubDownloadRelease> releases = [];
                if (db.Exists<LicenseNotificationPreferences>(x => x.ReleaseAnnouncements || x.WinBack))
                {
                    try { releases = (await GitHubDownloads.Get(licensing.GitHubRepository, cache)).Results; }
                    catch (Exception) { logger.LogWarning("GitHub releases unavailable for notification scheduling; delivery emails and reminders will continue."); }
                }
                LicenseNotifications.Schedule(db, DateTime.UtcNow, releases);
                var smtp = configuration.GetSection("SmtpConfig").Get<SmtpConfig>();
                if (smtp == null || string.IsNullOrWhiteSpace(smtp.Host)) continue;
                var retry = DateTime.UtcNow.AddMinutes(-15);
                foreach (var notification in db.Select(db.From<LicenseNotification>().Where(x => x.SentAtUtc == null && x.Attempts < 10
                    && (x.LastAttemptUtc == null || x.LastAttemptUtc < retry)).OrderBy(x => x.CreatedAtUtc).Limit(50)))
                {
                    var license = db.SingleById<SoftwareLicense>(notification.LicenseId);
                    if (notification.Type != "Transfer" && license != null && (license.Status != LicenseStatus.Active || license.UserId != notification.UserId)
                        || notification.Type is "Reminder" or "WinBack" && (license?.UpdatesThroughUtc == null
                            || !notification.Id.Contains(license.UpdatesThroughUtc.Value.ToString("yyyyMMdd"), StringComparison.Ordinal)))
                    { db.DeleteById<LicenseNotification>(notification.Id); continue; }
                    var preferences = db.SingleById<LicenseNotificationPreferences>(notification.UserId) ?? new();
                    if ((notification.Type == "Reminder" && !preferences.UpdateReminders) || (notification.Type == "Release" && !preferences.ReleaseAnnouncements)
                        || (notification.Type == "WinBack" && !preferences.WinBack))
                    { db.DeleteById<LicenseNotification>(notification.Id); continue; }
                    var user = await identity.Users.SingleOrDefaultAsync(x => x.Id == notification.UserId, stoppingToken);
                    if (user?.EmailConfirmed != true || string.IsNullOrWhiteSpace(user.Email)) continue;
                    db.UpdateOnly(() => new LicenseNotification { LastAttemptUtc = DateTime.UtcNow, Attempts = notification.Attempts + 1 }, x => x.Id == notification.Id);
                    try
                    {
                        using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = true, Credentials = new NetworkCredential(smtp.Username, smtp.Password) };
                        using var message = new MailMessage(new MailAddress(smtp.FromEmail, smtp.FromName), new MailAddress(smtp.DevToEmail ?? user.Email)) {
                            Subject = notification.Subject, Body = notification.Body + "\n\nMy licenses: " + site.BaseUrl.TrimEnd('/') + "/account\nNotification preferences: " + site.BaseUrl.TrimEnd('/') + "/account/settings",
                        };
                        await client.SendMailAsync(message, stoppingToken);
                        db.UpdateOnly(() => new LicenseNotification { SentAtUtc = DateTime.UtcNow }, x => x.Id == notification.Id);
                    }
                    catch (Exception) { logger.LogWarning("License notification delivery failed for notification {Id}.", notification.Id); }
                }
            }
            catch (Exception) { logger.LogWarning("License notification scheduler failed; inspect migrations and SMTP configuration."); }
        }
    }
}
