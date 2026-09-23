using System.Threading.RateLimiting;
namespace MyApp.ServiceInterface;
public sealed class ActivationThrottle : IDisposable
{
    private readonly PartitionedRateLimiter<Guid> limiter = PartitionedRateLimiter.Create<Guid, Guid>(id =>
        RateLimitPartition.GetFixedWindowLimiter(id, _ => new FixedWindowRateLimiterOptions {
            PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true,
        }));
    public bool Allow(Guid id) { using var lease = limiter.AttemptAcquire(id); return lease.IsAcquired; }
    public void Dispose() => limiter.Dispose();
}
