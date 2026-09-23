using ServiceStack;
namespace MyApp.ServiceModel;
[ValidateIsAdmin] public class GetSoftwareSetup : IGet, IReturn<SoftwareSetupResponse> { }
public class SoftwareSetupResponse {
    public bool StripeConfigured { get; set; }
    public bool WebhookConfigured { get; set; }
    public bool SigningConfigured { get; set; }
    public bool LiveMode { get; set; }
    public string? Repository { get; set; }
    public ResponseStatus? ResponseStatus { get; set; }
}
[ValidateIsAdmin] public class CreateMissingStripe : IPost, IReturn<LicensePricingResponse> { }
[ValidateIsAdmin] public class ApproveSoftwarePrice : IPost, IReturn<PriceBook> {
    public string Sku { get; set; } = "";
    public bool Approved { get; set; }
}
public class GetGitHubDownloads : IGet, IReturn<GitHubDownloadsResponse> { }
public class GitHubDownloadsResponse {
    public string? Repository { get; set; }
    public List<GitHubDownloadRelease> Results { get; set; } = [];
    public ResponseStatus? ResponseStatus { get; set; }
}
public class GitHubDownloadRelease {
    public string Name { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Url { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime PublishedAt { get; set; }
    public bool Prerelease { get; set; }
    public List<GitHubDownloadAsset> Assets { get; set; } = [];
}
public class GitHubDownloadAsset {
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public long Size { get; set; }
}
[ValidateIsAdmin] public class SetSoftwarePrice : IPost, IReturn<PriceBook> {
    public string Sku { get; set; } = "";
    public long UnitAmountCents { get; set; }
    public string Currency { get; set; } = "usd";
}
[ValidateIsAdmin] public class SearchLicenseCustomers : IGet, IReturn<LicenseCustomersResponse> {
    public string? Query { get; set; }
    public int Skip { get; set; }
}
public class LicenseCustomersResponse {
    public List<LicenseCustomer> Results { get; set; } = [];
    public ResponseStatus? ResponseStatus { get; set; }
}
public class LicenseCustomer {
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}
