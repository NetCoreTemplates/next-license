using System.Net.Http.Json;

namespace MyApp.Licensing;

public sealed record ActivationState(string InstallationId, DateTime? LastAttemptUtc);
public interface IActivationStore
{
    Task<ActivationState> Load(CancellationToken token);
    Task Save(ActivationState state, CancellationToken token);
    Task SaveLicense(string verifiedBlob, CancellationToken token);
}
/// <summary>Opt-in best-effort refresh. The host owns protected storage and launches this without blocking paid usage.</summary>
public sealed class ActivationClient(HttpClient http, Uri server, string publicKeyPem, string issuer, string product, IActivationStore store)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task Refresh(string blob, bool enabled, string version, string os, string arch, CancellationToken token = default)
    {
        if (!enabled || server.Scheme != "https") return;
        var acquired = false;
        try
        {
            acquired = await gate.WaitAsync(0, token);
            if (!acquired) return;
        }
        catch (OperationCanceledException) { return; }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var current = LicenseJwt.Read(blob, publicKeyPem, issuer, product);
            if (string.IsNullOrWhiteSpace(current.RefreshKey)) return;
            var state = await store.Load(timeout.Token);
            if (!Guid.TryParseExact(state.InstallationId, "D", out _)) return;
            var now = DateTime.UtcNow;
            if (state.LastAttemptUtc is { } last && now - last < TimeSpan.FromDays(1)) return;
            // Persist before sending: failure is retried on a later launch after the daily throttle.
            await store.Save(state with { LastAttemptUtc = now }, timeout.Token);
            using var response = await http.PostAsJsonAsync(new Uri(server, "/licensing/activate"), new {
                key = current.RefreshKey, installationId = state.InstallationId, appVersion = version, os, arch,
            }, timeout.Token);
            if (!response.IsSuccessStatusCode) return;
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var body = new MemoryStream();
            var buffer = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (body.Length + count > 64000) return;
                body.Write(buffer, 0, count);
            }
            var result = System.Text.Json.JsonSerializer.Deserialize<RefreshResponse>(body.ToArray(),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result?.Blob == null || result.Status != "Active") return;
            var refreshed = LicenseJwt.Read(result.Blob, publicKeyPem, issuer, product);
            if (refreshed.Id != current.Id || refreshed.Product != current.Product
                || refreshed.IssuedAt < current.IssuedAt) return;
            await store.SaveLicense(result.Blob, timeout.Token);
        }
        catch (Exception) { /* Refresh never gates paid usage, including storage, network, and verification failures. */ }
        finally { if (acquired) gate.Release(); }
    }
    private sealed class RefreshResponse { public string? Blob { get; set; } public string? Status { get; set; } }
}
