using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using ServiceStack.Data;
using ServiceStack.OrmLite;
namespace MyApp;

public class LicenseReadiness(IDbConnectionFactory factory, LicensingConfig config, LicenseStripeConfig stripe,
    LicenseSigning signing) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        try
        {
            using var db = factory.OpenDbConnection();
            if (!db.TableExists<LicenseOrderSettlement>() || !db.TableExists<SoftwareLicense>() || !db.TableExists<LicenseCheckoutPolicy>() || !db.TableExists<LicenseTransfer>() || !db.TableExists<LicenseNotification>())
                return Result("Licensing database migrations are pending.");
            if (string.IsNullOrWhiteSpace(stripe.SecretKey) || string.IsNullOrWhiteSpace(stripe.WebhookSecret)
                || !Uri.TryCreate(stripe.BaseUrl, UriKind.Absolute, out var url) || url.Scheme != "https")
                return Result("Stripe checkout configuration is incomplete.");
            _ = signing.HashShortKey(MyApp.Licensing.Core.ShortKey.Create());
            var now = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(config.LicensePrivateKeyPem)) return Result("Configure the JWT license signing key.");
            using (var jwtKey = System.Security.Cryptography.ECDsa.Create()) {
                jwtKey.ImportFromPem(config.LicensePrivateKeyPem);
                if (jwtKey.ExportParameters(false).Curve.Oid.Value != System.Security.Cryptography.ECCurve.NamedCurves.nistP256.Oid.Value)
                    return Result("The JWT signing key must use P-256.");
            }
            if (!db.Exists<LicenseAgreement>(x => x.EffectiveAtUtc <= now) || !db.Exists<PriceBook>(x => x.IsActive))
                return Result("An effective agreement and active prices are required.");
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception) { return Result("Licensing dependencies could not be validated."); }
    }
    private static Task<HealthCheckResult> Result(string message) => Task.FromResult(HealthCheckResult.Unhealthy(message));
}
