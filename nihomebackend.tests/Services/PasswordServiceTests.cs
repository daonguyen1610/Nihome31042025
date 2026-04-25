using NihomeBackend.Models;
using NihomeBackend.Services;
using Xunit;

namespace nihomebackend.tests.Services;

public class PasswordServiceTests
{
    private readonly PasswordService _sut = new();

    [Fact]
    public void Hash_ReturnsNonEmptyString()
    {
        var user = new ApplicationUser { Id = 1, PhoneNumber = "0123456789", PasswordHash = "" };

        var hash = _sut.Hash(user, "SecurePass1!");

        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    [Fact]
    public void Hash_DifferentPasswords_ProduceDifferentHashes()
    {
        var user = new ApplicationUser { Id = 1, PhoneNumber = "0123456789", PasswordHash = "" };

        var hash1 = _sut.Hash(user, "Password1!");
        var hash2 = _sut.Hash(user, "Password2!");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var user = new ApplicationUser { Id = 1, PhoneNumber = "0123456789", PasswordHash = "" };
        user.PasswordHash = _sut.Hash(user, "SecurePass1!");

        var result = _sut.Verify(user, "SecurePass1!");

        Assert.True(result);
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var user = new ApplicationUser { Id = 1, PhoneNumber = "0123456789", PasswordHash = "" };
        user.PasswordHash = _sut.Hash(user, "SecurePass1!");

        var result = _sut.Verify(user, "WrongPassword!");

        Assert.False(result);
    }

    [Fact]
    public void Verify_EmptyPassword_ReturnsFalse()
    {
        var user = new ApplicationUser { Id = 1, PhoneNumber = "0123456789", PasswordHash = "" };
        user.PasswordHash = _sut.Hash(user, "SecurePass1!");

        var result = _sut.Verify(user, "");

        Assert.False(result);
    }
}
