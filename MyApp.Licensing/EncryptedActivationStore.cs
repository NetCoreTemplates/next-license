using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace MyApp.Licensing;

/// <summary>Cross-platform encrypted storage. The host supplies a persistent 32-byte key from its OS secret store,
/// never from the license, executable or this directory. Use a directory owned by the current OS user.</summary>
public sealed class EncryptedActivationStore : IActivationStore, IDisposable
{
    private readonly string directory;
    private readonly byte[] key;
    private readonly SemaphoreSlim gate = new(1, 1);
    public EncryptedActivationStore(string directory, byte[] externalKey)
    {
        if (externalKey.Length != 32) throw new ArgumentException("A 32-byte host storage key is required.");
        this.directory = Path.GetFullPath(directory); key = externalKey.ToArray();
        Directory.CreateDirectory(this.directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(this.directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    public async Task<ActivationState> Load(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var text = await Read("activation", token);
            if (text != null) return JsonSerializer.Deserialize<ActivationState>(text) ?? throw new FormatException("Invalid activation state.");
            var state = new ActivationState(Guid.NewGuid().ToString("D"), null);
            await Write("activation", JsonSerializer.Serialize(state), token); return state;
        }
        finally { gate.Release(); }
    }
    public Task Save(ActivationState state, CancellationToken token) => Store("activation", JsonSerializer.Serialize(state), token);
    public Task SaveLicense(string verifiedBlob, CancellationToken token) => Store("license", verifiedBlob, token);
    public async Task<string?> LoadLicense(CancellationToken token = default)
    {
        await gate.WaitAsync(token); try { return await Read("license", token); } finally { gate.Release(); }
    }
    public async Task ClearLicense(CancellationToken token = default)
    {
        await gate.WaitAsync(token); try { File.Delete(Path.Combine(directory, "license.enc")); } finally { gate.Release(); }
    }
    private async Task Store(string name, string value, CancellationToken token)
    {
        await gate.WaitAsync(token); try { await Write(name, value, token); } finally { gate.Release(); }
    }
    private async Task<string?> Read(string name, CancellationToken token)
    {
        var path = Path.Combine(directory, name + ".enc");
        if (!File.Exists(path)) return null;
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length is < 29 or > 65536) throw new FormatException("Invalid encrypted storage length.");
        var bytes = new byte[(int)input.Length]; await input.ReadExactlyAsync(bytes, token);
        if (bytes[0] != 1) throw new FormatException("Unknown storage version.");
        var clear = new byte[bytes.Length - 29];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(bytes.AsSpan(1, 12), bytes.AsSpan(29), bytes.AsSpan(13, 16), clear, Encoding.UTF8.GetBytes("AcmeStudio:" + name));
            return Encoding.UTF8.GetString(clear);
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    private async Task Write(string name, string value, CancellationToken token)
    {
        var clear = Encoding.UTF8.GetBytes(value);
        if (clear.Length > 64000) throw new ArgumentException("Storage value is too large.");
        var bytes = new byte[clear.Length + 29]; bytes[0] = 1; RandomNumberGenerator.Fill(bytes.AsSpan(1, 12));
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(bytes.AsSpan(1, 12), clear, bytes.AsSpan(29), bytes.AsSpan(13, 16), Encoding.UTF8.GetBytes("AcmeStudio:" + name));
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
        var temporary = Path.Combine(directory, name + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var file = new FileStream(temporary, options)) { await file.WriteAsync(bytes, token); await file.FlushAsync(token); }
            File.Move(temporary, Path.Combine(directory, name + ".enc"), true);
        }
        finally { File.Delete(temporary); }
    }
    public void Dispose() { CryptographicOperations.ZeroMemory(key); gate.Dispose(); }
}
