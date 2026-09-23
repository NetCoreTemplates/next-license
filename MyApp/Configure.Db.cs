using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using MyApp.Data;
using ServiceStack.Data;
using ServiceStack.OrmLite;

[assembly: HostingStartup(typeof(MyApp.ConfigureDb))]
namespace MyApp;

public class ConfigureDb : IHostingStartup
{
    // Match EF Identity's quoted PascalCase names when OrmLite queries AspNetUsers.
    private static readonly IOrmLiteDialectProvider Postgres = new ServiceStack.OrmLite.PostgreSQL.PostgreSqlDialectProvider {
        NamingStrategy = new OrmLiteNamingStrategyBase(),
    };
    private static readonly IOrmLiteDialectProvider MySql = CreateMySql();
    private static readonly IOrmLiteDialectProvider SqlServer = CreateSqlServer();

    private static IOrmLiteDialectProvider CreateMySql()
    {
        var dialect = new ServiceStack.OrmLite.MySql.MySqlDialectProvider();
        dialect.RegisterConverter<DateTime>(new ServiceStack.OrmLite.MySql.Converters.MySqlDateTimeConverter { Precision = 6 });
        return dialect;
    }
    private static IOrmLiteDialectProvider CreateSqlServer()
    {
        var dialect = new ServiceStack.OrmLite.SqlServer.SqlServer2012OrmLiteDialectProvider();
        dialect.RegisterConverter<DateTime>(new ServiceStack.OrmLite.SqlServer.Converters.SqlServerDateTime2Converter());
        return dialect;
    }

    public static string NormalizeProvider(string? provider) => (provider ?? "sqlite").Trim().ToLowerInvariant() switch {
        "sqlite" => "sqlite",
        "postgres" or "postgresql" => "postgres",
        "sqlserver" or "mssql" => "sqlserver",
        "mysql" or "mariadb" => "mysql",
        _ => throw new InvalidOperationException("Database:Provider must be sqlite, postgres, mysql or sqlserver."),
    };

    public static string NormalizeConnection(string provider, string connection)
    {
        if (NormalizeProvider(provider) != "sqlite")
        {
            if (connection.Contains("Cache=Shared", StringComparison.OrdinalIgnoreCase)
                || connection.Contains("App_Data/app.db", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The selected server provider requires its own DefaultConnection; the SQLite default cannot be used.");
            return connection;
        }
        var sqlite = new SqliteConnectionStringBuilder(connection);
        if (sqlite.Cache == SqliteCacheMode.Default) sqlite.Cache = SqliteCacheMode.Shared;
        // OrmLite must receive DataSource (no space) to recognize a connection string.
        return sqlite.ToString().Replace("Data Source=", "DataSource=", StringComparison.OrdinalIgnoreCase);
    }

    public static IOrmLiteDialectProvider Dialect(string provider) => NormalizeProvider(provider) switch {
        "postgres" => Postgres,
        "mysql" => MySql,
        "sqlserver" => SqlServer,
        _ => SqliteDialect.Provider,
    };

    public static void ConfigureIdentity(DbContextOptionsBuilder options, string provider, string connection)
    {
        switch (NormalizeProvider(provider))
        {
            case "postgres": options.UseNpgsql(connection); break;
            case "mysql": options.UseMySQL(connection); break;
            case "sqlserver": options.UseSqlServer(connection); break;
            default:
                options.UseSqlite(NormalizeConnection("sqlite", connection), b => b.MigrationsAssembly(nameof(MyApp)));
                options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
                break;
        }
    }

    public void Configure(IWebHostBuilder builder) => builder.ConfigureServices((context, services) => {
        var provider = NormalizeProvider(context.Configuration["Database:Provider"] ?? context.Configuration["DB_PROVIDER"]);
        var connection = context.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
        {
            if (provider != "sqlite") throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection for the selected database provider.");
            connection = "DataSource=App_Data/app.db;Cache=Shared";
        }
        connection = NormalizeConnection(provider, connection);
        services.AddSingleton<IDbConnectionFactory>(new OrmLiteConnectionFactory(connection, Dialect(provider)));
        services.AddDbContext<ApplicationDbContext>(options => ConfigureIdentity(options, provider, connection));
        services.AddPlugin(new AdminDatabaseFeature());
    });
}
