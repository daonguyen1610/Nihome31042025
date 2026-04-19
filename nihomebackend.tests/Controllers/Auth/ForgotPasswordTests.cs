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

        var result = await host.Controller.ForgotPasswordStart(new ForgotPasswordStartRequest
        {
            PhoneNumber = user.PhoneNumber
        });

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

        var result = await host.Controller.ForgotPasswordResetDirect(new ForgotPasswordCompleteRequest
        {
            PhoneNumber = user.PhoneNumber,
            NewPassword = "newpass123"
        });

        Assert.Equal("Password reset completed.", ActionResultAssert.OkMessage(result));
        Assert.True(host.Passwords.Verify(user, "newpass123"));
    }

    [Fact]
    public async Task ForgotPasswordComplete_WithOtpEnabled_ResetsPasswordAndMarksOtpAsUsed()
    {
        using var host = AuthTestHost.Create();
        var user = host.CreateUser("0900000012", "oldpass123");

        await host.Controller.ForgotPasswordStart(new ForgotPasswordStartRequest
        {
            PhoneNumber = user.PhoneNumber
        });

        var otp = await host.Otps.GetLatestOtp(user.PhoneNumber);
        Assert.NotNull(otp);

        await host.Controller.ForgotPasswordVerifyOtp(new VerifyOtpRequest
        {
            PhoneNumber = user.PhoneNumber,
            OtpCode = otp!.OtpCode
        });

        var result = await host.Controller.ForgotPasswordComplete(new ForgotPasswordCompleteRequest
        {
            PhoneNumber = user.PhoneNumber,
            NewPassword = "newpass123"
        });

        Assert.Equal("Password reset completed.", ActionResultAssert.OkMessage(result));
        Assert.True(otp.IsUsed);
        Assert.True(host.Passwords.Verify(user, "newpass123"));
    }
}
