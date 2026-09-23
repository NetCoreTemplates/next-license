using ServiceStack;
namespace MyApp.ServiceModel;

[ValidateIsAdmin]
public class ExtendUpdatesThrough : IPost, IReturn<LicenseBlobResponse>
{
    public Guid Id { get; set; }
    public int ExpectedBlobVersion { get; set; }
    public DateTime UpdatesThroughUtc { get; set; }
    public string Reason { get; set; } = "";
}
[ValidateIsAdmin]
public class UpgradeToLifetimeUpdates : IPost, IReturn<LicenseBlobResponse>
{
    public Guid Id { get; set; }
    public int ExpectedBlobVersion { get; set; }
    public string Reason { get; set; } = "";
}
[ValidateIsAdmin]
public class ReissueLicense : IPost, IReturn<LicenseBlobResponse>
{
    public Guid Id { get; set; }
    public int ExpectedBlobVersion { get; set; }
    public bool RotateShortKey { get; set; }
    public string Reason { get; set; } = "";
}
