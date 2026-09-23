using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using MyApp.Data;
using MyApp.ServiceInterface;

AppHost.RegisterKey();

// Local developer secrets; process environment takes precedence. Never log values.
foreach (var envPath in new[] { Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"), Path.Combine(Directory.GetCurrentDirectory(), ".env") })
    if (File.Exists(envPath)) foreach (var line in File.ReadLines(envPath)) {
        var text = line.Trim();
        if (text.Length == 0 || text.StartsWith('#')) continue;
        var split = text.IndexOf('=');
        if (split < 1) continue;
        var key = text[..split].Trim();
        if (key.StartsWith("export ")) key = key[7..].Trim();
        if (Environment.GetEnvironmentVariable(key) == null)
            Environment.SetEnvironmentVariable(key, text[(split + 1)..].Trim().Trim('"', '\''));
    }
var runtimeSettings = Environment.GetEnvironmentVariable("APPSETTINGS_JSON_BASE64");
if (!string.IsNullOrWhiteSpace(runtimeSettings))
{
    using var document = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(runtimeSettings));
    ApplySettings(document.RootElement, "");
}
var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddAuthorization();
services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("App_Data"));

services.AddDatabaseDeveloperPageExceptionFilter();

services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

services.AddRazorPages();

if (builder.Configuration.GetSection("SmtpConfig").Exists())
    services.AddSingleton<IEmailSender<ApplicationUser>, EmailSender>();
else
    services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
// Uncomment to send emails with SMTP, configure SMTP with "SmtpConfig" in appsettings.json
// services.AddSingleton<IEmailSender<ApplicationUser>, EmailSender>();
services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AdditionalUserClaimsPrincipalFactory>();

// Register all services
services.AddServiceStack(typeof(MyServices).Assembly);

var app = builder.Build();
var nodeProxy = new NodeProxy(Environment.GetEnvironmentVariable("NEXT_DEV_SERVER_URL") ?? "http://127.0.0.1:3000") {
    Log = app.Logger
};

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();

    app.MapNotFoundToNode(nodeProxy);
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapCleanUrls();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapRazorPages();

app.UseServiceStack(new AppHost(), options => {
    options.MapEndpoints();
});

// Proxy HMR WebSocket and fallback routes to Node dev server in Development
if (app.Environment.IsDevelopment())
{
    app.RunNodeProcess(nodeProxy, "../MyApp.Client"); // Start Node if not running
    app.UseWebSockets();
    app.MapNextHmr(nodeProxy);
    app.MapFallbackToNode(nodeProxy); // Fallback to Node dev server in development
}
else
{
    app.MapFallbackToFile("index.html"); // Fallback to index.html in production (MyApp.Client/dist > wwwroot)
}

app.Run();

// Preserve explicit process environment overrides, including deployment destinations.
static void ApplySettings(System.Text.Json.JsonElement value, string path)
{
    if (value.ValueKind == System.Text.Json.JsonValueKind.Object)
        foreach (var property in value.EnumerateObject())
            ApplySettings(property.Value, path.Length == 0 ? property.Name : path + "__" + property.Name);
    else if (value.ValueKind == System.Text.Json.JsonValueKind.Array)
    {
        var i = 0;
        foreach (var item in value.EnumerateArray()) ApplySettings(item, path + "__" + i++);
    }
    else if (value.ValueKind != System.Text.Json.JsonValueKind.Null && Environment.GetEnvironmentVariable(path) == null)
        Environment.SetEnvironmentVariable(path, value.ToString());
}
