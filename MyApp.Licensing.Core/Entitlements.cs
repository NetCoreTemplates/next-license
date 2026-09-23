using System;
using System.Collections.Generic;

namespace MyApp.Licensing.Core;

public enum Edition { Free = 0, Pro = 10, Enterprise = 20 }
public enum UpdateMode { ThroughDate, Lifetime }
public enum ReleaseChannel { Stable, Beta }

/// <summary>Paid entitlement contains no use-expiry and never consults a clock.</summary>
public sealed class PaidEntitlement
{
    public string ProductId { get; }
    public Edition Edition { get; }
    public UpdateMode UpdateMode { get; }
    public DateTime? UpdatesThroughUtc { get; }

    public PaidEntitlement(string productId, Edition edition, UpdateMode updateMode, DateTime? updatesThroughUtc)
    {
        if (string.IsNullOrWhiteSpace(productId) || productId.Length > 80) throw new ArgumentException("Invalid product.");
        if (!Enum.IsDefined(typeof(Edition), edition) || !Enum.IsDefined(typeof(UpdateMode), updateMode))
            throw new ArgumentException("Unknown entitlement policy.");
        if ((updateMode == UpdateMode.ThroughDate) != updatesThroughUtc.HasValue)
            throw new ArgumentException("Dated entitlements require a cutoff; lifetime entitlements must omit it.");
        if (updatesThroughUtc.HasValue) Utc.Require(updatesThroughUtc.Value);
        ProductId = productId;
        Edition = edition;
        UpdateMode = updateMode;
        UpdatesThroughUtc = updatesThroughUtc;
    }
}

public sealed class Feature
{
    public string Key { get; }
    public Edition MinimumEdition { get; }
    public DateTime? EntitlementAtUtc { get; }
    public Feature(string key, Edition minimumEdition, DateTime? entitlementAtUtc)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new ArgumentException("Invalid feature key.");
        if (!Enum.IsDefined(typeof(Edition), minimumEdition)) throw new ArgumentException("Unknown edition.");
        if (entitlementAtUtc.HasValue) Utc.Require(entitlementAtUtc.Value);
        Key = key;
        MinimumEdition = minimumEdition;
        EntitlementAtUtc = entitlementAtUtc;
    }
}

public static class FeatureCutoff
{
    public static bool Has(PaidEntitlement license, string buildProductId, DateTime buildPublishedAtUtc, Feature feature)
    {
        Utc.Require(buildPublishedAtUtc);
        return license.ProductId == buildProductId
            && license.Edition >= feature.MinimumEdition
            && feature.EntitlementAtUtc.HasValue
            && buildPublishedAtUtc >= feature.EntitlementAtUtc.Value
            && (license.UpdateMode == UpdateMode.Lifetime || feature.EntitlementAtUtc.Value <= license.UpdatesThroughUtc!.Value);
    }
}

public static class RenewalPolicy
{
    public static DateTime Extend(DateTime currentUpdatesThroughUtc, DateTime paidAtUtc, int months)
    {
        Utc.Require(currentUpdatesThroughUtc);
        Utc.Require(paidAtUtc);
        if (months < 1 || months > 120) throw new ArgumentOutOfRangeException(nameof(months));
        return (currentUpdatesThroughUtc > paidAtUtc ? currentUpdatesThroughUtc : paidAtUtc).AddMonths(months);
    }
    public static bool DiscountEligible(DateTime cutoffUtc, DateTime atUtc, int graceDays = 60)
    {
        Utc.Require(cutoffUtc);
        Utc.Require(atUtc);
        if (graceDays < 0 || graceDays > 365) throw new ArgumentOutOfRangeException(nameof(graceDays));
        return atUtc <= cutoffUtc.AddDays(graceDays);
    }
}

public static class Utc
{
    public static void Require(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("An explicit UTC instant is required.");
    }
}
