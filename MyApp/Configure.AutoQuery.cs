[assembly: HostingStartup(typeof(MyApp.ConfigureAutoQuery))]
namespace MyApp;

public class ConfigureAutoQuery : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder.ConfigureServices(services => {
        services.AddPlugin(new AutoQueryDataFeature());
        services.AddPlugin(new AutoQueryFeature { MaxLimit = 1000 });
    });
}
