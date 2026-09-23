using MyApp.Licensing;
using MyApp.Licensing.Core;
using MyApp.ServiceInterface;
using NUnit.Framework;
namespace MyApp.Tests;
public class ActivationClientTests
{
    [Test]
    public async Task Cancellation_and_opt_out_do_not_access_storage_or_break_paid_launch()
    {
        using var http = new HttpClient();
        var store = new RejectAccess();
        var client = new ActivationClient(http, new Uri("https://example.invalid"), "unused", "acme-studio", "acme-studio", store);
        await client.Refresh("not read", false, "1.0.0", "linux", "x64");
        await client.Refresh("not read", true, "1.0.0", "linux", "x64", new CancellationToken(true));
        Assert.That(store.Accessed, Is.False);
    }
    [Test]
    public void Per_license_limit_does_not_block_unrelated_licenses()
    {
        using var throttle = new ActivationThrottle();
        var id = Guid.NewGuid();
        for (var i = 0; i < 30; i++) Assert.That(throttle.Allow(id), Is.True);
        Assert.That(throttle.Allow(id), Is.False);
        Assert.That(throttle.Allow(Guid.NewGuid()), Is.True);
    }
    private sealed class RejectAccess : IActivationStore
    {
        public bool Accessed { get; private set; }
        public Task<ActivationState> Load(CancellationToken token) { Accessed = true; throw new InvalidOperationException(); }
        public Task Save(ActivationState state, CancellationToken token) { Accessed = true; throw new InvalidOperationException(); }
        public Task SaveLicense(string blob, CancellationToken token) { Accessed = true; throw new InvalidOperationException(); }
    }
}
