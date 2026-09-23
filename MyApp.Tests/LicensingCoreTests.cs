using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyApp.Licensing;
using MyApp.Licensing.Core;
using NUnit.Framework;

namespace MyApp.Tests;

public class LicensingCoreTests
{
    private static readonly DateTime Cutoff = new(2027, 9, 20, 0, 0, 0, DateTimeKind.Utc);
    [TestCase(-1, 100, Edition.Pro, UpdateMode.ThroughDate, true)]
    [TestCase(0, 100, Edition.Pro, UpdateMode.ThroughDate, true)]
    [TestCase(1, 100, Edition.Pro, UpdateMode.ThroughDate, false)]
    [TestCase(-1, 2000, Edition.Pro, UpdateMode.ThroughDate, true)]
    [TestCase(1, 100, Edition.Pro, UpdateMode.Lifetime, true)]
    [TestCase(-1, 100, Edition.Free, UpdateMode.Lifetime, false)]
    [TestCase(1, 0, Edition.Pro, UpdateMode.Lifetime, false)]
    public void Paid_feature_matrix(int featureDays, int buildDays, Edition edition, UpdateMode mode, bool expected)
    {
        var license = new PaidEntitlement("acme-studio", edition, mode, mode == UpdateMode.ThroughDate ? Cutoff : null);
        var feature = new Feature("pro.export", Edition.Pro, Cutoff.AddDays(featureDays));
        // No current-time input exists: the same signed facts are unchanged at any wall clock.
        Assert.That(FeatureCutoff.Has(license, "acme-studio", Cutoff.AddDays(buildDays), feature), Is.EqualTo(expected));
    }
    [Test]
    public void Beta_features_have_no_entitlement_until_stable()
    {
        var license = new PaidEntitlement("acme-studio", Edition.Pro, UpdateMode.Lifetime, null);
        Assert.That(FeatureCutoff.Has(license, "acme-studio", Cutoff, new Feature("beta", Edition.Pro, null)), Is.False);
        Assert.That(FeatureCutoff.Has(license, "other-product", Cutoff, new Feature("pro", Edition.Pro, Cutoff)), Is.False);
    }
    [Test]
    public void Invalid_policy_combinations_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new PaidEntitlement("acme-studio", Edition.Pro, UpdateMode.Lifetime, Cutoff));
        Assert.Throws<ArgumentException>(() => new PaidEntitlement("acme-studio", Edition.Pro, UpdateMode.ThroughDate, null));
        Assert.Throws<ArgumentException>(() => new PaidEntitlement("acme-studio", Edition.Pro, UpdateMode.ThroughDate, DateTime.SpecifyKind(Cutoff, DateTimeKind.Local)));
    }
    [TestCase("2024-01-31", "2024-01-01", 1, "2024-02-29")]
    [TestCase("2024-02-29", "2024-02-29", 12, "2025-02-28")]
    [TestCase("2024-01-31", "2025-03-31", 1, "2025-04-30")]
    public void Renewal_calendar_boundaries(string cutoff, string paid, int months, string expected) =>
        Assert.That(RenewalPolicy.Extend(Date(cutoff), Date(paid), months), Is.EqualTo(Date(expected)));
    private static DateTime Date(string s) => DateTime.SpecifyKind(DateTime.Parse(s), DateTimeKind.Utc);

    [Test]
    public void Keys_reject_typos_and_use_secret_hashes()
    {
        var key = ShortKey.Create();
        Assert.That(ShortKey.Normalize(key.ToLowerInvariant()), Has.Length.EqualTo(36));
        Assert.Throws<ArgumentException>(() => ShortKey.Normalize(key[..^1]));
        Assert.That(ShortKey.Hash(key, new byte[32]), Is.Not.EqualTo(ShortKey.Hash(key, Enumerable.Repeat((byte)1, 32).ToArray())));
    }
    [Test]
    public void Master_key_wrapper_authenticates_key_identity_and_requires_recovery_key()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var master = RandomNumberGenerator.GetBytes(32);
        var wrapped = DocumentSigner.Wrap(key, master, "leaf");
        using var recovered = DocumentSigner.Unwrap(wrapped, master, "leaf");
        Assert.That(recovered.ExportSubjectPublicKeyInfo(), Is.EqualTo(key.ExportSubjectPublicKeyInfo()));
        Assert.Throws<AuthenticationTagMismatchException>(() => DocumentSigner.Unwrap(wrapped, master, "other"));
        Assert.Throws<AuthenticationTagMismatchException>(() => DocumentSigner.Unwrap(wrapped, RandomNumberGenerator.GetBytes(32), "leaf"));
    }
}
