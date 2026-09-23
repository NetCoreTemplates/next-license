using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using MyApp.Data;
using MyApp.Licensing.Core;
using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.OrmLite;

namespace MyApp;

/// <summary>Creates clearly marked example fixtures on an empty demo database.</summary>
public static class ExampleDataSeeder
{
    private const string DemoPrefix = "DEMO-";
    private const string AgreementVersion = "demo-1";
    private const string SeedActor = "example-data-seed";

    public static void Seed(IAppHost appHost)
    {
        using var db = appHost.Resolve<IDbConnectionFactory>().Open();
        var seeded = db.Count<LicenseOrder>(x => x.OrderNumber.StartsWith(DemoPrefix));
        if (seeded != 0)
        {
            Console.WriteLine($"Example fixtures are already present ({seeded} demo orders); no data was changed.");
            return;
        }
        if (db.Count<LicenseOrder>() != 0 || db.Count<SoftwareLicense>() != 0)
            throw new InvalidOperationException("Example-data seed requires a database with no orders or licenses. Use a fresh disposable database.");
        if (!appHost.GetApplicationServices().GetRequiredService<IHostEnvironment>().IsDevelopment())
            BackupPreviewDatabase(appHost);
        using var scope = appHost.GetApplicationServices().GetRequiredService<IServiceScopeFactory>().CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signing = appHost.GetApplicationServices().GetRequiredService<LicenseSigning>();
        signing.ValidateLicenseIssuance();

        var now = DateTime.UtcNow;
        var customers = new[]
        {
            new DemoCustomer("alex.morgan@example.test", "Alex Morgan", "Morgan Design Co."),
            new DemoCustomer("jamie.chen@example.test", "Jamie Chen", "Northstar Labs"),
            new DemoCustomer("riley.patel@example.test", "Riley Patel", null),
        };
        var demoPassword = appHost.GetApplicationServices().GetRequiredService<IHostEnvironment>().IsDevelopment()
            ? "Demo-Only-Change-Me!42"
            : Environment.GetEnvironmentVariable("EXAMPLE_DATA_PASSWORD");
        if (string.IsNullOrWhiteSpace(demoPassword))
            throw new InvalidOperationException("Set EXAMPLE_DATA_PASSWORD when seeding a hosted preview.");
        var users = customers.Select(x => EnsureUser(userManager, x, demoPassword)).ToArray();
        using var tx = db.OpenTransaction();

        if (!db.Exists<LicenseAgreement>(x => x.Version == AgreementVersion))
            db.Insert(new LicenseAgreement {
                Version = AgreementVersion, EffectiveAtUtc = now.AddDays(-90),
                BodyMarkdown = "# Acme Studio License Agreement\n\n" +
                    "This demonstration agreement grants the named licensee a personal or organization license to use Acme Studio.\n\n" +
                    "## License terms\n\n- Use the software within the edition and update coverage shown on your license.\n" +
                    "- You may install it on devices you control for your own work.\n" +
                    "- Do not redistribute the software or your license key.\n\n" +
                    "This sample text is example data and must be replaced before selling software.\n",
                CreatedDate = now.AddDays(-90), ModifiedDate = now.AddDays(-90), CreatedBy = SeedActor, ModifiedBy = SeedActor,
            });

        // Set attractive example amounts for the Operations UI, but never approve
        // a paid price or invent a Stripe Price ID. The public pricing page remains safe.
        SetDraftPrice(db, "pro-12m-new", 4900, now);
        SetDraftPrice(db, "pro-lifetime-new", 12900, now);
        SetDraftPrice(db, "pro-12m-renewal", 2900, now);
        SetDraftPrice(db, "pro-lifetime-upgrade", 8000, now);
        SetDraftPrice(db, "pro-edition-upgrade", 5000, now);

        var annualOrder = CreatePaidOrder(db, users[0], customers[0], now.AddDays(-12),
            "annual", "pro-12m-new", "Acme Studio Pro · 12 months", Edition.Pro,
            UpdateMode.ThroughDate, 12, 4900, 2, now);
        var annualLicense = CreateLicense(db, signing, users[0], customers[0], annualOrder,
            Edition.Pro, UpdateMode.ThroughDate, now.AddMonths(12), 2, now.AddDays(-12), now);
        annualOrder.LicenseId = annualLicense.Id;
        db.UpdateOnly(() => new LicenseOrder { LicenseId = annualLicense.Id }, x => x.Id == annualOrder.Id);

        var lifetimeOrder = CreatePaidOrder(db, users[1], customers[1], now.AddDays(-5),
            "lifetime", "pro-lifetime-new", "Acme Studio Pro · Lifetime", Edition.Pro,
            UpdateMode.Lifetime, 0, 12900, 1, now);
        var lifetimeLicense = CreateLicense(db, signing, users[1], customers[1], lifetimeOrder,
            Edition.Pro, UpdateMode.Lifetime, null, 1, now.AddDays(-5), now);
        db.UpdateOnly(() => new LicenseOrder { LicenseId = lifetimeLicense.Id }, x => x.Id == lifetimeOrder.Id);

        CreatePendingOrder(db, users[2], customers[2], now.AddHours(-3), now);

        foreach (var user in users)
            db.Insert(new LicenseNotificationPreferences { UserId = user.Id, UpdateReminders = true, ReleaseAnnouncements = true, WinBack = false });

        tx.Commit();
        Console.WriteLine("Example fixtures created: 3 demo customers, 3 demo orders, 2 signed demo licenses.");
        if (appHost.GetApplicationServices().GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            Console.WriteLine("Demo customer sign-in: alex.morgan@example.test / Demo-Only-Change-Me!42");
            Console.WriteLine("Demo customer sign-in: jamie.chen@example.test / Demo-Only-Change-Me!42");
            Console.WriteLine("Demo customer sign-in: riley.patel@example.test / Demo-Only-Change-Me!42");
            Console.WriteLine("Admin sign-in uses the existing Development account: admin@email.com / p@55wOrd");
        }
        Console.WriteLine("Paid sample orders are local fixtures with no Stripe IDs; paid prices remain unapproved.");
    }

    private static void BackupPreviewDatabase(IAppHost appHost)
    {
        var services = appHost.GetApplicationServices();
        var connection = services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
            ?? "DataSource=App_Data/app.db;Cache=Shared";
        var sourcePath = Path.GetFullPath(new SqliteConnectionStringBuilder(connection).DataSource,
            services.GetRequiredService<IHostEnvironment>().ContentRootPath);
        var backupPath = sourcePath + ".before-example-data-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".bak";
        if (!OperatingSystem.IsWindows())
        {
            using var emptyBackup = new FileStream(backupPath, new FileStreamOptions {
                Mode = FileMode.CreateNew, Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            });
        }
        using (var source = new SqliteConnection($"DataSource={sourcePath}"))
        using (var backup = new SqliteConnection($"DataSource={backupPath}"))
        {
            source.Open();
            backup.Open();
            source.BackupDatabase(backup);
        }
        Console.WriteLine("Created a SQLite backup before adding example data.");
    }

    private static ApplicationUser EnsureUser(UserManager<ApplicationUser> manager, DemoCustomer customer, string password)
    {
        var user = manager.FindByEmailAsync(customer.Email).GetAwaiter().GetResult();
        if (user != null) return user;
        user = new ApplicationUser {
            Id = "demo-" + customer.Email[..customer.Email.IndexOf('@')].Replace('.', '-'),
            Email = customer.Email, UserName = customer.Email, EmailConfirmed = true,
            DisplayName = customer.Name, FirstName = customer.Name.Split(' ')[0],
            LastName = customer.Name.Split(' ').Last(),
        };
        var result = manager.CreateAsync(user, password).GetAwaiter().GetResult();
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        return user;
    }

    private static void SetDraftPrice(System.Data.IDbConnection db, string sku, long cents, DateTime now) =>
        db.UpdateOnly(() => new PriceBook { UnitAmountCents = cents, Currency = "usd", IsActive = false,
            ModifiedDate = now, ModifiedBy = SeedActor }, x => x.Sku == sku);

    private static LicenseOrder CreatePaidOrder(System.Data.IDbConnection db, ApplicationUser user, DemoCustomer customer,
        DateTime at, string suffix, string sku, string description, Edition edition, UpdateMode mode,
        int months, long cents, int seats, DateTime now)
    {
        var id = Guid.NewGuid();
        var order = new LicenseOrder {
            Id = id, OrderNumber = DemoPrefix + suffix.ToUpperInvariant() + "-" + at.ToString("yyyyMMdd"),
            UserId = user.Id, Kind = OrderKind.NewPurchase, Seats = seats, LicenseeName = customer.Name,
            LicenseeOrganization = customer.Organization, Currency = "usd", ExpectedAmountCents = cents,
            FinalAmountCents = cents, AgreementVersion = AgreementVersion, AgreementAcceptedAtUtc = at,
            Status = OrderStatus.Paid, PaidAtUtc = at,
            CreatedDate = at, ModifiedDate = at, CreatedBy = SeedActor, ModifiedBy = SeedActor,
        };
        db.Insert(order);
        db.Insert(new OrderLine {
            OrderId = id, Sku = sku, Description = description, Edition = edition, UpdateMode = mode,
            TermMonths = months, Quantity = 1, UnitAmountCents = cents, StripePriceId = "",
            CreatedDate = at, ModifiedDate = at, CreatedBy = SeedActor, ModifiedBy = SeedActor,
        });
        db.Insert(new LicenseCheckoutPolicy { OrderId = id, AllowPromotionCodes = false, AutomaticTax = false });
        db.Insert(new LicenseAgreementAcceptance { OrderId = id, UserId = user.Id, AgreementVersion = AgreementVersion,
            AcceptedAtUtc = at, IpAddress = "127.0.0.1" });
        db.Insert(new LicenseOrderSettlement { OrderId = id, SubtotalCents = cents, TotalCents = cents });
        return order;
    }

    private static SoftwareLicense CreateLicense(System.Data.IDbConnection db, LicenseSigning signing,
        ApplicationUser user, DemoCustomer customer, LicenseOrder order, Edition edition, UpdateMode mode,
        DateTime? through, int seats, DateTime issued, DateTime now)
    {
        var key = ShortKey.Create();
        var license = new SoftwareLicense {
            Id = Guid.NewGuid(), ShortKeyHash = signing.HashShortKey(key), ShortKeySuffix = key[^4..],
            ProductId = LicenseProduct.Id, Edition = edition, UpdateMode = mode, UpdatesThroughUtc = through,
            LicenseeName = customer.Name, LicenseeOrganization = customer.Organization, Seats = seats,
            IssuedAtUtc = issued, Status = LicenseStatus.Active, UserId = user.Id, OrderId = order.Id,
            SigningKeyId = "jwt-es256", BlobVersion = 1,
            CreatedDate = issued, ModifiedDate = issued, CreatedBy = SeedActor, ModifiedBy = SeedActor,
        };
        db.Insert(license);
        var (blob, _) = signing.SignLicense(db, license, key, issued);
        db.Insert(new LicenseBlob { LicenseId = license.Id, Version = 1, Blob = blob,
            CreatedDate = issued, ModifiedDate = issued, CreatedBy = SeedActor, ModifiedBy = SeedActor });
        return license;
    }

    private static void CreatePendingOrder(System.Data.IDbConnection db, ApplicationUser user, DemoCustomer customer,
        DateTime at, DateTime now)
    {
        var id = Guid.NewGuid();
        var order = new LicenseOrder {
            Id = id, OrderNumber = DemoPrefix + "PENDING-" + at.ToString("yyyyMMdd-HHmm"), UserId = user.Id,
            Kind = OrderKind.NewPurchase, Seats = 1, LicenseeName = customer.Name, Currency = "usd",
            ExpectedAmountCents = 4900, AgreementVersion = AgreementVersion, AgreementAcceptedAtUtc = at,
            Status = OrderStatus.Pending, CreatedDate = at, ModifiedDate = at,
            CreatedBy = SeedActor, ModifiedBy = SeedActor,
        };
        db.Insert(order);
        db.Insert(new OrderLine { OrderId = id, Sku = "pro-12m-new", Description = "Acme Studio Pro · 12 months",
            Edition = Edition.Pro, UpdateMode = UpdateMode.ThroughDate, TermMonths = 12, Quantity = 1,
            UnitAmountCents = 4900, StripePriceId = "", CreatedDate = at, ModifiedDate = at,
            CreatedBy = SeedActor, ModifiedBy = SeedActor });
        db.Insert(new LicenseCheckoutPolicy { OrderId = id, AllowPromotionCodes = false, AutomaticTax = false });
        db.Insert(new LicenseAgreementAcceptance { OrderId = id, UserId = user.Id, AgreementVersion = AgreementVersion,
            AcceptedAtUtc = at, IpAddress = "127.0.0.1" });
    }

    private sealed record DemoCustomer(string Email, string Name, string? Organization);
}
