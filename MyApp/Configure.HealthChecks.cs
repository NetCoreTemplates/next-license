using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

[assembly: HostingStartup(typeof(MyApp.HealthChecks))]

namespace MyApp;

public class HealthChecks : IHostingStartup
{
    public class HealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
        {
            // Perform health check logic here
            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddHealthChecks()
                .AddCheck<HealthCheck>("HealthCheck", tags: ["liveness"])
                .AddCheck<LicenseReadiness>("Licensing", tags: ["readiness"]);

            services.AddTransient<IStartupFilter, StartupFilter>();
        });
    }
    
    public class StartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app => {
                app.UseHealthChecks("/up", new HealthCheckOptions { Predicate = check => check.Tags.Contains("liveness") });
                app.UseHealthChecks("/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("readiness") });
                next(app);
            };
    }
}
