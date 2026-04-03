using KidMonitor.Core.Security;

namespace KidMonitor.Tests.Security;

public sealed class EncryptionServiceTests
{
    private readonly IEncryptionService _sut = new WindowsDpapiEncryptionService();

    [Fact]
    public void EncryptDecrypt_RoundTripsSensitiveText()
    {
        const string plaintext = "discord message with badword";

        var ciphertext = _sut.Encrypt(plaintext);
        var decrypted = _sut.Decrypt(ciphertext);

        Assert.NotEqual(plaintext, ciphertext);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_LeavesEmptyStringUntouched()
    {
        Assert.Equal(string.Empty, _sut.Encrypt(string.Empty));
        Assert.Equal(string.Empty, _sut.Decrypt(string.Empty));
    }
}
