using System.Security.Cryptography;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using MyApp.Licensing.Core;
using NUnit.Framework;
namespace MyApp.Tests;
public class CustomerOrderTests
{
    [Test]
    public void Customer_order_uses_snapshot_amount_and_product_with_actionable_pending_status()
    {
        var order = new LicenseOrder { ExpectedAmountCents = 4900, Currency = "usd", Seats = 1, RequiresReview = true };
        var line = new OrderLine { Edition = Edition.Pro, UpdateMode = UpdateMode.ThroughDate, TermMonths = 12 };
        var result = CustomerOrderServices.Describe(order, line);
        Assert.That(result.ProductName, Is.EqualTo("Acme Studio Pro"));
        Assert.That(result.AmountCents, Is.EqualTo(4900));
        Assert.That(result.Message, Does.Contain("Check payment status"));
        order.FinalAmountCents = 3900; order.Status = OrderStatus.Paid;
        Assert.That(CustomerOrderServices.Describe(order, line).AmountCents, Is.EqualTo(3900));
    }
    [Test]
    public void Issuance_validation_requires_private_P256_key_and_short_key_secret()
    {
        Assert.Catch(() => new LicenseSigning(new()).ValidateLicenseIssuance());
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var config = new LicensingConfig { LicensePrivateKeyPem = key.ExportPkcs8PrivateKeyPem(), ShortKeySalt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
        Assert.DoesNotThrow(() => new LicenseSigning(config).ValidateLicenseIssuance());
        config.LicensePrivateKeyPem = key.ExportSubjectPublicKeyInfoPem();
        Assert.Catch(() => new LicenseSigning(config).ValidateLicenseIssuance());
    }
}
