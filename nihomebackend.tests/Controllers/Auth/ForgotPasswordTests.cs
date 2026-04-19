using NihomeBackend.Models.DTOs.Requests.Auth;
using nihomebackend.tests.Infrastructure;

namespace nihomebackend.tests.Controllers.Auth;

public sealed class ForgotPasswordTests
{
    [Fact]
    public async Task ForgotPasswordStart_WithOtpEnabled_CreatesOtpSessionAndSendsEmail()
    {
        using var host = AuthTestHost.Create();
        var user = host.CreateUser("0900000010", "oldpass123", email: "forgot@example.com");

        var result = await host.StartForgotPassword(user.PhoneNumber);

        Assert.Equal("OTP code sent to email.", ActionResultAssert.OkMessage(result));

        var otp = await host.Otps.GetLatestOtp(user.PhoneNumber);
        var email = Assert.Single(host.Email.SentEmails);

        Assert.NotNull(otp);
        Assert.Equal(user.Email, email.ToEmail);
        Assert.Contains(otp!.OtpCode, email.HtmlContent);
    }

    [Fact]
    public async Task ForgotPasswordResetDirect_WithOtpDisabled_UpdatesPassword()
    {
        using var host = AuthTestHost.Create(enableOtpForForgotPassword: false);
        var user = host.CreateUser("0900000011", "oldpass123");

        var result = await host.ResetForgotPasswordDirect(user.PhoneNumber, "newpass123");

        Assert.Equal("Password reset completed.", ActionResultAssert.OkMessage(result));
        Assert.True(host.Passwords.Verify(user, "newpass123"));
    }

    [Fact]
    public async Task ForgotPasswordComplete_WithOtpEnabled_ResetsPasswordAndMarksOtpAsUsed()
    {
        using var host = AuthTestHost.Create();
        var user = host.CreateUser("0900000012", "oldpass123");

        await host.StartForgotPassword(user.PhoneNumber);

        var otp = await host.Otps.GetLatestOtp(user.PhoneNumber);
        Assert.NotNull(otp);

        await host.VerifyForgotPasswordOtp(user.PhoneNumber, otp!.OtpCode);

        var result = await host.CompleteForgotPassword(user.PhoneNumber, "newpass123");

        Assert.Equal("Password reset completed.", ActionResultAssert.OkMessage(result));
        Assert.True(otp.IsUsed);
        Assert.True(host.Passwords.Verify(user, "newpass123"));
    }

    [Fact]
    public async Task ForgotPasswordStart_WithUnknownPhoneNumber_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();

        var result = await host.StartForgotPassword("0900000013");

        Assert.Equal("Account not found.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ForgotPasswordResetDirect_WhenOtpIsEnabled_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();
        var user = host.CreateUser("0900000014", "oldpass123");

        var result = await host.ResetForgotPasswordDirect(user.PhoneNumber, "newpass123");

        Assert.Equal(
            "OTP verification is required. Please use the standard forgot password flow.",
            ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ForgotPasswordVerifyOtp_WithInvalidCode_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();

        var result = await host.VerifyForgotPasswordOtp("0900000015", "000000");

        Assert.Equal("Invalid OTP.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ForgotPasswordComplete_WithUnknownAccount_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();

        var result = await host.CompleteForgotPassword("0900000016", "newpass123");

        Assert.Equal("Account not found.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ForgotPasswordComplete_WithoutOtpSession_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();
        var user = host.CreateUser("0900000017", "oldpass123");

        var result = await host.CompleteForgotPassword(user.PhoneNumber, "newpass123");

        Assert.Equal("OTP session not found or expired.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ResendForgotOtp_WhenOtpIsDisabled_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create(enableOtpForForgotPassword: false);

        var result = await host.ResendForgotOtp("0900000018");

        Assert.Equal("OTP verification is disabled for forgot password.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ResendForgotOtp_WithUnknownPhoneNumber_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();

        var result = await host.ResendForgotOtp("0900000019");

        Assert.Equal("Account not found.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ResendForgotOtp_TooSoon_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();
        var user = host.CreateUser("0900000024", "oldpass123", email: "forgot.resend@example.com");
        await host.StartForgotPassword(user.PhoneNumber);

        var result = await host.ResendForgotOtp(user.PhoneNumber);

        Assert.Equal("Please wait before requesting a new OTP.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ResendForgotOtp_WhenHourlyLimitIsReached_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();
        var user = host.CreateUser("0900000027", "oldpass123", email: "forgot.limit@example.com");
        host.SeedOtpRateLimit(user.PhoneNumber, email: user.Email);

        var result = await host.ResendForgotOtp(user.PhoneNumber);

        Assert.Equal("OTP request limit exceeded.", ActionResultAssert.BadRequestMessage(result));
    }
}
