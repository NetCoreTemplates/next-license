using MyApp.Migrations;
using MyApp.ServiceModel;
using NUnit.Framework;
using ServiceStack.OrmLite;

namespace MyApp.Tests;

public class LicenseCatalogTests
{
    [Test]
    public void Baseline_creates_all_tables_and_can_be_reverted_and_recreated()
    {
        using var db = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider).OpenDbConnection();
        var migration = new Migration1000 { Db = db };
        var migrations = typeof(Migration1000).Assembly.GetTypes()
            .Where(x => !x.IsAbstract && typeof(MigrationBase).IsAssignableFrom(x)).ToArray();
        Assert.That(migrations, Is.EqualTo(new[] { typeof(Migration1000) }));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            migration.Up();
            var tables = db.Column<string>("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'");
            Assert.That(tables, Has.Count.EqualTo(17));
            Assert.That(tables, Does.Not.Contain("Booking"));
            Assert.That(db.Count<PriceBook>(), Is.EqualTo(5));
            var triggers = db.Column<string>("SELECT name FROM sqlite_master WHERE type = 'trigger'");
            Assert.That(triggers, Is.Empty);
            Assert.That(db.Select<PriceBook>().All(x => !x.IsActive), Is.True);
            migration.Down();
            Assert.That(db.Column<string>("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'"), Is.Empty);
            Assert.That(db.Column<string>("SELECT name FROM sqlite_master WHERE type = 'trigger'"), Is.Empty);
        }
    }

    [Test]
    public void Pending_orders_allow_null_provider_ids_but_reject_duplicate_real_ids()
    {
        using var db = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider).OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        var first = new LicenseOrder { Id = Guid.NewGuid(), OrderNumber = "first" };
        var second = new LicenseOrder { Id = Guid.NewGuid(), OrderNumber = "second" };
        db.Insert(first); db.Insert(second);
        db.UpdateOnly(() => new LicenseOrder { StripeCheckoutSessionId = "cs_unique", StripePaymentIntentId = "pi_unique" }, x => x.Id == first.Id);
        Assert.Catch(() => db.UpdateOnly(() => new LicenseOrder { StripeCheckoutSessionId = "cs_unique" }, x => x.Id == second.Id));
        Assert.Catch(() => db.UpdateOnly(() => new LicenseOrder { StripePaymentIntentId = "pi_unique" }, x => x.Id == second.Id));
    }

    [TestCase("SQLite")]
    [TestCase("PostgreSQL")]
    [TestCase("MySQL")]
    [TestCase("SQLServer")]
    public void Every_retained_table_generates_schema_without_triggers(string provider)
    {
        var dialect = ConfigureDb.Dialect(provider);
        using var db = new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider).OpenDbConnection();
        new Migration1000 { Db = db }.Up();
        var names = db.Column<string>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'");
        foreach (var name in names)
        {
            var type = typeof(SoftwareLicense).Assembly.GetType("MyApp.ServiceModel." + name)!;
            var sql = dialect.ToCreateTableStatement(type) + string.Join("\n", dialect.ToCreateIndexStatements(type));
            Assert.That(sql, Does.Not.Contain("TRIGGER").IgnoreCase, name);
            Assert.That(sql, Does.Not.Contain("RAISE(").IgnoreCase, name);
            Assert.That(sql, Does.Contain("CREATE TABLE").IgnoreCase, name);
        }
        var indexes = Migration1000.StripeIdentityIndexes(dialect).ToArray();
        Assert.That(indexes, Has.Length.EqualTo(2));
        foreach (var index in indexes)
        {
            Assert.That(index.Contains("IS NOT NULL"), Is.EqualTo(provider == "SQLServer"));
            Assert.That(index, Does.Contain("CREATE UNIQUE INDEX"));
        }
    }
}
