using ServiceStack;
using ServiceStack.DataAnnotations;
namespace MyApp.ServiceModel;
public class LicenseNotification
{
    [PrimaryKey] public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public Guid LicenseId { get; set; }
    public string Type { get; set; } = "";
    public string Subject { get; set; } = "";
    [StringLength(StringLengthAttribute.MaxText)] public string Body { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? LastAttemptUtc { get; set; }
    public int Attempts { get; set; }
}
public class LicenseNotificationPreferences
{
    [PrimaryKey] public string UserId { get; set; } = "";
    public bool UpdateReminders { get; set; } = true;
    public bool ReleaseAnnouncements { get; set; }
    public bool WinBack { get; set; }
}
[ValidateIsAuthenticated]
public class GetLicenseNotificationPreferences : IGet, IReturn<LicenseNotificationPreferences> { }
[ValidateIsAuthenticated]
public class SaveLicenseNotificationPreferences : IPost, IReturnVoid
{
    public bool UpdateReminders { get; set; }
    public bool ReleaseAnnouncements { get; set; }
    public bool WinBack { get; set; }
}
[Route("/account/licenses/{Id}/resend", "POST"), ValidateIsAuthenticated]
public class ResendLicense : IPost, IReturnVoid { public Guid Id { get; set; } }
