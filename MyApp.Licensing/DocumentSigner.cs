using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyApp.Licensing.Core;

namespace MyApp.Licensing;

/// <summary>Signing primitives shared by offline tooling and the server; never ship private keys in hosts.</summary>
public static class DocumentSigner
{
    public static string Sign(string prefix, string domain, object payload, ECDsa key)
    {
        if (key.KeySize != 256 || key.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
            throw new ArgumentException("P-256 required.");
        var segment = SignedDocuments.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signature = key.SignData(Encoding.ASCII.GetBytes(domain + "." + segment), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return prefix + "." + segment + "." + SignedDocuments.Encode(signature);
    }

    public static string Certify(ECDsa root, ECDsa leaf, string rootId, string keyId, string purpose, DateTime notBefore, DateTime notAfter)
    {
        Utc.Require(notBefore); Utc.Require(notAfter);
        if (purpose is not ("License" or "Release") || notAfter <= notBefore) throw new ArgumentException("Invalid certificate policy.");
        var pub = leaf.ExportParameters(false);
        return Sign("MYAPPCERT1", SignedDocuments.CertificateDomain, new {
            v = 1, root = rootId, kid = keyId, purpose,
            x = SignedDocuments.Encode(pub.Q.X!), y = SignedDocuments.Encode(pub.Q.Y!), nbf = notBefore, naf = notAfter,
        }, root);
    }

    // AES-GCM master key must be backed up independently of application/database backups.
    public static string Wrap(ECDsa key, byte[] masterKey, string keyId)
    {
        if (masterKey.Length != 32) throw new ArgumentException("A 256-bit external master key is required.");
        var clear = key.ExportPkcs8PrivateKey();
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var cipher = new byte[clear.Length];
            var tag = new byte[16];
            using var aes = new AesGcm(masterKey, 16);
            aes.Encrypt(nonce, clear, cipher, tag, Encoding.UTF8.GetBytes(keyId));
            return string.Join('.', "v1", SignedDocuments.Encode(nonce), SignedDocuments.Encode(cipher), SignedDocuments.Encode(tag));
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public static ECDsa Unwrap(string wrapped, byte[] masterKey, string keyId)
    {
        if (masterKey.Length != 32 || wrapped.Length > 4096) throw new ArgumentException("Invalid wrapped key.");
        var parts = wrapped.Split('.');
        if (parts.Length != 4 || parts[0] != "v1") throw new FormatException("Unknown wrapping version.");
        var cipher = SignedDocuments.Decode(parts[2]);
        var clear = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(masterKey, 16);
            aes.Decrypt(SignedDocuments.Decode(parts[1]), cipher, SignedDocuments.Decode(parts[3]), clear, Encoding.UTF8.GetBytes(keyId));
            var key = ECDsa.Create();
            try { key.ImportPkcs8PrivateKey(clear, out var read); if (read != clear.Length || key.KeySize != 256) throw new CryptographicException("Invalid private key."); return key; }
            catch { key.Dispose(); throw; }
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
}
