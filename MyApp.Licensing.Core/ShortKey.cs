using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MyApp.Licensing.Core;

public static class ShortKey
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    // 26 independent base32 characters provide 130 bits of entropy; six checksum characters are additional.
    public static string Create()
    {
        var random = new byte[26];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(random);
        var body = new string(random.Select(x => Alphabet[x & 31]).ToArray());
        var value = body + Checksum(body);
        return "ACME-" + string.Join("-", Enumerable.Range(0, 8).Select(i => value.Substring(i * 4, 4)));
    }

    public static string Normalize(string key)
    {
        if (key == null || key.Length > 64) throw new ArgumentException("Invalid license key.");
        var normalized = key.Trim().ToUpperInvariant().Replace("-", "");
        if (!normalized.StartsWith("ACME", StringComparison.Ordinal)) throw new ArgumentException("Invalid key prefix.");
        var value = normalized.Substring(4);
        if (value.Length != 32 || value.Any(x => Alphabet.IndexOf(x) < 0)
            || value.Substring(26) != Checksum(value.Substring(0, 26)))
            throw new ArgumentException("Invalid license key checksum.");
        return "ACME" + value;
    }

    public static string Hash(string key, byte[] secretSalt)
    {
        if (secretSalt == null || secretSalt.Length < 32) throw new ArgumentException("At least 32 salt bytes are required.");
        using (var hmac = new HMACSHA256(secretSalt))
            return BitConverter.ToString(hmac.ComputeHash(Encoding.ASCII.GetBytes(Normalize(key)))).Replace("-", "").ToLowerInvariant();
    }
    private static string Checksum(string body)
    {
        using (var sha = SHA256.Create())
            return new string(sha.ComputeHash(Encoding.ASCII.GetBytes(body)).Take(6).Select(x => Alphabet[x & 31]).ToArray());
    }
}
