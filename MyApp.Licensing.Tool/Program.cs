using System.Security.Cryptography;
using System.Text.Json;
using MyApp.Licensing;
using MyApp.Licensing.Core;

// Simple license setup: one ES256 keypair, no certificates or root hierarchy.
if (args.Length == 2 && args[0] == "jwt") {
    var output = Path.GetFullPath(args[1]);
    if (Directory.Exists(output)) throw new InvalidOperationException("Output directory already exists.");
    Directory.CreateDirectory(output);
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(output, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    using var jwtKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    await File.WriteAllTextAsync(Path.Combine(output, "license-private.pem"), jwtKey.ExportPkcs8PrivateKeyPem());
    await File.WriteAllTextAsync(Path.Combine(output, "license-public.pem"), jwtKey.ExportSubjectPublicKeyInfoPem());
    foreach (var file in Directory.GetFiles(output))
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    Console.WriteLine("Created ES256 keys. Keep license-private.pem on the server; bundle license-public.pem with your apps.");
    return 0;
}
// Run root and certification operations ONLY on an offline operator machine.
if (args.Length != 3 || args[0] is not ("root" or "leaf") || args[1] is not ("License" or "Release"))
{
    Console.Error.WriteLine("Usage: root|leaf License|Release <new-output-directory>\nFor leaf: set ROOT_PEM, ROOT_ID, LEAF_ID and LICENSING_MASTER_KEY in the offline shell.");
    return 2;
}
var directory = Path.GetFullPath(args[2]);
if (Directory.Exists(directory)) throw new InvalidOperationException("Output directory must not exist; refusing to overwrite key material.");
Directory.CreateDirectory(directory);
if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var pub = key.ExportParameters(false);
if (args[0] == "root")
{
    await File.WriteAllTextAsync(Path.Combine(directory, "root-private.pem"), key.ExportECPrivateKeyPem());
    await File.WriteAllTextAsync(Path.Combine(directory, "root-public.json"), JsonSerializer.Serialize(new {
        purpose = args[1], x = SignedDocuments.Encode(pub.Q.X!), y = SignedDocuments.Encode(pub.Q.Y!),
    }));
}
else
{
    string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException("Missing " + name);
    using var root = ECDsa.Create();
    root.ImportFromPem(await File.ReadAllTextAsync(Required("ROOT_PEM")));
    var id = Required("LEAF_ID");
    var at = DateTime.UtcNow;
    var end = at.AddYears(1);
    var cert = DocumentSigner.Certify(root, key, Required("ROOT_ID"), id, args[1], at, end);
    var master = Convert.FromBase64String(Required("LICENSING_MASTER_KEY"));
    try
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "leaf-import.json"), JsonSerializer.Serialize(new {
            Id = id, Purpose = args[1], PublicKey = key.ExportSubjectPublicKeyInfoPem(),
            WrappedPrivateKey = DocumentSigner.Wrap(key, master, id), RootCertificate = cert,
            NotBefore = at, NotAfter = end, IsActive = true,
        }));
    }
    finally { CryptographicOperations.ZeroMemory(master); }
}
foreach (var file in Directory.GetFiles(directory))
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
Console.WriteLine("Created key artifacts. Keep root private material offline and back up separately from the application.");
return 0;
