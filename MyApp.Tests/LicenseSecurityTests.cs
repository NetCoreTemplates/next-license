using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace MyApp.Tests;

public class LicenseSecurityTests
{
    [TestCase("https://next-license.react-templates.net", "next-license.react-templates.net", true)]
    [TestCase("http://next-license.react-templates.net", "next-license.react-templates.net", false)]
    [TestCase("https://another.example", "next-license.react-templates.net", false)]
    [TestCase("https://next-license.react-templates.net", "another.example", false)]
    public void Proxy_origin_check_uses_the_configured_public_scheme_only_for_its_host(
        string origin, string host, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString(host);

        Assert.That(LicenseSecurityFilter.IsSameOrigin(
            context.Request, origin, "https://next-license.react-templates.net"), Is.EqualTo(expected));
    }
}
