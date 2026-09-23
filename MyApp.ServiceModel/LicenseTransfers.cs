using ServiceStack;
using ServiceStack.DataAnnotations;
namespace MyApp.ServiceModel;
public class LicenseTransfer
{
    [PrimaryKey] public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public string FromUserId { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
}
[Route("/account/licenses/{Id}/transfer", "POST"), ValidateIsAuthenticated]
public class TransferLicense : IPost, IReturn<LicenseTransfer>
{
    public Guid Id { get; set; }
    public string RecipientEmail { get; set; } = "";
}
[ValidateIsAuthenticated]
public class ListLicenseTransfers : IGet, IReturn<LicenseTransfersResponse> { }
public class LicenseTransfersResponse { public List<LicenseTransfer> Results { get; set; } = []; public ResponseStatus? ResponseStatus { get; set; } }
[ValidateIsAuthenticated]
public class AcceptLicenseTransfer : IPost, IReturnVoid { public Guid Id { get; set; } }
[ValidateIsAuthenticated]
public class CancelLicenseTransfer : IPost, IReturnVoid { public Guid Id { get; set; } }
