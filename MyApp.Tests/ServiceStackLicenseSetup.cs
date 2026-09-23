using NUnit.Framework;

namespace MyApp.Tests;

[SetUpFixture]
public class ServiceStackLicenseSetup
{
    [OneTimeSetUp]
    public void RegisterLicense() => MyApp.AppHost.RegisterKey();
}
