using ServiceStack;
using ServiceStack.DataAnnotations;
using MyApp.Licensing.Core;
namespace MyApp.ServiceModel;

public enum OrderKind { NewPurchase, Renewal, LifetimeUpgrade, EditionUpgrade }
public enum OrderStatus { Pending, Paid, Failed, PartiallyRefunded, Refunded }
public class LicenseOrder : AuditBase
{
    [PrimaryKey] public Guid Id { get; set; }
    [Index(Unique = true)] public string OrderNumber { get; set; } = "";
    public string UserId { get; set; } = "";
    public OrderKind Kind { get; set; }
    public Guid? LicenseId { get; set; }
    public int Seats { get; set; }
    public string LicenseeName { get; set; } = "";
    public string? LicenseeOrganization { get; set; }
    public string Currency { get; set; } = "usd";
    public long ExpectedAmountCents { get; set; }
    public long? FinalAmountCents { get; set; }
    public string AgreementVersion { get; set; } = "";
    public DateTime AgreementAcceptedAtUtc { get; set; }
    [StringLength(255)] public string? StripeCheckoutSessionId { get; set; }
    [StringLength(255)] public string? StripePaymentIntentId { get; set; }
    public string? StripeInvoiceId { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public bool RequiresReview { get; set; }
}
public class OrderLine : AuditBase
{
    [AutoIncrement] public long Id { get; set; }
    [Index(Unique = true)] public Guid OrderId { get; set; }
    public string Sku { get; set; } = "";
    public string Description { get; set; } = "";
    public Edition Edition { get; set; }
    public UpdateMode UpdateMode { get; set; }
    public int TermMonths { get; set; }
    public int Quantity { get; set; }
    public long UnitAmountCents { get; set; }
    public string StripePriceId { get; set; } = "";
}
public class PriceBook : AuditBase
{
    [PrimaryKey] public string Sku { get; set; } = "";
    public Edition Edition { get; set; }
    public UpdateMode UpdateMode { get; set; }
    public int TermMonths { get; set; }
    public string Currency { get; set; } = "usd";
    public long UnitAmountCents { get; set; }
    public string StripePriceId { get; set; } = "";
    public bool IsActive { get; set; }
}
public class LicenseAgreement : AuditBase
{
    [PrimaryKey] public string Version { get; set; } = "";
    public DateTime EffectiveAtUtc { get; set; }
    [StringLength(StringLengthAttribute.MaxText)] public string BodyMarkdown { get; set; } = "";
}
public class LicenseAgreementAcceptance
{
    [PrimaryKey] public Guid OrderId { get; set; }
    public string UserId { get; set; } = "";
    public string AgreementVersion { get; set; } = "";
    public DateTime AcceptedAtUtc { get; set; }
    public string IpAddress { get; set; } = "";
}
public class StripeEventInbox
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    [StringLength(StringLengthAttribute.MaxText)] public string Payload { get; set; } = "";
    [StringLength(StringLengthAttribute.MaxText)] public string? LastError { get; set; }
    public int Attempts { get; set; }
}
public class LicenseRefund
{
    [PrimaryKey] public string StripeRefundId { get; set; } = "";
    public Guid OrderId { get; set; }
    public long AmountCents { get; set; }
    public string Status { get; set; } = "";
    [StringLength(StringLengthAttribute.MaxText)] public string? Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
public class LicenseDispute
{
    [PrimaryKey] public string StripeDisputeId { get; set; } = "";
    public Guid OrderId { get; set; }
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "";
    public string Status { get; set; } = "";
    [StringLength(StringLengthAttribute.MaxText)] public string Reason { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool Reviewed { get; set; }
    public string? ReviewedBy { get; set; }
    [StringLength(StringLengthAttribute.MaxText)] public string? ReviewNotes { get; set; }
}
[ValidateIsAdmin]
public class ListLicenseDisputes : IGet, IReturn<LicenseDisputesResponse> { public bool? Reviewed { get; set; } public int Skip { get; set; } }
public class LicenseDisputesResponse { public List<LicenseDispute> Results { get; set; } = []; public ResponseStatus? ResponseStatus { get; set; } }
[ValidateIsAdmin]
public class ReviewLicenseDispute : IPost, IReturnVoid { public string Id { get; set; } = ""; public string Notes { get; set; } = ""; }
[ValidateIsAdmin]
public class RefundLicenseOrder : IPost, IReturn<RefundLicenseOrderResponse>
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public long AmountCents { get; set; }
    [StringLength(StringLengthAttribute.MaxText)] public string Reason { get; set; } = "";
}
public class RefundLicenseOrderResponse { public LicenseRefund? Result { get; set; } public ResponseStatus? ResponseStatus { get; set; } }
public class LicenseRefundRequest
{
    [PrimaryKey] public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public long AmountCents { get; set; }
    [StringLength(StringLengthAttribute.MaxText)] public string Reason { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public string? StripeRefundId { get; set; }
}
[ValidateIsAdmin]
public class ReviewLicenseOrder : IPost, IReturnVoid { public Guid Id { get; set; } public string Notes { get; set; } = ""; }
[Route("/licensing/pricing", "GET")]
public class GetLicensePricing : IGet, IReturn<LicensePricingResponse> { }
public class LicensePricingResponse { public List<PriceBook> Results { get; set; } = []; public LicenseAgreement? Agreement { get; set; } public ResponseStatus? ResponseStatus { get; set; } }
[Route("/checkout", "POST"), ValidateIsAuthenticated]
public class CreateLicenseCheckout : IPost, IReturn<LicenseCheckoutResponse>
{
    public string Sku { get; set; } = "";
    public Guid? LicenseId { get; set; }
    public int Seats { get; set; } = 1;
    public string LicenseeName { get; set; } = "";
    public string? LicenseeOrganization { get; set; }
    public string AgreementVersion { get; set; } = "";
    public bool AcceptAgreement { get; set; }
}
public class LicenseCheckoutResponse { public Guid OrderId { get; set; } public string Url { get; set; } = ""; public ResponseStatus? ResponseStatus { get; set; } }
[Route("/account/orders", "GET"), ValidateIsAuthenticated]
public class GetAccountOrders : IGet, IReturn<CustomerOrdersResponse> { }
public class AccountOrdersResponse { public List<LicenseOrder> Results { get; set; } = []; public ResponseStatus? ResponseStatus { get; set; } }

[Route("/account/orders/{Id}/invoice", "GET"), ValidateIsAuthenticated]
public class GetOrderInvoice : IGet, IReturn<OrderInvoiceResponse> { public Guid Id { get; set; } }
public class OrderInvoiceResponse { public string? Url { get; set; } public ResponseStatus? ResponseStatus { get; set; } }
[Route("/stripe/webhook", "POST")]
public class LicenseStripeWebhook : IPost, IReturn<EmptyResponse> { }

public class CustomerOrder
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = "";
    public string Description { get; set; } = "";
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "usd";
    public int Seats { get; set; }
    public OrderStatus Status { get; set; }
    public bool RequiresReview { get; set; }
    public bool HasInvoice { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Message { get; set; } = "";
}
public class CustomerOrdersResponse
{
    public List<CustomerOrder> Results { get; set; } = [];
    public ResponseStatus? ResponseStatus { get; set; }
}
[ValidateIsAuthenticated]
public class RefreshAccountOrder : IPost, IReturn<CustomerOrder>
{
    public Guid Id { get; set; }
}
