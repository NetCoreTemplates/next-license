using ServiceStack.DataAnnotations;
namespace MyApp.ServiceModel;
public class LicenseCheckoutPolicy
{
    [PrimaryKey] public Guid OrderId { get; set; }
    public bool AllowPromotionCodes { get; set; }
    public bool AutomaticTax { get; set; }
}
public class LicenseOrderSettlement
{
    [PrimaryKey] public Guid OrderId { get; set; }
    public long SubtotalCents { get; set; }
    public long DiscountCents { get; set; }
    public long TaxCents { get; set; }
    public long TotalCents { get; set; }
    public string? StripePromotionCodeId { get; set; }
}
