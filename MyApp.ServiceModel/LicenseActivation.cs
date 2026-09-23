using ServiceStack;
namespace MyApp.ServiceModel;

[Route("/licensing/activate", "POST"), Route("/licensing/refresh", "POST")]
public class ActivateLicense : IPost, IReturn<LicenseBlobResponse>
{
    public string Key { get; set; } = "";
    public string? Blob { get; set; }
}
