using NihomeBackend.Models.DTOs.Requests.Auth;
using NihomeBackend.Models.DTOs.Responses;
using nihomebackend.tests.Infrastructure;

namespace nihomebackend.tests.Controllers.Auth;

public sealed class RegisterTests
{
    [Fact]
    public async Task StartRegister_WithOtpDisabled_CreatesUserAndReturnsAuthResponse()
    {
        using var host = AuthTestHost.Create(enableOtpForRegistration: false);

        var result = await host.Controller.StartRegister(new RegisterStartRequest
        {
            PhoneNumber = "0900000001",
            FullName = "Test User",
            Email = "test@example.com",
            Password = "secret123"
        });

        var response = ActionResultAssert.Ok<AuthResponse>(result);

        Assert.False(response.OtpRequired);
        Assert.Equal("0900000001", response.PhoneNumber);
        Assert.NotEmpty(response.AccessToken);
        Assert.NotEmpty(response.RefreshToken);
        Assert.NotNull(host.FindUser("0900000001"));
    }

    [Fact]
    public async Task StartRegister_WithOtpEnabled_CreatesOtpSessionAndSendsEmail()
    {
        using var host = AuthTestHost.Create();
        const string phoneNumber = "0900000002";

        var result = await host.Controller.StartRegister(new RegisterStartRequest
        {
            PhoneNumber = phoneNumber,
            FullName = "OTP User",
            Email = "otp@example.com",
            Password = "secret123"
        });

        Assert.Equal("OTP code sent to email.", ActionResultAssert.OkMessage(result));

        var otp = await host.Otps.GetLatestOtp(phoneNumber);
        var email = Assert.Single(host.Email.SentEmails);

        Assert.NotNull(otp);
        Assert.Equal("otp@example.com", email.ToEmail);
        Assert.Contains(otp!.OtpCode, email.HtmlContent);
        Assert.Null(host.FindUser(phoneNumber));
    }

    [Fact]
    public async Task CompleteRegister_WithVerifiedOtp_CreatesUserAndMarksOtpAsUsed()
    {
        using var host = AuthTestHost.Create();
        const string phoneNumber = "0900000003";

        await host.Controller.StartRegister(new RegisterStartRequest
        {
            PhoneNumber = phoneNumber,
            FullName = "OTP User",
            Email = "otp.complete@example.com",
            Password = "secret123"
        });

        var otp = await host.Otps.GetLatestOtp(phoneNumber);
        Assert.NotNull(otp);

        await host.Controller.VerifyOtp(new VerifyOtpRequest
        {
            PhoneNumber = phoneNumber,
            OtpCode = otp!.OtpCode
        });

        var result = await host.Controller.CompleteRegister(new RegistrationCompleteRequest
        {
            PhoneNumber = phoneNumber,
            Password = "secret123"
        });

        var response = ActionResultAssert.Ok<AuthResponse>(result);

        Assert.Equal(phoneNumber, response.PhoneNumber);
        Assert.NotEmpty(response.AccessToken);
        Assert.NotEmpty(response.RefreshToken);
        Assert.True(otp.IsUsed);
        Assert.NotNull(host.FindUser(phoneNumber));
    }
}
