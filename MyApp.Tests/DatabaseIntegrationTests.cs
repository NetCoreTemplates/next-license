using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.Migrations;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using NUnit.Framework;
using ServiceStack.OrmLite;

namespace MyApp.Tests;

[Category("Database"), NonParallelizable]
public class DatabaseIntegrationTests
{
    [Test]
    public void Baseline_recreates_all_tables_and_preserves_unique_provider_ids()
    {
        var factory = DatabaseTestRun.CreateFactory();
        using var db = factory.OpenDbConnection();
        var migration = new Migration1000 { Db = db };
        for (var run = 0; run < 2; run++)
        {
            migration.Up();
            Assert.That(db.Select<PriceBook>(), Has.Count.EqualTo(5));
            var triggerSql = DatabaseTestRun.Provider switch {
                "postgres" => "SELECT count(*) FROM information_schema.triggers WHERE trigger_schema='public'",
                "mysql" => "SELECT count(*) FROM information_schema.triggers WHERE trigger_schema=DATABASE()",
                "sqlserver" => "SELECT count(*) FROM sys.triggers WHERE is_ms_shipped=0",
                _ => "SELECT count(*) FROM sqlite_master WHERE type='trigger'",
            };
            Assert.That(db.Scalar<long>(triggerSql), Is.Zero);
            Assert.That(db.Select<PriceBook>().All(x => !x.IsActive), Is.True);
            var first = new LicenseOrder { Id = Guid.NewGuid(), OrderNumber = "first" };
            var second = new LicenseOrder { Id = Guid.NewGuid(), OrderNumber = "second" };
            db.Insert(first); db.Insert(second);
            db.UpdateOnly(() => new LicenseOrder { StripeCheckoutSessionId = "cs_unique", StripePaymentIntentId = "pi_unique" }, x => x.Id == first.Id);
            Assert.Catch(() => db.UpdateOnly(() => new LicenseOrder { StripeCheckoutSessionId = "cs_unique" }, x => x.Id == second.Id));
            Assert.Catch(() => db.UpdateOnly(() => new LicenseOrder { StripePaymentIntentId = "pi_unique" }, x => x.Id == second.Id));
            migration.Down();
            Assert.That(db.TableExists<SoftwareLicense>(), Is.False);
        }
    }

    [Test]
    public async Task Identity_bootstrap_is_idempotent_and_supports_customer_lookup_and_OrmLite_projection()
    {
        if (DatabaseTestRun.Connection == null) Assert.Ignore("Live server Identity bootstrap is checked in the provider matrix.");
        var factory = DatabaseTestRun.CreateFactory();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        ConfigureDb.ConfigureIdentity(options, DatabaseTestRun.Provider, DatabaseTestRun.Connection!);
        using var identity = new ApplicationDbContext(options.Options);
        using var db = factory.OpenDbConnection();
        // Verify bootstrap also works when application tables already exist.
        new Migration1000 { Db = db }.Up();
        ConfigureDbMigrations.EnsureIdentitySchema(identity, factory);
        ConfigureDbMigrations.EnsureIdentitySchema(identity, factory);
        var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "customer@example.invalid", Email = "customer@example.invalid", DisplayName = "Customer", EmailConfirmed = true };
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "Disposable-Test-Password-123!");
        identity.Users.Add(user);
        await identity.SaveChangesAsync();
        identity.ChangeTracker.Clear();
        var stored = await identity.Users.SingleAsync(x => x.Id == user.Id);
        Assert.That(new PasswordHasher<ApplicationUser>().VerifyHashedPassword(stored, stored.PasswordHash!, "Disposable-Test-Password-123!"), Is.EqualTo(PasswordVerificationResult.Success));
        using var service = new SoftwareSetupServices(new(), new(), identity);
        var results = (LicenseCustomersResponse)await service.Get(new SearchLicenseCustomers { Query = "customer@" });
        Assert.That(results.Results.Single().Name, Is.EqualTo("Customer"));
        Assert.That(db.SingleById<User>(user.Id).DisplayName, Is.EqualTo("Customer"));
    }
    [Test]
    public async Task Production_host_bootstraps_and_restarts_with_encoded_deployment_settings()
    {
        if (DatabaseTestRun.Connection == null) Assert.Ignore("Live application startup is checked in the provider matrix.");
        var factory = DatabaseTestRun.CreateFactory();
        using (var db = factory.OpenDbConnection())
        {
            if (db.TableExists("Migration")) db.ExecuteSql("DROP TABLE " + ConfigureDb.Dialect(DatabaseTestRun.Provider).GetQuotedName("Migration"));
        }
        var root = Path.Combine(Path.GetTempPath(), "next-license-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "App_Data"));
        try
        {
            for (var run = 0; run < 2; run++)
            {
                var start = new System.Diagnostics.ProcessStartInfo("dotnet") {
                    WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                };
                start.ArgumentList.Add(typeof(AppHost).Assembly.Location);
                foreach (var name in start.Environment.Keys.Where(x => x.StartsWith("Database__") || x.StartsWith("ConnectionStrings__")
                    || x.StartsWith("Licensing__") || x.StartsWith("Stripe__") || x.StartsWith("LicenseStripe__") || x == "DB_PROVIDER").ToArray())
                    start.Environment.Remove(name);
                start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
                start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
                start.Environment["APPSETTINGS_JSON_BASE64"] = Convert.ToBase64String(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new {
                    Database = new { Provider = DatabaseTestRun.Provider },
                    ConnectionStrings = new { DefaultConnection = DatabaseTestRun.Connection },
                }));
                using var process = System.Diagnostics.Process.Start(start)!;
                var output = new System.Collections.Concurrent.ConcurrentQueue<string>();
                process.OutputDataReceived += (_, e) => { if (e.Data != null) output.Enqueue(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.Enqueue(e.Data); };
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                try
                {
                    string? url = null;
                    for (var attempt = 0; attempt < 150 && !process.HasExited; attempt++)
                    {
                        var listening = output.FirstOrDefault(x => x.Contains("Now listening on: http://127.0.0.1:"));
                        if (listening != null) { url = listening[(listening.IndexOf("http://", StringComparison.Ordinal))..].Trim(); break; }
                        await Task.Delay(200);
                    }
                    Assert.That(url, Is.Not.Null, string.Join("\n", output));
                    using var client = new HttpClient { BaseAddress = new Uri(url!) };
                    Assert.That(await client.GetStringAsync("/up"), Is.EqualTo("Healthy"));
                    var pricing = await client.GetStringAsync("/api/GetLicensePricing");
                    using var parsed = System.Text.Json.JsonDocument.Parse(pricing);
                    Assert.That(parsed.RootElement.GetProperty("results").GetArrayLength(), Is.Zero);
                }
                finally
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
