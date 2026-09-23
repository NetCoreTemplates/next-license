using MyApp.Licensing.Core;
using MyApp.ServiceModel;
using ServiceStack.OrmLite;
namespace MyApp.Migrations;

public class Migration1000 : MigrationBase
{
    private static readonly Type[] Tables = [typeof(SoftwareLicense), typeof(LicenseBlob), typeof(LicensingAuditEvent), typeof(PriceBook),
        typeof(LicenseOrder), typeof(OrderLine), typeof(LicenseAgreement), typeof(LicenseAgreementAcceptance), typeof(StripeEventInbox), typeof(LicenseRefund), typeof(LicenseDispute), typeof(LicenseRefundRequest), typeof(LicenseNotification), typeof(LicenseNotificationPreferences),
        typeof(LicenseTransfer), typeof(LicenseCheckoutPolicy), typeof(LicenseOrderSettlement)];
    public override void Up()
    {
        foreach (var table in Tables) Db.CreateTable(false, table);
        foreach (var sql in StripeIdentityIndexes(Db.GetDialectProvider())) Db.ExecuteSql(sql);
        foreach (var sku in new[] { "pro-12m-new", "pro-lifetime-new", "pro-12m-renewal", "pro-lifetime-upgrade", "pro-edition-upgrade" })
            Db.Insert(new PriceBook { Sku = sku, Edition = sku == "pro-edition-upgrade" ? Edition.Enterprise : Edition.Pro,
                UpdateMode = sku.Contains("lifetime") ? UpdateMode.Lifetime : UpdateMode.ThroughDate, TermMonths = sku.Contains("12m") ? 12 : 0,
                CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow, CreatedBy = "migration", ModifiedBy = "migration" });
        // Prices stay inactive until an operator configures matching Stripe one-time Prices.
    }
    // SQL Server treats NULL as a unique value. Filter only there so multiple pending
    // orders can coexist, while actual Stripe identities remain unique on every provider.
    public static IEnumerable<string> StripeIdentityIndexes(IOrmLiteDialectProvider dialect)
    {
        foreach (var field in new[] { nameof(LicenseOrder.StripeCheckoutSessionId), nameof(LicenseOrder.StripePaymentIntentId) })
        {
            var table = dialect.GetQuotedTableName(typeof(LicenseOrder));
            var column = dialect.GetQuotedColumnName(field);
            var filter = dialect.GetType().Name.StartsWith("SqlServer", StringComparison.Ordinal) ? $" WHERE {column} IS NOT NULL" : "";
            yield return $"CREATE UNIQUE INDEX {dialect.GetQuotedName("uidx_order_" + field)} ON {table} ({column}){filter}";
        }
    }
    public override void Down() { foreach (var table in Tables.Reverse()) Db.DropTable(table); }
}
