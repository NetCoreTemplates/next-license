using System.Security.Cryptography;
using MyApp.Licensing;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        const string product = "acme-studio", issuer = "acme-studio";
        var buildDate = typeof(Program).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().Single(x => x.Key == "BuildDate").Value!;
        var assembly = typeof(Program).Assembly;
        using var resource = assembly.GetManifestResourceStream("Acme.LicensePublicKey");
        string publicKey; string[] examples = [];
        if (resource != null) { using var reader = new StreamReader(resource); publicKey = reader.ReadToEnd(); }
        else {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            publicKey = key.ExportSubjectPublicKeyInfoPem();
            var claims = new LicenseClaims { Issuer = issuer, Product = product, Id = Guid.NewGuid().ToString(),
                Name = "Alex Morgan", Organization = "Northstar Studio", Seats = 3, Edition = "Pro", IssuedAt = 1780000000,
                UpdatesThrough = "2026-12-31" };
            examples = [LicenseJwt.Sign(claims, key.ExportPkcs8PrivateKeyPem()),
                LicenseJwt.Sign(claims with { UpdatesThrough = "2026-06-30" }, key.ExportPkcs8PrivateKeyPem()),
                LicenseJwt.Sign(claims with { Lifetime = true, UpdatesThrough = null }, key.ExportPkcs8PrivateKeyPem())];
            Console.WriteLine("Disposable demonstration key; bundle your public key for distribution.");
        }
        if (args.Contains("--desktop")) return DesktopHost.Run(publicKey, issuer, product, buildDate, examples);
        foreach (var token in args.Length > 0 ? new[] { File.ReadAllText(args[0]).Trim() } : new[] { "" }.Concat(examples)) {
            var result = LicenseJwt.Verify(token, publicKey, issuer, product, buildDate);
            Console.WriteLine($"Pro enabled: {result.Valid}; status: {result.Status}");
            if (result.License is { } c) Console.WriteLine($"Registered to {c.Name} / {c.Organization}; {c.Seats} seats");
        }
        return 0;
    }
}
