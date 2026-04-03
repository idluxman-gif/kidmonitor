using System.Security.Cryptography;
using System.Text;

namespace KidMonitor.Core.Security;

/// <summary>
/// Encrypts and decrypts sensitive monitored content before it is persisted.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext for at-rest storage.
    /// </summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts ciphertext read from storage.
    /// Legacy plaintext values are returned unchanged.
    /// </summary>
    string Decrypt(string ciphertext);
}

/// <summary>
/// Windows DPAPI implementation backed by machine-local protection.
/// </summary>
public sealed class WindowsDpapiEncryptionService : IEncryptionService
{
    private const string Prefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KidMonitor.ProtectedContent.v1");

    /// <summary>
    /// Shared default instance used by contexts created outside DI.
    /// </summary>
    public static IEncryptionService Shared { get; } = new WindowsDpapiEncryptionService();

    /// <inheritdoc />
    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }

        if (plaintext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return plaintext;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Protected content storage requires Windows DPAPI.");
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.LocalMachine);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    /// <inheritdoc />
    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return ciphertext;
        }

        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return ciphertext;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Protected content storage requires Windows DPAPI.");
        }

        var protectedBytes = Convert.FromBase64String(ciphertext[Prefix.Length..]);
        var plaintextBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
