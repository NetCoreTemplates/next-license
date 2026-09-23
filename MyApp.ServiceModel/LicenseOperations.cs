using ServiceStack;
using MyApp.Licensing.Core;
namespace MyApp.ServiceModel;

[ValidateIsAdmin]
public class GetLicensingDashboard : IGet, IReturn<LicensingDashboardResponse> { }
public class LicensingDashboardResponse
{
    public long ActiveLicenses { get; set; }
    public long OrdersRequiringReview { get; set; }
    public long PendingStripeEvents { get; set; }
    public List<PriceBook> Prices { get; set; } = [];
    public List<LicenseAgreement> Agreements { get; set; } = [];
    public ResponseStatus? ResponseStatus { get; set; }
}
[ValidateIsAdmin]
public class PublishLicenseAgreement : IPost, IReturn<LicenseAgreement>
{
    public string Version { get; set; } = "";
    public string BodyMarkdown { get; set; } = "";
}
[ValidateIsAdmin]
public class SearchLicenses : IGet, IReturn<AccountLicensesResponse> { public string? Query { get; set; } public string? UserId { get; set; } public string? KeySuffix { get; set; } public int Skip { get; set; } }
[ValidateIsAdmin]
public class SearchOrders : IGet, IReturn<AccountOrdersResponse> { public string? Query { get; set; } public string? UserId { get; set; } public bool? RequiresReview { get; set; } public int Skip { get; set; } }
[ValidateIsAdmin]
public class RetryLicenseStripeEvent : IPost, IReturnVoid { public string Id { get; set; } = ""; }
