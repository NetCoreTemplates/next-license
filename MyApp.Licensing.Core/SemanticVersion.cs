using System;
using System.Globalization;
using System.Text.RegularExpressions;
namespace MyApp.Licensing.Core;

/// <summary>SemVer 2 precedence. Build metadata is retained in identity but ignored when comparing.</summary>
public sealed class SemanticVersion : IComparable<SemanticVersion>
{
    public string Value { get; }
    public string Prerelease { get; }
    private readonly long major, minor, patch;
    private SemanticVersion(string value, long major, long minor, long patch, string prerelease)
    { Value = value; this.major = major; this.minor = minor; this.patch = patch; Prerelease = prerelease; }
    public static bool TryParse(string? value, out SemanticVersion? version)
    {
        version = null;
        if (value == null || value.Length > 100) return false;
        var match = Regex.Match(value, @"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z", RegexOptions.CultureInvariant);
        if (!match.Success || !long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !long.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var patch)) return false;
        var prerelease = match.Groups[4].Value;
        foreach (var identifier in prerelease.Split('.'))
            if (Numeric(identifier) && identifier.Length > 1 && identifier[0] == '0') return false;
        version = new SemanticVersion(value, major, minor, patch, prerelease);
        return true;
    }
    public static SemanticVersion Parse(string value) => TryParse(value, out var version) ? version! : throw new FormatException("Invalid semantic version.");
    public int CompareTo(SemanticVersion? other)
    {
        if (other == null) return 1;
        var result = major.CompareTo(other.major); if (result != 0) return result;
        result = minor.CompareTo(other.minor); if (result != 0) return result;
        result = patch.CompareTo(other.patch); if (result != 0) return result;
        if (Prerelease.Length == 0 || other.Prerelease.Length == 0)
            return Prerelease.Length == other.Prerelease.Length ? 0 : Prerelease.Length == 0 ? 1 : -1;
        var left = Prerelease.Split('.'); var right = other.Prerelease.Split('.');
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var ln = Numeric(left[i]); var rn = Numeric(right[i]);
            if (ln != rn) return ln ? -1 : 1;
            if (ln && left[i].Length != right[i].Length) return left[i].Length.CompareTo(right[i].Length);
            result = string.CompareOrdinal(left[i], right[i]); if (result != 0) return result;
        }
        return left.Length.CompareTo(right.Length);
    }
    private static bool Numeric(string text)
    {
        if (text.Length == 0) return false;
        foreach (var c in text) if (c < '0' || c > '9') return false;
        return true;
    }
}
