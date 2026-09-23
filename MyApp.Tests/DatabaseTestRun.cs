using System.Data.Common;
using MyApp.Migrations;
using NUnit.Framework;
using ServiceStack.OrmLite;

namespace MyApp.Tests;

// Opt in with LICENSE_TEST_PROVIDER and LICENSE_TEST_ADMIN_CONNECTION. A randomly
// named database is created and dropped; no caller-supplied application DB is erased.
[SetUpFixture]
public class DatabaseTestRun
{
    private string? database;
    private OrmLiteConnectionFactory? admin;
    public static string Provider { get; private set; } = "sqlite";
    public static string? Connection { get; private set; }

    [OneTimeSetUp]
    public void Start()
    {
        var selected = Environment.GetEnvironmentVariable("LICENSE_TEST_PROVIDER");
        if (string.IsNullOrWhiteSpace(selected)) return;
        Provider = ConfigureDb.NormalizeProvider(selected);
        if (Provider == "sqlite") return;
        var source = Environment.GetEnvironmentVariable("LICENSE_TEST_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set LICENSE_TEST_ADMIN_CONNECTION for live database tests.");
        var settings = new DbConnectionStringBuilder { ConnectionString = source };
        settings["Pooling"] = false;
        var dialect = ConfigureDb.Dialect(Provider);
        admin = new OrmLiteConnectionFactory(settings.ConnectionString, dialect);
        database = "next_license_test_" + Guid.NewGuid().ToString("N")[..12];
        using var db = admin.OpenDbConnection();
        var versionSql = Provider == "sqlserver" ? "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))" : "SELECT version()";
        TestContext.Progress.WriteLine($"Server {Provider}: {db.Scalar<string>(versionSql)}");
        db.ExecuteSql("CREATE DATABASE " + dialect.GetQuotedName(database));
        settings["Database"] = database;
        Connection = settings.ConnectionString;
        TestContext.Progress.WriteLine($"Live {Provider}: isolated database {database}");
    }

    public static OrmLiteConnectionFactory CreateFactory()
    {
        if (Connection == null) return new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider);
        var factory = new OrmLiteConnectionFactory(Connection, ConfigureDb.Dialect(Provider));
        using var db = factory.OpenDbConnection();
        new Migration1000 { Db = db }.Down();
        return factory;
    }

    [OneTimeTearDown]
    public void Stop()
    {
        if (admin == null || database == null) return;
        using var db = admin.OpenDbConnection();
        db.ExecuteSql("DROP DATABASE " + ConfigureDb.Dialect(Provider).GetQuotedName(database));
        TestContext.Progress.WriteLine($"Removed isolated {Provider} database {database}");
    }
}
