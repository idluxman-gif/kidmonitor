using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KidMonitor.Core.Data;

/// <summary>
/// Provides DPAPI-backed EF Core value converters for sensitive monitored content.
/// Values are prefixed so legacy plaintext rows remain readable.
/// </summary>
internal static class ProtectedContentConverter
{
    private const string Prefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KidMonitor.ProtectedContent.v1");

    public static readonly ValueConverter<string, string> RequiredString = new(
        value => Protect(value) ?? string.Empty,
        value => Unprotect(value) ?? string.Empty);

    public static readonly ValueConverter<string?, string?> OptionalString = new(
        value => Protect(value),
        value => Unprotect(value));

    private static string? Protect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Protected content storage requires Windows DPAPI.");
        }

        var plaintext = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.LocalMachine);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    private static string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Protected content storage requires Windows DPAPI.");
        }

        var protectedBytes = Convert.FromBase64String(value[Prefix.Length..]);
        var plaintext = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(plaintext);
    }
}
