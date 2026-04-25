using Microsoft.Extensions.Logging;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Services;

public class OtpServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly OtpService _sut;
    private readonly Mock<IEmailService> _emailServiceMock;

    public OtpServiceTests()
    {
        _db = DbContextFactory.Create();
        _emailServiceMock = new Mock<IEmailService>();
        _sut = new OtpService(
            _db,
            Mock.Of<ILogger<OtpService>>(),
            _emailServiceMock.Object);
    }

    public void Dispose() => _db.Dispose();

    // --- GenerateOtp ---

    [Fact]
    public async Task GenerateOtp_Returns6DigitCode()
    {
        var otp = await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        Assert.Matches(@"^\d{6}$", otp);
    }

    [Fact]
    public async Task GenerateOtp_SavesEntryToDatabase()
    {
        await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        var entry = _db.RegistrationOtps.FirstOrDefault(o => o.PhoneNumber == "0123456789");
        Assert.NotNull(entry);
        Assert.False(entry.IsUsed);
        Assert.Equal("User", entry.FullName);
        Assert.Equal("user@test.com", entry.Email);
    }

    [Fact]
    public async Task GenerateOtp_SetsExpiryTo5Minutes()
    {
        var before = DateTime.UtcNow;

        await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        var entry = _db.RegistrationOtps.First();
        Assert.True(entry.ExpiresAt >= before.AddMinutes(4));
        Assert.True(entry.ExpiresAt <= before.AddMinutes(6));
    }

    [Fact]
    public async Task GenerateOtp_WithEmail_SendsEmail()
    {
        await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        _emailServiceMock.Verify(
            e => e.SendEmailAsync("user@test.com", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateOtp_WithoutEmail_DoesNotSendEmail()
    {
        await _sut.GenerateOtp("0123456789", "User", null);

        _emailServiceMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateOtp_EmailFailure_DoesNotThrow()
    {
        _emailServiceMock
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("SMTP error"));

        var otp = await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        Assert.NotNull(otp);
    }

    [Fact]
    public async Task GenerateOtp_UsesTemplateFromSiteSettings()
    {
        _db.SiteSettings.Add(new SiteSettings
        {
            SiteName = "MySite",
            OtpEmailSubjectTemplate = "[{{siteName}}] Code: {{otpCode}}",
            OtpEmailBodyTemplate = "<p>{{otpCode}} for {{siteName}}</p>"
        });
        await _db.SaveChangesAsync();

        await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "user@test.com",
            It.Is<string>(s => s.Contains("[MySite]")),
            It.Is<string>(s => s.Contains("for MySite"))),
            Times.Once);
    }

    // --- VerifyOtp ---

    [Fact]
    public async Task VerifyOtp_ValidCode_ReturnsEntry()
    {
        var otp = await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        var result = await _sut.VerifyOtp("0123456789", otp);

        Assert.NotNull(result);
        Assert.Equal("0123456789", result.PhoneNumber);
    }

    [Fact]
    public async Task VerifyOtp_WrongCode_ReturnsNull()
    {
        await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        var result = await _sut.VerifyOtp("0123456789", "000000");

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyOtp_WrongPhone_ReturnsNull()
    {
        var otp = await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        var result = await _sut.VerifyOtp("9999999999", otp);

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyOtp_UsedOtp_ReturnsNull()
    {
        var otp = await _sut.GenerateOtp("0123456789", "User", "user@test.com");
        var entry = await _sut.VerifyOtp("0123456789", otp);
        await _sut.MarkAsUsed(entry!);

        var result = await _sut.VerifyOtp("0123456789", otp);

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyOtp_ExpiredOtp_ReturnsNull()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "123456",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            IsUsed = false
        });
        await _db.SaveChangesAsync();

        var result = await _sut.VerifyOtp("0123456789", "123456");

        Assert.Null(result);
    }

    // --- MarkAsUsed ---

    [Fact]
    public async Task MarkAsUsed_SetsIsUsedTrue()
    {
        var otp = await _sut.GenerateOtp("0123456789", "User", null);
        var entry = await _sut.VerifyOtp("0123456789", otp);

        await _sut.MarkAsUsed(entry!);

        var fromDb = await _db.RegistrationOtps.FindAsync(entry!.Id);
        Assert.True(fromDb!.IsUsed);
    }

    // --- CanRequestOtp ---

    [Fact]
    public async Task CanRequestOtp_NoExistingOtp_ReturnsTrue()
    {
        var result = await _sut.CanRequestOtp("0123456789");

        Assert.True(result);
    }

    [Fact]
    public async Task CanRequestOtp_RecentOtpWithin60Seconds_ReturnsFalse()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "123456",
            CreatedAt = DateTime.UtcNow.AddSeconds(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        });
        await _db.SaveChangesAsync();

        var result = await _sut.CanRequestOtp("0123456789");

        Assert.False(result);
    }

    [Fact]
    public async Task CanRequestOtp_UnusedValidOtpExists_ReturnsFalse()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "123456",
            CreatedAt = DateTime.UtcNow.AddSeconds(-90),
            ExpiresAt = DateTime.UtcNow.AddMinutes(3),
            IsUsed = false
        });
        await _db.SaveChangesAsync();

        var result = await _sut.CanRequestOtp("0123456789");

        Assert.False(result);
    }

    // --- ResendOtp ---

    [Fact]
    public async Task ResendOtp_WithinCooldown_ReturnsNull()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "123456",
            CreatedAt = DateTime.UtcNow.AddSeconds(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        });
        await _db.SaveChangesAsync();

        var result = await _sut.ResendOtp("0123456789");

        Assert.Null(result);
    }

    [Fact]
    public async Task ResendOtp_ExceededHourlyLimit_ThrowsInvalidOperationException()
    {
        for (int i = 0; i < 5; i++)
        {
            _db.RegistrationOtps.Add(new RegistrationOtp
            {
                PhoneNumber = "0123456789",
                OtpCode = $"{100000 + i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10 + i),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5 + i),
                IsUsed = true
            });
        }
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ResendOtp("0123456789"));
    }

    // --- CountOtpLastHour ---

    [Fact]
    public async Task CountOtpLastHour_CountsOnlyRecentOtps()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "111111",
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-25),
            IsUsed = true
        });
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "222222",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1.5),
            IsUsed = true
        });
        await _db.SaveChangesAsync();

        var count = await _sut.CountOtpLastHour("0123456789");

        Assert.Equal(1, count);
    }
}
