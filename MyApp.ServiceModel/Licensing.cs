using ServiceStack;
using ServiceStack.DataAnnotations;
using MyApp.Licensing.Core;

namespace MyApp.ServiceModel;

public static class LicenseProduct
{
    public const string Id = "acme-studio";
}
public enum LicenseStatus { Active, Revoked }
public class SoftwareLicense : AuditBase
{
    [PrimaryKey] public Guid Id { get; set; }
    [System.Runtime.Serialization.IgnoreDataMember]
    [Index(Unique = true), StringLength(64)] public string ShortKeyHash { get; set; } = "";
    public string ShortKeySuffix { get; set; } = "";
    public string ProductId { get; set; } = LicenseProduct.Id;
    public Edition Edition { get; set; }
    public UpdateMode UpdateMode { get; set; }
    public DateTime? UpdatesThroughUtc { get; set; }
    public string LicenseeName { get; set; } = "";
    public string? LicenseeOrganization { get; set; }
    public int Seats { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public LicenseStatus Status { get; set; }
    [Index] public string UserId { get; set; } = "";
    public Guid OrderId { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    [StringLength(StringLengthAttribute.MaxText)] public string? RevokedReason { get; set; }
    public string SigningKeyId { get; set; } = "";
    public int BlobVersion { get; set; }
}
[CompositeIndex(nameof(LicenseId), nameof(Version), Unique = true)]
public class LicenseBlob : AuditBase
{
    [AutoIncrement] public long Id { get; set; }
    public Guid LicenseId { get; set; }
    public int Version { get; set; }
    [StringLength(StringLengthAttribute.MaxText)] public string Blob { get; set; } = "";
}
public class LicensingAuditEvent
{
    [AutoIncrement] public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string Subject { get; set; } = "";
    [StringLength(StringLengthAttribute.MaxText)] public string Detail { get; set; } = "";
}

[Route("/account/licenses", "GET"), ValidateIsAuthenticated]
public class GetAccountLicenses : IGet, IReturn<AccountLicensesResponse> { }
public class AccountLicensesResponse { public List<SoftwareLicense> Results { get; set; } = []; public ResponseStatus? ResponseStatus { get; set; } }
[Route("/account/licenses/{Id}/blob", "GET"), ValidateIsAuthenticated]
public class GetLicenseBlob : IGet, IReturn<LicenseBlobResponse> { public Guid Id { get; set; } }
public class LicenseBlobResponse { public string? Blob { get; set; } public string Status { get; set; } = "Active"; public ResponseStatus? ResponseStatus { get; set; } }
[ValidateIsAdmin]
public class IssueLicense : IPost, IReturn<LicenseBlobResponse>
{
    public string UserId { get; set; } = "";
    public string LicenseeName { get; set; } = "";
    public string? LicenseeOrganization { get; set; }
    public int Seats { get; set; } = 1;
    public Edition Edition { get; set; } = Edition.Pro;
    public UpdateMode UpdateMode { get; set; }
    public DateTime? UpdatesThroughUtc { get; set; }
}
[ValidateIsAdmin]
public class RevokeLicense : IPost, IReturnVoid { public Guid Id { get; set; } [ValidateNotEmpty] public string Reason { get; set; } = ""; }
