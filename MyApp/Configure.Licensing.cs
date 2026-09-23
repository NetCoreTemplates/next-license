using MyApp.ServiceInterface;
[assembly: HostingStartup(typeof(MyApp.ConfigureLicensing))]
namespace MyApp;
public class ConfigureLicensing : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder.ConfigureServices((context, services) => {
        var licensing = context.Configuration.GetSection("Licensing").Get<LicensingConfig>() ?? new();
        if (string.IsNullOrWhiteSpace(licensing.LicensePrivateKeyPem) && !string.IsNullOrWhiteSpace(licensing.LicensePrivateKeyPath))
            licensing.LicensePrivateKeyPem = File.ReadAllText(Path.GetFullPath(licensing.LicensePrivateKeyPath, context.HostingEnvironment.ContentRootPath));
        services.AddSingleton(licensing);
        services.AddMemoryCache();
        services.AddSingleton<LicenseSigning>();
        services.AddSingleton<LicenseAccountDeletion>();
        services.AddSingleton<ActivationThrottle>();
        var stripe = context.Configuration.GetSection("LicenseStripe").Get<LicenseStripeConfig>() ?? new();
        context.Configuration.GetSection("Stripe").Bind(stripe);
        services.AddSingleton(stripe);
        services.AddSingleton<LicenseStripeGateway>();
        services.AddSingleton<LicenseFulfillment>();
        services.AddSingleton<ILicenseCheckoutReader>(sp => sp.GetRequiredService<LicenseStripeGateway>());
        services.AddSingleton<ILicenseRefundGateway>(sp => sp.GetRequiredService<LicenseStripeGateway>());
        services.AddSingleton<LicenseCheckoutRecovery>();
        services.AddHostedService<LicenseCheckoutReconciliation>();
        services.AddHostedService<LicenseInboxRecovery>();
        services.AddHostedService<LicenseNotificationWorker>();
    });
}
