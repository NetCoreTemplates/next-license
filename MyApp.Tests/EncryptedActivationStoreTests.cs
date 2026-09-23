using System.Security.Cryptography;
using MyApp.Licensing;
using NUnit.Framework;
namespace MyApp.Tests;
public class EncryptedActivationStoreTests
{
    [Test]
    public async Task Storage_survives_restart_and_rejects_wrong_key_and_tampering()
    {
        var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        var key=RandomNumberGenerator.GetBytes(32); ActivationState state;
        try
        {
            using(var store=new EncryptedActivationStore(directory,key))
            { state=await store.Load(default); await store.Save(state with { LastAttemptUtc=DateTime.UtcNow },default); await store.SaveLicense("signed-sensitive-license",default); }
            Assert.That(System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(Path.Combine(directory,"license.enc"))),Does.Not.Contain("signed-sensitive-license"));
            using(var restored=new EncryptedActivationStore(directory,key))
            { Assert.That((await restored.Load(default)).InstallationId,Is.EqualTo(state.InstallationId)); Assert.That(await restored.LoadLicense(),Is.EqualTo("signed-sensitive-license")); }
            using(var wrong=new EncryptedActivationStore(directory,RandomNumberGenerator.GetBytes(32)))
                Assert.ThrowsAsync<AuthenticationTagMismatchException>(()=>wrong.LoadLicense());
            var bytes=await File.ReadAllBytesAsync(Path.Combine(directory,"license.enc")); bytes[^1]^=1; await File.WriteAllBytesAsync(Path.Combine(directory,"license.enc"),bytes);
            using var tampered=new EncryptedActivationStore(directory,key);
            Assert.ThrowsAsync<AuthenticationTagMismatchException>(()=>tampered.LoadLicense());
        }
        finally { Directory.Delete(directory,true); CryptographicOperations.ZeroMemory(key); }
    }
}
