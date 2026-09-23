using MyApp.Licensing.Core;
using NUnit.Framework;
namespace MyApp.Tests;
public class SemanticVersionTests
{
    [Test]
    public void Official_semver_precedence_and_build_metadata()
    {
        var versions = new[] { "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0", "1.1.0", "2.0.0" };
        for (var i = 1; i < versions.Length; i++) Assert.That(SemanticVersion.Parse(versions[i]).CompareTo(SemanticVersion.Parse(versions[i - 1])), Is.GreaterThan(0));
        Assert.That(SemanticVersion.Parse("1.0.0+build.2").CompareTo(SemanticVersion.Parse("1.0.0+build.3")), Is.Zero);
        foreach (var value in new[] { "1.0", "01.0.0", "1.0.0-alpha.01", "1.0.0-", "1.0.0-a..b", "1.0.0\n" })
            Assert.That(SemanticVersion.TryParse(value, out _), Is.False, value);
    }
}
