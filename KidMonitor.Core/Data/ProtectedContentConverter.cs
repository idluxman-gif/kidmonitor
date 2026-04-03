using KidMonitor.Core.Security;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KidMonitor.Core.Data;

/// <summary>
/// Builds EF Core value converters for sensitive monitored content.
/// </summary>
internal static class ProtectedContentConverter
{
    public static ValueConverter<string, string> CreateRequiredString(IEncryptionService encryptionService) => new(
        value => encryptionService.Encrypt(value),
        value => encryptionService.Decrypt(value));

    public static ValueConverter<string?, string?> CreateOptionalString(IEncryptionService encryptionService) => new(
        value => value == null ? null : encryptionService.Encrypt(value),
        value => value == null ? null : encryptionService.Decrypt(value));
}
