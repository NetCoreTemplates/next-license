using ServiceStack;
using MyApp.ServiceModel;
using ServiceStack.Data;
using ServiceStack.OrmLite;
namespace MyApp.ServiceInterface;
public class LicenseAccountDeletion(IDbConnectionFactory factory)
{
    public void RemovePersonalAccess(string userId)
    {
        using var db = factory.OpenDbConnection();
        using var tx = db.OpenTransaction();
        var anonymous = "deleted:" + Guid.NewGuid().ToString("N");
        foreach (var license in db.Select<SoftwareLicense>(x => x.UserId == userId))
        {
            db.UpdateOnly(() => new SoftwareLicense { UserId = anonymous, ModifiedBy = "account-deletion", ModifiedDate = DateTime.UtcNow }, x => x.Id == license.Id);
        }
        db.UpdateOnly(() => new LicenseOrder { UserId = anonymous }, x => x.UserId == userId);
        db.UpdateOnly(() => new LicenseAgreementAcceptance { UserId = anonymous, IpAddress = "" }, x => x.UserId == userId);
        db.Delete<LicenseTransfer>(x => x.FromUserId == userId || x.ToUserId == userId);
        db.Delete<LicenseNotification>(x => x.UserId == userId);
        db.DeleteById<LicenseNotificationPreferences>(userId);
        db.Insert(new LicensingAuditEvent { Actor = "account-deletion", Action = "PersonalAccessRemoved", Subject = anonymous,
            OccurredAtUtc = DateTime.UtcNow, Detail = "Identity link, preferences and pending transfers removed. Minimum commercial evidence retained." });
        tx.Commit();
    }
}
