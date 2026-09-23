using MyApp.Licensing.Core;
using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;
namespace MyApp.ServiceInterface;

public class LicenseMaintenanceServices(LicenseSigning signing) : Service
{
    public object Post(ExtendUpdatesThrough request) => Change(request.Id, request.ExpectedBlobVersion, request.Reason,
        "ExtendUpdatesThrough", false, license => {
            if (request.UpdatesThroughUtc.Kind != DateTimeKind.Utc || license.UpdateMode != UpdateMode.ThroughDate
                || request.UpdatesThroughUtc <= license.UpdatesThroughUtc)
                throw HttpError.BadRequest("Supply a later UTC cutoff for a dated license.");
            license.UpdatesThroughUtc = request.UpdatesThroughUtc;
        });

    public object Post(UpgradeToLifetimeUpdates request) => Change(request.Id, request.ExpectedBlobVersion, request.Reason,
        "UpgradeToLifetimeUpdates", false, license => {
            if (license.UpdateMode == UpdateMode.Lifetime) throw HttpError.Conflict("License already has lifetime updates.");
            license.UpdateMode = UpdateMode.Lifetime; license.UpdatesThroughUtc = null;
        });

    public object Post(ReissueLicense request) => Change(request.Id, request.ExpectedBlobVersion, request.Reason,
        "ReissueLicense", request.RotateShortKey, _ => { });

    private LicenseBlobResponse Change(Guid id, int expectedVersion, string reason, string action, bool rotate, Action<SoftwareLicense> change)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000) throw HttpError.BadRequest("A concise support reason is required.");
        using var tx = Db.OpenTransaction();
        var license = Db.SingleById<SoftwareLicense>(id) ?? throw HttpError.NotFound("License not found.");
        if (license.Status != LicenseStatus.Active || license.BlobVersion != expectedVersion)
            throw HttpError.Conflict("License changed or is revoked. Reload it before applying this action.");
        var stored = Db.Single<LicenseBlob>(x => x.LicenseId == id && x.Version == expectedVersion)
            ?? throw new InvalidOperationException("Current blob missing.");
        var previous = signing.ReadLicense(stored.Blob);
        if (previous.Id != id.ToString("D") || signing.HashShortKey(previous.ShortKey) != license.ShortKeyHash)
            throw new InvalidOperationException("Current blob identity does not match the license.");
        var previousCutoff = license.UpdatesThroughUtc;
        change(license);
        var shortKey = rotate ? ShortKey.Create() : previous.ShortKey;
        var now = DateTime.UtcNow;
        license.ShortKeyHash = signing.HashShortKey(shortKey); license.ShortKeySuffix = shortKey[^4..];
        license.BlobVersion++; license.ModifiedDate = now; license.ModifiedBy = GetSession().UserAuthId;
        var signed = signing.SignLicense(Db, license, shortKey, now);
        license.SigningKeyId = signed.KeyId;
        Db.Update(license);
        Db.Insert(new LicenseBlob { LicenseId = id, Version = license.BlobVersion, Blob = signed.Blob,
            CreatedDate = now, ModifiedDate = now, CreatedBy = license.ModifiedBy, ModifiedBy = license.ModifiedBy });
        Db.Insert(new LicensingAuditEvent { Actor = license.ModifiedBy, Action = action, Subject = id.ToString(), OccurredAtUtc = now,
            Detail = System.Text.Json.JsonSerializer.Serialize(new { reason, previousCutoff, license.UpdatesThroughUtc,
                license.UpdateMode, rotate, license.BlobVersion }) });
        tx.Commit();
        return new LicenseBlobResponse { Blob = signed.Blob };
    }
}
