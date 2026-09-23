using ServiceStack.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using MyApp.Data;

[assembly: HostingStartup(typeof(MyApp.ConfigureAuth))]

namespace MyApp;

public class ConfigureAuth : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services => {
            services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options => {
                options.LoginPath = "/signin";
                options.AccessDeniedPath = "/forbidden";
                options.Events.OnRedirectToLogin = context => ApiOrRedirect(context, 401, "Sign in to view your account.");
                options.Events.OnRedirectToAccessDenied = context => ApiOrRedirect(context, 403, "You do not have access to this operation.");
            });
            services.AddPlugin(new AuthFeature(IdentityAuth.For<ApplicationUser>(options => {
                options.SessionFactory = () => new CustomUserSession();
                options.CredentialsAuth();
                options.AdminUsersFeature();
            })));
        });
    private static Task ApiOrRedirect(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context, int status, string message)
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments("/api") || request.Path.StartsWithSegments("/json")
            || request.Headers.Accept.Any(x => x?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            context.Response.StatusCode = status;
            return context.Response.WriteAsJsonAsync(new { responseStatus = new {
                errorCode = status == 401 ? "Unauthorized" : "Forbidden", message,
            } });
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}