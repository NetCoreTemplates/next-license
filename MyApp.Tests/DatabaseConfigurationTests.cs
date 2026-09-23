using NUnit.Framework;
namespace MyApp.Tests;

public class DatabaseConfigurationTests
{
    [TestCase("PostgreSql", "postgres")]
    [TestCase("MariaDB", "mysql")]
    [TestCase("MSSQL", "sqlserver")]
    [TestCase(null, "sqlite")]
    public void Database_provider_aliases_are_normalized(string? supplied, string expected) =>
        Assert.That(ConfigureDb.NormalizeProvider(supplied), Is.EqualTo(expected));

    [Test]
    public void SQLite_connection_strings_are_canonical_and_server_configuration_cannot_use_the_default_file()
    {
        var connection = ConfigureDb.NormalizeConnection("sqlite", "DataSource=/tmp/license-test.db");
        Assert.That(connection, Does.StartWith("DataSource="));
        Assert.That(connection, Does.Contain("Cache=Shared"));
        foreach (var provider in new[] { "postgres", "mysql", "sqlserver" })
            Assert.Throws<InvalidOperationException>(() => ConfigureDb.NormalizeConnection(provider, "DataSource=App_Data/app.db;Cache=Shared"));
        Assert.Throws<InvalidOperationException>(() => ConfigureDb.NormalizeProvider("unknown"));
    }
}
