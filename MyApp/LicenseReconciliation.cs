using MyApp.ServiceInterface;
using MyApp.ServiceModel;
using ServiceStack.Data;
using ServiceStack.Jobs;
using ServiceStack.OrmLite;
namespace MyApp;

// Durable inbox recovery covers a process crash after accepting a webhook but before enqueueing its job.
public class LicenseInboxRecovery(IDbConnectionFactory factory, IBackgroundJobs jobs, ILogger<LicenseInboxRecovery> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var db = factory.OpenDbConnection();
                if (db.TableExists<StripeEventInbox>())
                {
                    var threshold = DateTime.UtcNow.AddMinutes(-5);
                    foreach (var evt in db.Select(db.From<StripeEventInbox>().Where(x => x.ProcessedAtUtc == null && x.ReceivedAtUtc < threshold && x.Attempts < 20).Limit(100)))
                        jobs.EnqueueCommand<ProcessLicenseStripeEventCommand>(new ProcessLicenseStripeEvent { Id = evt.Id });
                }
            }
            catch (Exception) { logger.LogWarning("License inbox recovery failed; inspect database connectivity and pending inbox rows."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}

public class LicenseCheckoutReconciliation(IDbConnectionFactory factory, LicenseStripeConfig config,
    LicenseCheckoutRecovery recovery, ILogger<LicenseCheckoutReconciliation> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(config.SecretKey)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var db = factory.OpenDbConnection();
                var threshold = DateTime.UtcNow.AddMinutes(-10);
                var pending = db.Select(db.From<LicenseOrder>().Where(x => x.Status == OrderStatus.Pending
                    && x.StripeCheckoutSessionId != null && x.ModifiedDate < threshold)
                    .OrderBy(x => x.ModifiedDate).Limit(100));
                foreach (var order in pending)
                {
                    if (stoppingToken.IsCancellationRequested) return;
                    // Rotate the scan so a permanently invalid order cannot starve later orders.
                    db.UpdateOnly(() => new LicenseOrder { ModifiedDate = DateTime.UtcNow, ModifiedBy = "reconciliation" },
                        x => x.Id == order.Id && x.Status == OrderStatus.Pending);
                    try { await recovery.Reconcile(db, order.Id); }
                    catch (Exception)
                    {
                        db.UpdateOnly(() => new LicenseOrder { RequiresReview = true }, x => x.Id == order.Id);
                        logger.LogWarning("Checkout reconciliation requires review for order {OrderId}.", order.Id);
                    }
                }
            }
            catch (Exception) { logger.LogWarning("Checkout reconciliation failed; inspect database connectivity."); }
        }
    }
}

