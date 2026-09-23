using Microsoft.AspNetCore.Identity;
using MyApp.Data;
using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;
namespace MyApp.ServiceInterface;
public class LicenseTransferServices(UserManager<ApplicationUser> users) : Service
{
    public async Task<object> Post(TransferLicense request)
    {
        var user = GetSession().UserAuthId;
        var license = Db.Single<SoftwareLicense>(x => x.Id == request.Id && x.UserId == user && x.Status == LicenseStatus.Active)
            ?? throw HttpError.NotFound("Active license not found.");
        var recipient = await users.FindByEmailAsync(request.RecipientEmail);
        if (recipient?.EmailConfirmed != true || recipient.Id == user) throw HttpError.BadRequest("The recipient needs a different verified account.");
        using var tx = Db.OpenTransaction();
        var now = DateTime.UtcNow;
        if (Db.Exists<LicenseTransfer>(x => x.LicenseId == license.Id && x.AcceptedAtUtc == null && x.CancelledAtUtc == null && x.ExpiresAtUtc > now))
            throw HttpError.Conflict("A transfer is already pending.");
        var transfer = new LicenseTransfer { Id = Guid.NewGuid(), LicenseId = license.Id, FromUserId = user,
            ToUserId = recipient.Id, CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(7) };
        Db.Insert(transfer);
        Db.Insert(new LicenseNotification { Id = "transfer:" + transfer.Id, UserId = recipient.Id, LicenseId = license.Id, Type = "Transfer",
            Subject = "An Acme Studio license transfer is waiting", Body = "Sign in to My licenses to review and accept this transfer. It expires in seven days.", CreatedAtUtc = now });
        tx.Commit(); return transfer;
    }
    public object Get(ListLicenseTransfers request)
    {
        var user = GetSession().UserAuthId; var now = DateTime.UtcNow;
        return new LicenseTransfersResponse { Results = Db.Select<LicenseTransfer>(x => (x.FromUserId == user || x.ToUserId == user)
            && x.AcceptedAtUtc == null && x.CancelledAtUtc == null && x.ExpiresAtUtc > now) };
    }
    public void Post(CancelLicenseTransfer request)
    {
        var user = GetSession().UserAuthId;
        if (Db.UpdateOnly(() => new LicenseTransfer { CancelledAtUtc = DateTime.UtcNow }, x => x.Id == request.Id && x.FromUserId == user && x.AcceptedAtUtc == null) != 1)
            throw HttpError.NotFound("Pending transfer not found.");
    }
    public async Task Post(AcceptLicenseTransfer request)
    {
        var user = GetSession().UserAuthId;
        var recipient = await users.FindByIdAsync(user);
        if (recipient?.EmailConfirmed != true) throw HttpError.Forbidden("A verified account is required.");
        using var tx = Db.OpenTransaction();
        var now = DateTime.UtcNow;
        var transfer = Db.Single<LicenseTransfer>(x => x.Id == request.Id && x.ToUserId == user && x.CancelledAtUtc == null && x.AcceptedAtUtc == null && x.ExpiresAtUtc > now)
            ?? throw HttpError.NotFound("Pending transfer not found.");
        if (Db.UpdateOnly(() => new SoftwareLicense { UserId = user, ModifiedBy = user, ModifiedDate = now },
            x => x.Id == transfer.LicenseId && x.UserId == transfer.FromUserId && x.Status == LicenseStatus.Active) != 1)
            throw HttpError.Conflict("The license changed; request a new transfer.");
        // Old billing evidence stays with its buyer; the recipient receives license custody, not their invoices.
        Db.UpdateOnly(() => new LicenseTransfer { AcceptedAtUtc = now }, x => x.Id == transfer.Id);
        Db.Insert(new LicensingAuditEvent { Actor = user, Action = "AcceptLicenseTransfer", Subject = transfer.LicenseId.ToString(), OccurredAtUtc = now,
            Detail = System.Text.Json.JsonSerializer.Serialize(new { transfer.Id, transfer.FromUserId, transfer.ToUserId }) });
        tx.Commit();
    }
}
