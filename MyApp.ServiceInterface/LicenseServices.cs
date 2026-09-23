using MyApp.Licensing.Core;
using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;

namespace MyApp.ServiceInterface;

public class LicenseServices(LicenseSigning signing) : Service
{
    public object Get(GetLicensePricing request) => new LicensePricingResponse {
        Results = Db.Select<PriceBook>(x => x.IsActive),
        Agreement = Db.Single(Db.From<LicenseAgreement>().Where(x => x.EffectiveAtUtc <= DateTime.UtcNow).OrderByDescending(x => x.EffectiveAtUtc)),
    };
    public object Get(GetAccountLicenses request) => new AccountLicensesResponse {
        Results = Db.Select<SoftwareLicense>(x => x.UserId == GetSession().UserAuthId),
    };
    public object Get(GetLicenseBlob request)
    {
        var userId = GetSession().UserAuthId;
        var license = Db.Single<SoftwareLicense>(x => x.Id == request.Id && x.UserId == userId)
            ?? throw HttpError.NotFound("License not found.");
        if (license.Status == LicenseStatus.Revoked) return new LicenseBlobResponse { Status = "Revoked" };
        var blob = Db.Single<LicenseBlob>(x => x.LicenseId == license.Id && x.Version == license.BlobVersion);
        return new LicenseBlobResponse { Blob = blob?.Blob };
    }
    public object Post(IssueLicense request)
    {
        _ = new PaidEntitlement(LicenseProduct.Id, request.Edition, request.UpdateMode, request.UpdatesThroughUtc);
        if (request.Seats is < 1 or > 1000000 || string.IsNullOrWhiteSpace(request.UserId)
            || string.IsNullOrWhiteSpace(request.LicenseeName) || request.LicenseeName.Length > 200
            || request.LicenseeOrganization?.Length > 200) throw HttpError.BadRequest("Invalid licensee or seat count.");
        var at = DateTime.UtcNow;
        var shortKey = ShortKey.Create();
        var license = new SoftwareLicense {
            Id = Guid.NewGuid(), ShortKeyHash = signing.HashShortKey(shortKey), ShortKeySuffix = shortKey[^4..],
            Edition = request.Edition, UpdateMode = request.UpdateMode, UpdatesThroughUtc = request.UpdatesThroughUtc,
            LicenseeName = request.LicenseeName, LicenseeOrganization = request.LicenseeOrganization,
            Seats = request.Seats, UserId = request.UserId, IssuedAtUtc = at, BlobVersion = 1,
            CreatedBy = GetSession().UserAuthId, ModifiedBy = GetSession().UserAuthId, CreatedDate = at, ModifiedDate = at,
        };
        using var tx = Db.OpenTransaction();
        var signed = signing.SignLicense(Db, license, shortKey, at);
        license.SigningKeyId = signed.KeyId;
        Db.Insert(license);
        Db.Insert(new LicenseBlob { LicenseId = license.Id, Version = 1, Blob = signed.Blob,
            CreatedDate = at, ModifiedDate = at, CreatedBy = license.CreatedBy, ModifiedBy = license.ModifiedBy });
        Audit("IssueLicense", license.Id.ToString(), "Administrative issuance");
        tx.Commit();
        return new LicenseBlobResponse { Blob = signed.Blob };
    }
    public void Post(RevokeLicense request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000) throw HttpError.BadRequest("A concise revocation reason is required.");
        using var tx = Db.OpenTransaction();
        var license = Db.SingleById<SoftwareLicense>(request.Id) ?? throw HttpError.NotFound("License not found.");
        if (license.Status != LicenseStatus.Revoked)
        {
            Db.UpdateOnly(() => new SoftwareLicense { Status = LicenseStatus.Revoked, RevokedAtUtc = DateTime.UtcNow,
                RevokedReason = request.Reason, ModifiedDate = DateTime.UtcNow, ModifiedBy = GetSession().UserAuthId }, x => x.Id == request.Id);
            Audit("RevokeLicense", request.Id.ToString(), request.Reason);
        }
        tx.Commit();
    }
    private void Audit(string action, string subject, string detail) => Db.Insert(new LicensingAuditEvent {
        OccurredAtUtc = DateTime.UtcNow, Actor = GetSession().UserAuthId, Action = action, Subject = subject, Detail = detail,
    });
}
