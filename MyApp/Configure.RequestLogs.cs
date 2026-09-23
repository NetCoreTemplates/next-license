using MyApp.ServiceModel;
using ServiceStack.Jobs;
using ServiceStack.Web;
[assembly: HostingStartup(typeof(MyApp.ConfigureRequestLogs))]
namespace MyApp;

public class ConfigureRequestLogs : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder.ConfigureServices(services => {
        var feature = new RequestLogsFeature {
            RequestLogger = new SqliteRequestLogger(),
            EnableRequestBodyTracking = false, EnableResponseTracking = false,
            EnableSessionTracking = false, EnableErrorTracking = false,
            RequestLogFilter = (_, entry) => Sanitize(entry),
        };
        feature.ExcludeRequestDtoTypes.AddRange([typeof(LicenseStripeWebhook), typeof(ActivateLicense),
            typeof(GetLicenseBlob), typeof(IssueLicense), typeof(ReissueLicense), typeof(ExtendUpdatesThrough), typeof(UpgradeToLifetimeUpdates)]);
        services.AddPlugin(feature);
        services.AddHostedService<LicenseRequestLogsWorker>();
    });
    public static void Sanitize(RequestLogEntry entry)
    {
        entry.Headers = null; entry.FormData = null; entry.RequestDto = null; entry.RequestBody = null;
        entry.ResponseDto = null; entry.ErrorResponse = null; entry.Session = null;
        entry.IpAddress = null; entry.ForwardedFor = null; entry.Referer = null;
        entry.AbsoluteUri = null; entry.Items = null; entry.SessionId = null;
    }
}
public class LicenseRequestLogsWorker(IRequestLogger requestLogger, ILogger<LicenseRequestLogsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ((SqliteRequestLogger)requestLogger).TickAsync(logger, stoppingToken);
    }
}
