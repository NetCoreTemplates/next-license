using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.Migrations;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using NUnit.Framework;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.Host;
using ServiceStack.OrmLite;
using ServiceStack.Testing;
namespace MyApp.Tests;

[NonParallelizable]
[Category("Database")]
public class SoftwareSetupTests
{
    [Test]
    public void Changing_price_removes_approval_and_old_Stripe_mapping_without_changing_order_evidence()
    {
        var factory = DatabaseTestRun.CreateFactory();
        using var db = factory.OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        using var host = new BasicAppHost { ConfigureContainer = c => c.Register<IDbConnectionFactory>(factory) }.Init();
        var request = new BasicRequest();
        request.Items[Keywords.Session] = new AuthUserSession { UserAuthId = "admin", IsAuthenticated = true, Roles = ["Admin"] };
        using var identity = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("DataSource=:memory:;Cache=Shared").Options);
        using var service = new SoftwareSetupServices(new(), new(), identity) { Request = request };
        db.UpdateOnly(() => new PriceBook { UnitAmountCents = 9900, IsActive = true, StripePriceId = "price_original" }, x => x.Sku == "pro-12m-new");
        var order = Guid.NewGuid();
        db.Insert(new OrderLine { OrderId = order, Sku = "pro-12m-new", UnitAmountCents = 9900, StripePriceId = "price_original" });
        var unchanged = (PriceBook)service.Post(new SetSoftwarePrice { Sku = "pro-12m-new", UnitAmountCents = 9900 });
        Assert.That(unchanged.IsActive, Is.True);
        var changed = (PriceBook)service.Post(new SetSoftwarePrice { Sku = "pro-12m-new", UnitAmountCents = 12900 });
        Assert.That(changed.IsActive, Is.False);
        Assert.That(changed.StripePriceId, Is.Empty);
        Assert.That(db.Single<OrderLine>(x => x.OrderId == order).UnitAmountCents, Is.EqualTo(9900));
        Assert.ThrowsAsync<HttpError>(() => service.Post(new ApproveSoftwarePrice { Sku = changed.Sku, Approved = true }));
        Assert.Throws<HttpError>(() => service.Post(new SetSoftwarePrice { Sku = changed.Sku, UnitAmountCents = 0 }));
    }
    [Test]
    public async Task Customer_lookup_searches_identity_by_email_and_does_not_expose_security_fields()
    {
        using var identity = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("DataSource=:memory:;Cache=Shared").Options);
        await identity.Database.OpenConnectionAsync(); await identity.Database.EnsureCreatedAsync();
        identity.Users.Add(new ApplicationUser { Id = "customer", UserName = "alex@example.com", Email = "alex@example.com", DisplayName = "Alex" });
        await identity.SaveChangesAsync();
        using var service = new SoftwareSetupServices(new(), new(), identity);
        var result = (LicenseCustomersResponse)await service.Get(new SearchLicenseCustomers { Query = "alex@" });
        Assert.That(result.Results.Single().Name, Is.EqualTo("Alex"));
        Assert.That(typeof(LicenseCustomer).GetProperties().Select(x => x.Name), Is.EquivalentTo(new[] { "Id", "Name", "Email" }));
        Assert.That(((LicenseCustomersResponse)await service.Get(new SearchLicenseCustomers { Query = "missing" })).Results, Is.Empty);
    }

    [Test, Explicit("Requires an explicitly configured Stripe test key")]
    public async Task Stripe_test_catalog_provisioning_is_retryable_and_requires_separate_approval()
    {
        var secret = Environment.GetEnvironmentVariable("LICENSE_STRIPE_ACCEPTANCE_KEY");
        Assert.That(secret, Does.StartWith("sk_test_"), "A Stripe test key is required.");
        var factory = DatabaseTestRun.CreateFactory();
        using var db = factory.OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        using var host = new BasicAppHost { ConfigureContainer = c => c.Register<IDbConnectionFactory>(factory) }.Init();
        var request = new BasicRequest();
        request.Items[Keywords.Session] = new AuthUserSession { UserAuthId = "acceptance", IsAuthenticated = true, Roles = ["Admin"] };
        using var identity = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("DataSource=:memory:;Cache=Shared").Options);
        using var service = new SoftwareSetupServices(new() { SecretKey = secret }, new(), identity) { Request = request };
        var sku = "acceptance-" + Guid.NewGuid().ToString("N");
        db.Insert(new PriceBook { Sku = sku, UnitAmountCents = 100, Currency = "usd" });
        string? priceId = null;
        var stripe = new Stripe.StripeClient(secret);
        try {
            await service.Post(new CreateMissingStripe());
            var first = db.SingleById<PriceBook>(sku); priceId = first.StripePriceId;
            Assert.That(first.IsActive, Is.False);
            Assert.That(priceId, Does.StartWith("price_"));
            await service.Post(new CreateMissingStripe());
            Assert.That(db.SingleById<PriceBook>(sku).StripePriceId, Is.EqualTo(priceId));
            var approved = (PriceBook)await service.Post(new ApproveSoftwarePrice { Sku = sku, Approved = true });
            Assert.That(approved.IsActive, Is.True);
            var hidden = (PriceBook)await service.Post(new ApproveSoftwarePrice { Sku = sku, Approved = false });
            Assert.That(hidden.IsActive, Is.False);
        } finally {
            if (priceId != null) {
                var prices = new Stripe.PriceService(stripe);
                var price = await prices.GetAsync(priceId);
                await new Stripe.ProductService(stripe).UpdateAsync(price.ProductId, new Stripe.ProductUpdateOptions { DefaultPrice = "" });
                await prices.UpdateAsync(priceId, new Stripe.PriceUpdateOptions { Active = false });
                await new Stripe.ProductService(stripe).UpdateAsync(price.ProductId, new Stripe.ProductUpdateOptions { Active = false });
            }
        }
    }
}
