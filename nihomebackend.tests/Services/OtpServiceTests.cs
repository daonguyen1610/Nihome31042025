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

    // =============================================
    // GenerateOtp — Normal Cases
    // =============================================

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

    [Fact]
    public async Task GenerateOtp_NoSiteSettings_UsesDefaultTemplate()
    {
        await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "user@test.com",
            It.Is<string>(s => s.Contains("Nihome")),
            It.Is<string>(s => s.Contains("{{otpCode}}") == false && s.Contains("Nihome"))),
            Times.Once);
    }

    [Fact]
    public async Task GenerateOtp_TemplateReplacesOtpCodeInBody()
    {
        _db.SiteSettings.Add(new SiteSettings
        {
            SiteName = "TestSite",
            OtpEmailBodyTemplate = "<p>Code: {{otpCode}}</p>"
        });
        await _db.SaveChangesAsync();

        var otp = await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "user@test.com",
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains($"Code: {otp}"))),
            Times.Once);
    }

    [Fact]
    public async Task GenerateOtp_TemplateReplacesOtpExpireMinutesInBody()
    {
        _db.SiteSettings.Add(new SiteSettings
        {
            SiteName = "TestSite",
            OtpEmailBodyTemplate = "<p>Expires in {{otpExpireMinutes}} min</p>"
        });
        await _db.SaveChangesAsync();

        await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "user@test.com",
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("Expires in 5 min"))),
            Times.Once);
    }

    [Fact]
    public async Task GenerateOtp_MultipleCalls_CreatesSeparateEntries()
    {
        await _sut.GenerateOtp("0123456789", "User1", "user1@test.com");
        await _sut.GenerateOtp("0123456789", "User2", "user2@test.com");

        var count = _db.RegistrationOtps.Count(o => o.PhoneNumber == "0123456789");
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GenerateOtp_DifferentPhones_CreatesSeparateEntries()
    {
        await _sut.GenerateOtp("0111111111", "User1", "u1@test.com");
        await _sut.GenerateOtp("0222222222", "User2", "u2@test.com");

        Assert.Equal(1, _db.RegistrationOtps.Count(o => o.PhoneNumber == "0111111111"));
        Assert.Equal(1, _db.RegistrationOtps.Count(o => o.PhoneNumber == "0222222222"));
    }

    // =============================================
    // GenerateOtp — Abnormal / Edge Cases
    // =============================================

    [Fact]
    public async Task GenerateOtp_WithoutEmail_DoesNotSendEmail()
    {
        await _sut.GenerateOtp("0123456789", "User", null);

        _emailServiceMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateOtp_EmptyEmail_DoesNotSendEmail()
    {
        await _sut.GenerateOtp("0123456789", "User", "");

        _emailServiceMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateOtp_WhitespaceEmail_DoesNotSendEmail()
    {
        await _sut.GenerateOtp("0123456789", "User", "   ");

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
    public async Task GenerateOtp_EmailFailure_StillSavesOtpToDatabase()
    {
        _emailServiceMock
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("SMTP error"));

        var otp = await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        var entry = _db.RegistrationOtps.FirstOrDefault(o => o.PhoneNumber == "0123456789");
        Assert.NotNull(entry);
        Assert.Equal(otp, entry.OtpCode);
    }

    [Fact]
    public async Task GenerateOtp_NullFullName_SavesNullFullName()
    {
        await _sut.GenerateOtp("0123456789", null, "user@test.com");

        var entry = _db.RegistrationOtps.First();
        Assert.Null(entry.FullName);
    }

    [Fact]
    public async Task GenerateOtp_EmptyFullName_SavesEmptyFullName()
    {
        await _sut.GenerateOtp("0123456789", "", "user@test.com");

        var entry = _db.RegistrationOtps.First();
        Assert.Equal("", entry.FullName);
    }

    [Fact]
    public async Task GenerateOtp_SiteSettingsWithEmptySiteName_UsesEmptyInTemplate()
    {
        _db.SiteSettings.Add(new SiteSettings
        {
            SiteName = "",
            OtpEmailSubjectTemplate = "[{{siteName}}] Your code"
        });
        await _db.SaveChangesAsync();

        await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        _emailServiceMock.Verify(e => e.SendEmailAsync(
            "user@test.com",
            It.Is<string>(s => s.Contains("[] Your code")),
            It.IsAny<string>()),
            Times.Once);
    }

    // =============================================
    // VerifyOtp — Normal Cases
    // =============================================

    [Fact]
    public async Task VerifyOtp_ValidCode_ReturnsEntry()
    {
        var otp = await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        var result = await _sut.VerifyOtp("0123456789", otp);

        Assert.NotNull(result);
        Assert.Equal("0123456789", result.PhoneNumber);
    }

    [Fact]
    public async Task VerifyOtp_ValidCode_ReturnsCorrectFields()
    {
        var otp = await _sut.GenerateOtp("0123456789", "Test User", "test@test.com");

        var result = await _sut.VerifyOtp("0123456789", otp);

        Assert.NotNull(result);
        Assert.Equal("Test User", result.FullName);
        Assert.Equal("test@test.com", result.Email);
        Assert.Equal(otp, result.OtpCode);
        Assert.False(result.IsUsed);
    }

    [Fact]
    public async Task VerifyOtp_MultipleOtpsForSamePhone_ReturnsLatest()
    {
        var otp1 = await _sut.GenerateOtp("0123456789", "User1", "u1@test.com");
        var otp2 = await _sut.GenerateOtp("0123456789", "User2", "u2@test.com");

        var result = await _sut.VerifyOtp("0123456789", otp2);

        Assert.NotNull(result);
        Assert.Equal("User2", result.FullName);
    }

    // =============================================
    // VerifyOtp — Abnormal / Edge Cases
    // =============================================

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

    [Fact]
    public async Task VerifyOtp_NoOtpExists_ReturnsNull()
    {
        var result = await _sut.VerifyOtp("0123456789", "123456");

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyOtp_EmptyCode_ReturnsNull()
    {
        await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        var result = await _sut.VerifyOtp("0123456789", "");

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyOtp_BothWrongPhoneAndCode_ReturnsNull()
    {
        await _sut.GenerateOtp("0123456789", "User", "user@test.com");

        var result = await _sut.VerifyOtp("9999999999", "000000");

        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyOtp_OtpFromDifferentPhone_ReturnsNull()
    {
        var otp1 = await _sut.GenerateOtp("0111111111", "User1", null);
        await _sut.GenerateOtp("0222222222", "User2", null);

        var result = await _sut.VerifyOtp("0222222222", otp1);

        Assert.Null(result);
    }

    // =============================================
    // MarkAsUsed — Normal Cases
    // =============================================

    [Fact]
    public async Task MarkAsUsed_SetsIsUsedTrue()
    {
        var otp = await _sut.GenerateOtp("0123456789", "User", null);
        var entry = await _sut.VerifyOtp("0123456789", otp);

        await _sut.MarkAsUsed(entry!);

        var fromDb = await _db.RegistrationOtps.FindAsync(entry!.Id);
        Assert.True(fromDb!.IsUsed);
    }

    [Fact]
    public async Task MarkAsUsed_DoesNotAffectOtherEntries()
    {
        var otp1 = await _sut.GenerateOtp("0123456789", "User1", null);
        var otp2 = await _sut.GenerateOtp("0123456789", "User2", null);
        var entry1 = _db.RegistrationOtps.First(o => o.OtpCode == otp1);

        await _sut.MarkAsUsed(entry1);

        var entry2 = _db.RegistrationOtps.First(o => o.OtpCode == otp2);
        Assert.False(entry2.IsUsed);
    }

    // =============================================
    // GetLatestOtp — Normal Cases
    // =============================================

    [Fact]
    public async Task GetLatestOtp_ReturnsLatestEntry()
    {
        await _sut.GenerateOtp("0123456789", "User1", "u1@test.com");
        await _sut.GenerateOtp("0123456789", "User2", "u2@test.com");

        var result = await _sut.GetLatestOtp("0123456789");

        Assert.NotNull(result);
        Assert.Equal("User2", result.FullName);
    }

    [Fact]
    public async Task GetLatestOtp_ReturnsEntryRegardlessOfUsedState()
    {
        var otp = await _sut.GenerateOtp("0123456789", "User", null);
        var entry = await _sut.VerifyOtp("0123456789", otp);
        await _sut.MarkAsUsed(entry!);

        var result = await _sut.GetLatestOtp("0123456789");

        Assert.NotNull(result);
        Assert.True(result.IsUsed);
    }

    // =============================================
    // GetLatestOtp — Abnormal / Edge Cases
    // =============================================

    [Fact]
    public async Task GetLatestOtp_NoEntries_ReturnsNull()
    {
        var result = await _sut.GetLatestOtp("0123456789");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestOtp_DifferentPhone_ReturnsNull()
    {
        await _sut.GenerateOtp("0111111111", "User1", null);

        var result = await _sut.GetLatestOtp("0222222222");

        Assert.Null(result);
    }

    // =============================================
    // CanRequestOtp — Normal Cases
    // =============================================

    [Fact]
    public async Task CanRequestOtp_NoExistingOtp_ReturnsTrue()
    {
        var result = await _sut.CanRequestOtp("0123456789");

        Assert.True(result);
    }

    [Fact]
    public async Task CanRequestOtp_UsedAndExpiredOtp_After60Seconds_ReturnsTrue()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "123456",
            CreatedAt = DateTime.UtcNow.AddSeconds(-90),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            IsUsed = true
        });
        await _db.SaveChangesAsync();

        var result = await _sut.CanRequestOtp("0123456789");

        Assert.True(result);
    }

    [Fact]
    public async Task CanRequestOtp_DifferentPhone_ReturnsTrue()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0111111111",
            OtpCode = "123456",
            CreatedAt = DateTime.UtcNow.AddSeconds(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        });
        await _db.SaveChangesAsync();

        var result = await _sut.CanRequestOtp("0222222222");

        Assert.True(result);
    }

    // =============================================
    // CanRequestOtp — Abnormal / Edge Cases
    // =============================================

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

    [Fact]
    public async Task CanRequestOtp_RecentOtpAt59Seconds_ReturnsFalse()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "123456",
            CreatedAt = DateTime.UtcNow.AddSeconds(-59),
            ExpiresAt = DateTime.UtcNow.AddMinutes(4),
            IsUsed = false
        });
        await _db.SaveChangesAsync();

        var result = await _sut.CanRequestOtp("0123456789");

        Assert.False(result);
    }

    [Fact]
    public async Task CanRequestOtp_UsedOtpWithin60Seconds_ReturnsFalse()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "123456",
            CreatedAt = DateTime.UtcNow.AddSeconds(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = true
        });
        await _db.SaveChangesAsync();

        var result = await _sut.CanRequestOtp("0123456789");

        Assert.False(result);
    }

    // =============================================
    // ResendOtp — Normal Cases
    // =============================================

    [Fact]
    public async Task ResendOtp_Eligible_ReturnsNewOtpCode()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "111111",
            FullName = "User",
            Email = "u@t.com",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            IsUsed = true
        });
        await _db.SaveChangesAsync();

        var result = await _sut.ResendOtp("0123456789");

        Assert.NotNull(result);
        Assert.Matches(@"^\d{6}$", result);
    }

    [Fact]
    public async Task ResendOtp_Eligible_PreservesFullNameAndEmail()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "111111",
            FullName = "OriginalName",
            Email = "original@test.com",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            IsUsed = true
        });
        await _db.SaveChangesAsync();

        await _sut.ResendOtp("0123456789");

        var latest = _db.RegistrationOtps.OrderByDescending(o => o.Id).First();
        Assert.Equal("original@test.com", latest.Email);
    }

    [Fact]
    public async Task ResendOtp_NoExistingOtp_GeneratesNewOtp()
    {
        var result = await _sut.ResendOtp("0123456789");

        Assert.NotNull(result);
        Assert.Single(_db.RegistrationOtps.Where(o => o.PhoneNumber == "0123456789"));
    }

    [Fact]
    public async Task ResendOtp_NoPreviousFullNameOrEmail_UsesEmptyStringAndNull()
    {
        var result = await _sut.ResendOtp("0123456789");

        Assert.NotNull(result);
        var entry = _db.RegistrationOtps.First();
        Assert.Equal(string.Empty, entry.FullName);
        Assert.Null(entry.Email);
    }

    // =============================================
    // ResendOtp — Abnormal / Edge Cases
    // =============================================

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
    public async Task ResendOtp_ExactlyAt5RequestsInLastHour_ThrowsInvalidOperationException()
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

    [Fact]
    public async Task ResendOtp_MoreThan5RequestsInLastHour_ThrowsInvalidOperationException()
    {
        for (int i = 0; i < 7; i++)
        {
            _db.RegistrationOtps.Add(new RegistrationOtp
            {
                PhoneNumber = "0123456789",
                OtpCode = $"{100000 + i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-50 + i),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-45 + i),
                IsUsed = true
            });
        }
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ResendOtp("0123456789"));
    }

    [Fact]
    public async Task ResendOtp_4RequestsInLastHour_Succeeds()
    {
        for (int i = 0; i < 4; i++)
        {
            _db.RegistrationOtps.Add(new RegistrationOtp
            {
                PhoneNumber = "0123456789",
                OtpCode = $"{100000 + i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-50 + i),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-45 + i),
                IsUsed = true
            });
        }
        await _db.SaveChangesAsync();

        var result = await _sut.ResendOtp("0123456789");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ResendOtp_5RequestsButAllOlderThanOneHour_Succeeds()
    {
        for (int i = 0; i < 5; i++)
        {
            _db.RegistrationOtps.Add(new RegistrationOtp
            {
                PhoneNumber = "0123456789",
                OtpCode = $"{100000 + i}",
                CreatedAt = DateTime.UtcNow.AddHours(-2).AddMinutes(i),
                ExpiresAt = DateTime.UtcNow.AddHours(-1.5).AddMinutes(i),
                IsUsed = true
            });
        }
        await _db.SaveChangesAsync();

        var result = await _sut.ResendOtp("0123456789");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ResendOtp_CooldownPassed_ButUnusedValidOtpExists_ReturnsNull()
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

        var result = await _sut.ResendOtp("0123456789");

        Assert.Null(result);
    }

    // =============================================
    // CountOtpLastHour — Normal Cases
    // =============================================

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

    [Fact]
    public async Task CountOtpLastHour_NoEntries_ReturnsZero()
    {
        var count = await _sut.CountOtpLastHour("0123456789");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountOtpLastHour_OnlyCountsMatchingPhone()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0111111111",
            OtpCode = "111111",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            IsUsed = true
        });
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0222222222",
            OtpCode = "222222",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            IsUsed = true
        });
        await _db.SaveChangesAsync();

        var count = await _sut.CountOtpLastHour("0111111111");

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CountOtpLastHour_CountsBothUsedAndUnusedOtps()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "111111",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            IsUsed = true
        });
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "222222",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(1),
            IsUsed = false
        });
        await _db.SaveChangesAsync();

        var count = await _sut.CountOtpLastHour("0123456789");

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CountOtpLastHour_AllOlderThanOneHour_ReturnsZero()
    {
        _db.RegistrationOtps.Add(new RegistrationOtp
        {
            PhoneNumber = "0123456789",
            OtpCode = "111111",
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            ExpiresAt = DateTime.UtcNow.AddHours(-2),
            IsUsed = true
        });
        await _db.SaveChangesAsync();

        var count = await _sut.CountOtpLastHour("0123456789");

        Assert.Equal(0, count);
    }
}
