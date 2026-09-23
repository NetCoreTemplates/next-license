using System.Security.Cryptography;
using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.OrmLite;
namespace MyApp.ServiceInterface;

public class ActivationServices(LicenseSigning signing, ActivationThrottle throttle) : Service
{
    public object Post(ActivateLicense request)
    {
        // Refresh needs only a bearer key or JWT, never device identity.
        string hash;
        try
        {
            if (!string.IsNullOrEmpty(request.Blob) && !string.IsNullOrEmpty(request.Key))
                throw HttpError.BadRequest("Supply either a short key or a signed license file.");
            var key = string.IsNullOrEmpty(request.Blob) ? request.Key
                : signing.ReadLicense(request.Blob).ShortKey;
            hash = signing.HashShortKey(key);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or CryptographicException)
        { throw HttpError.NotFound("License not found."); }
        var license = Db.Single<SoftwareLicense>(x => x.ShortKeyHash == hash) ?? throw HttpError.NotFound("License not found.");
        if (!throttle.Allow(license.Id)) throw new HttpError(429, "ActivationRateLimit", "Too many refresh requests for this license.");
        if (license.Status == LicenseStatus.Revoked) return new LicenseBlobResponse { Status = "Revoked" };
        return new LicenseBlobResponse { Blob = Db.Single<LicenseBlob>(x => x.LicenseId == license.Id && x.Version == license.BlobVersion)?.Blob };
    }
}
