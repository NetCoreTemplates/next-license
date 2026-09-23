using MyApp.ServiceInterface;
using System.Security.Cryptography;
[assembly: HostingStartup(typeof(MyApp.ConfigureLicensing))]
namespace MyApp;
public class ConfigureLicensing : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder.ConfigureServices((context, services) => {
        var licensing = context.Configuration.GetSection("Licensing").Get<LicensingConfig>() ?? new();
        var stripe = context.Configuration.GetSection("LicenseStripe").Get<LicenseStripeConfig>() ?? new();
        context.Configuration.GetSection("Stripe").Bind(stripe);
        if (licensing.GeneratePreviewKeys)
        {
            if (stripe.LiveMode || (!string.IsNullOrWhiteSpace(stripe.SecretKey)
                && !stripe.SecretKey.StartsWith("sk_test_", StringComparison.Ordinal)
                && !stripe.SecretKey.StartsWith("rk_test_", StringComparison.Ordinal)))
                throw new InvalidOperationException("Preview signing keys cannot be used with live Stripe checkout.");
            var keyDirectory = Path.Combine(context.HostingEnvironment.ContentRootPath, "App_Data", "preview-license-keys");
            Directory.CreateDirectory(keyDirectory);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(keyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var privateKeyPath = Path.Combine(keyDirectory, "license-private.pem");
            var saltPath = Path.Combine(keyDirectory, "short-key-salt.txt");
            if (!File.Exists(privateKeyPath) && !File.Exists(saltPath))
            {
                using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                WritePrivateFile(privateKeyPath, key.ExportECPrivateKeyPem());
                WritePrivateFile(saltPath, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            }
            if (!File.Exists(privateKeyPath) || !File.Exists(saltPath))
                throw new InvalidOperationException("Preview signing files are incomplete; restore the matching key and salt.");
            licensing.LicensePrivateKeyPem = File.ReadAllText(privateKeyPath);
            licensing.ShortKeySalt = File.ReadAllText(saltPath).Trim();
        }
        if (string.IsNullOrWhiteSpace(licensing.LicensePrivateKeyPem) && !string.IsNullOrWhiteSpace(licensing.LicensePrivateKeyPath))
            licensing.LicensePrivateKeyPem = File.ReadAllText(Path.GetFullPath(licensing.LicensePrivateKeyPath, context.HostingEnvironment.ContentRootPath));
        services.AddSingleton(licensing);
        services.AddMemoryCache();
        services.AddSingleton<LicenseSigning>();
        services.AddSingleton<LicenseAccountDeletion>();
        services.AddSingleton<ActivationThrottle>();
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

    private static void WritePrivateFile(string path, string contents)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using var file = new FileStream(path, options);
        using var writer = new StreamWriter(file);
        writer.Write(contents);
    }
}
