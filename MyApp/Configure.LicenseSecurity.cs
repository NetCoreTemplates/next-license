using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

[assembly: HostingStartup(typeof(MyApp.ConfigureLicenseSecurity))]
namespace MyApp;

public class ConfigureLicenseSecurity : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder.ConfigureServices(services => {
        services.AddRateLimiter(options => {
            options.RejectionStatusCode = 429;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => {
                var path = context.Request.Path;
                if (!path.StartsWithSegments("/licensing") && !path.StartsWithSegments("/updates")
                    && !path.Value!.EndsWith("/ActivateLicense", StringComparison.OrdinalIgnoreCase)
                    && !path.Value.EndsWith("/GetUpdateFeed", StringComparison.OrdinalIgnoreCase))
                    return RateLimitPartition.GetNoLimiter("public");
                return RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
            });
        });
        services.AddTransient<IStartupFilter, LicenseSecurityFilter>();
    });
}
public class LicenseSecurityFilter(IHostEnvironment environment) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app => {
        app.Use(async (context, continuation) => {
            var request = context.Request;
            var path = request.Path;
            var tooling = path.StartsWithSegments("/ui") || path.StartsWithSegments("/admin-ui");
            context.Response.OnStarting(() => {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                if (!environment.IsDevelopment()) context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; script-src 'self' 'unsafe-inline'" + (tooling ? " 'unsafe-eval'" : "") +
                    "; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
                if (path.StartsWithSegments("/account") || path.StartsWithSegments("/licensing") || path.StartsWithSegments("/api"))
                    context.Response.Headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            var mutation = request.Method is not ("GET" or "HEAD" or "OPTIONS");
            // Check all cookie-bearing dynamic mutations, including ServiceStack's alternate API routes.
            // A spoofed API-key header must not bypass a cookie-origin check.
            var exempt = path == "/stripe/webhook" || path == "/licensing/activate" || path == "/licensing/refresh";
            if (mutation && request.Headers.ContainsKey("Cookie") && !exempt && !path.StartsWithSegments("/Identity"))
            {
                var source = request.Headers.Origin.FirstOrDefault() ?? request.Headers.Referer.FirstOrDefault();
                if (!Uri.TryCreate(source, UriKind.Absolute, out var origin) || origin.Scheme != request.Scheme
                    || !string.Equals(origin.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new { responseStatus = new { errorCode = "CsrfValidationFailed", message = "A same-origin request is required." } });
                    return;
                }
            }
            await continuation();
        });
        app.UseRateLimiter(); next(app);
    };
}
