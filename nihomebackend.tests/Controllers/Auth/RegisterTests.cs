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

        var result = await host.StartRegister("0900000001");

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

        var result = await host.StartRegister(phoneNumber, "OTP User", "otp@example.com");

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

        await host.StartRegister(phoneNumber, "OTP User", "otp.complete@example.com");

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

    [Fact]
    public async Task StartRegister_WithExistingPhoneNumber_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();
        host.CreateUser("0900000004", "secret123");

        var result = await host.StartRegister("0900000004");

        Assert.Equal("Phone number already registered.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task VerifyOtp_WithInvalidCode_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();

        var result = await host.Controller.VerifyOtp(new VerifyOtpRequest
        {
            PhoneNumber = "0900000005",
            OtpCode = "000000"
        });

        Assert.Equal("Invalid OTP.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task CompleteRegister_WhenOtpIsDisabled_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create(enableOtpForRegistration: false);

        var result = await host.Controller.CompleteRegister(new RegistrationCompleteRequest
        {
            PhoneNumber = "0900000006",
            Password = "secret123"
        });

        Assert.Equal(
            "OTP verification is disabled. Complete registration from register/start.",
            ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task CompleteRegister_WithoutOtpSession_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();

        var result = await host.Controller.CompleteRegister(new RegistrationCompleteRequest
        {
            PhoneNumber = "0900000007",
            Password = "secret123"
        });

        Assert.Equal("OTP session not found or expired.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task CompleteRegister_WithUsedOtp_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();
        host.AddOtp("0900000008", isUsed: true);

        var result = await host.Controller.CompleteRegister(new RegistrationCompleteRequest
        {
            PhoneNumber = "0900000008",
            Password = "secret123"
        });

        Assert.Equal("OTP session not found or expired.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ResendRegisterOtp_WhenOtpIsDisabled_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create(enableOtpForRegistration: false);

        var result = await host.Controller.ResendRegisterOtp(new ResendOtpRequest
        {
            PhoneNumber = "0900000009"
        });

        Assert.Equal("OTP verification is disabled for registration.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ResendRegisterOtp_WithExistingUser_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();
        host.CreateUser("0900000013", "secret123");

        var result = await host.Controller.ResendRegisterOtp(new ResendOtpRequest
        {
            PhoneNumber = "0900000013"
        });

        Assert.Equal("Phone number already registered.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ResendRegisterOtp_TooSoon_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();
        const string phoneNumber = "0900000014";
        await host.StartRegister(phoneNumber, "OTP User", "register.resend@example.com");

        var result = await host.Controller.ResendRegisterOtp(new ResendOtpRequest
        {
            PhoneNumber = phoneNumber
        });

        Assert.Equal("Please wait before requesting a new OTP.", ActionResultAssert.BadRequestMessage(result));
    }

    [Fact]
    public async Task ResendRegisterOtp_WhenHourlyLimitIsReached_ReturnsBadRequest()
    {
        using var host = AuthTestHost.Create();
        const string phoneNumber = "0900000015";
        host.SeedOtpRateLimit(phoneNumber, email: "register.limit@example.com");

        var result = await host.Controller.ResendRegisterOtp(new ResendOtpRequest
        {
            PhoneNumber = phoneNumber
        });

        Assert.Equal("OTP request limit exceeded.", ActionResultAssert.BadRequestMessage(result));
    }
}
