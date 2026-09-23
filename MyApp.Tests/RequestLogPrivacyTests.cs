using NUnit.Framework;
using ServiceStack;
namespace MyApp.Tests;
public class RequestLogPrivacyTests
{
    [Test]
    public void Request_log_redacts_credentials_payloads_sessions_and_network_identifiers()
    {
        var entry = new RequestLogEntry {
            Headers = new() { ["Authorization"] = "secret-bearer", ["Cookie"] = "secret-cookie" },
            RequestBody = "secret-body", RequestDto = new { Key = "secret-key" }, ResponseDto = new { Blob = "secret-blob" },
            FormData = new() { ["password"] = "secret-password" }, ErrorResponse = new { Detail = "secret-error" },
            IpAddress = "secret-ip", ForwardedFor = "secret-proxy", Referer = "secret-referer", AbsoluteUri = "secret-query",
            Items = new() { ["secret-item"] = "secret-value" }, SessionId = "secret-session",
        };
        ConfigureRequestLogs.Sanitize(entry);
        Assert.That(entry.ToJson(), Does.Not.Contain("secret"));
    }
}
