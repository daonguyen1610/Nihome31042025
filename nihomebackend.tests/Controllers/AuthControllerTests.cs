using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Mappings;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests.Auth;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PasswordService _passwordService;
    private readonly JwtService _jwtService;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly OtpService _otpService;
    private readonly IMapper _mapper;
    private readonly JwtOptions _jwtOptions;
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _db = DbContextFactory.Create();
        _passwordService = new PasswordService();

        _jwtOptions = new JwtOptions
        {
            Issuer = "test",
            Audience = "test",
            AccessTokenMinutes = 30,
            RefreshTokenDays = 7,
            ActiveKeyId = "key1",
            Keys = new Dictionary<string, string>
            {
                { "key1", "this-is-a-test-secret-key-that-must-be-at-least-32-chars!" }
            }
        };

        _jwtService = new JwtService(
            Options.Create(_jwtOptions),
            Mock.Of<ILogger<JwtService>>());

        _refreshTokenService = new RefreshTokenService(
            _db, Options.Create(_jwtOptions));

        var emailServiceMock = new Mock<IEmailService>();
        _otpService = new OtpService(
            _db, Mock.Of<ILogger<OtpService>>(), emailServiceMock.Object);

        var mapperConfig = new MapperConfiguration(cfg =>
            cfg.AddProfile<AutoMapperProfile>());
        _mapper = mapperConfig.CreateMapper();

        _sut = new AuthController(
            _db,
            _passwordService,
            _jwtService,
            _refreshTokenService,
            _otpService,
            _mapper,
            Options.Create(_jwtOptions),
            new NoOpAuditLogger(),
            Mock.Of<ILogger<AuthController>>());
    }

    public void Dispose() => _db.Dispose();

    // --- Registration (OTP disabled) ---

    [Fact]
    public async Task StartRegister_OtpDisabled_CreatesUserAndReturnsAuthResponse()
    {
        await SeedSettings(enableOtpForRegistration: false);

        var result = await _sut.StartRegister(new RegisterStartRequest
        {
            PhoneNumber = "0123456789",
            FullName = "New User",
            Email = "new@test.com",
            Password = "SecurePass1!"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(ok.Value);
        Assert.Equal("0123456789", response.PhoneNumber);
        Assert.False(response.OtpRequired);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
    }

    [Fact]
    public async Task StartRegister_DuplicatePhone_ReturnsBadRequest()
    {
        await SeedUser("0123456789");
        await SeedSettings(enableOtpForRegistration: false);

        var result = await _sut.StartRegister(new RegisterStartRequest
        {
            PhoneNumber = "0123456789",
            FullName = "Dup User",
            Password = "Pass1!"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task StartRegister_OtpEnabled_ReturnsOtpRequired()
    {
        await SeedSettings(enableOtpForRegistration: true);

        var result = await _sut.StartRegister(new RegisterStartRequest
        {
            PhoneNumber = "0123456789",
            FullName = "New User",
            Email = "new@test.com",
            Password = "SecurePass1!"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = ok.Value;
        var otpRequired = json?.GetType().GetProperty("otpRequired")?.GetValue(json);
        Assert.Equal(true, otpRequired);
    }

    // --- Registration Complete (OTP enabled) ---

    [Fact]
    public async Task CompleteRegister_OtpDisabled_ReturnsBadRequest()
    {
        await SeedSettings(enableOtpForRegistration: false);

        var result = await _sut.CompleteRegister(new RegistrationCompleteRequest
        {
            PhoneNumber = "0123456789",
            Password = "Pass1!"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CompleteRegister_ValidOtp_CreatesUserAndReturnsAuth()
    {
        await SeedSettings(enableOtpForRegistration: true);
        var otp = await _otpService.GenerateOtp("0123456789", "Test User", "t@t.com");

        var result = await _sut.CompleteRegister(new RegistrationCompleteRequest
        {
            PhoneNumber = "0123456789",
            Password = "SecurePass1!"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<AuthResponse>(ok.Value);
        Assert.Single(_db.Users.Where(u => u.PhoneNumber == "0123456789"));
    }

    [Fact]
    public async Task CompleteRegister_DuplicatePhone_ReturnsBadRequest()
    {
        await SeedUser("0123456789");
        await SeedSettings(enableOtpForRegistration: true);

        var result = await _sut.CompleteRegister(new RegistrationCompleteRequest
        {
            PhoneNumber = "0123456789",
            Password = "Pass1!"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // --- Login ---

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAuthResponse()
    {
        await SeedUser("0123456789", password: "SecurePass1!");

        var result = await _sut.Login(new LoginRequest
        {
            PhoneNumber = "0123456789",
            Password = "SecurePass1!"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        await SeedUser("0123456789", password: "SecurePass1!");

        var result = await _sut.Login(new LoginRequest
        {
            PhoneNumber = "0123456789",
            Password = "WrongPass!"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        var result = await _sut.Login(new LoginRequest
        {
            PhoneNumber = "9999999999",
            Password = "Pass1!"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_InactiveUser_ReturnsUnauthorized()
    {
        await SeedUser("0123456789", password: "SecurePass1!", isActive: false);

        var result = await _sut.Login(new LoginRequest
        {
            PhoneNumber = "0123456789",
            Password = "SecurePass1!"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // --- Refresh ---

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewAuthResponse()
    {
        var user = await SeedUser("0123456789");
        var token = await _refreshTokenService.IssueAsync(user);

        var result = await _sut.Refresh(new RefreshRequest { RefreshToken = token.Token });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.NotEqual(token.Token, response.RefreshToken);
    }

    [Fact]
    public async Task Refresh_InvalidToken_ReturnsUnauthorized()
    {
        var result = await _sut.Refresh(new RefreshRequest { RefreshToken = "invalid" });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_RevokesOldToken()
    {
        var user = await SeedUser("0123456789");
        var token = await _refreshTokenService.IssueAsync(user);

        await _sut.Refresh(new RefreshRequest { RefreshToken = token.Token });

        var old = await _db.RefreshTokens.FindAsync(token.Id);
        Assert.True(old!.IsRevoked);
    }

    // --- Logout ---

    [Fact]
    public async Task Logout_ValidToken_RevokesAndReturnsOk()
    {
        var user = await SeedUser("0123456789");
        var token = await _refreshTokenService.IssueAsync(user);

        var result = await _sut.Logout(new RefreshRequest { RefreshToken = token.Token });

        Assert.IsType<OkObjectResult>(result);
        var fromDb = await _db.RefreshTokens.FindAsync(token.Id);
        Assert.True(fromDb!.IsRevoked);
    }

    [Fact]
    public async Task Logout_InvalidToken_StillReturnsOk()
    {
        var result = await _sut.Logout(new RefreshRequest { RefreshToken = "invalid" });

        Assert.IsType<OkObjectResult>(result);
    }

    // --- Forgot Password ---

    [Fact]
    public async Task ForgotPasswordStart_UserNotFound_ReturnsBadRequest()
    {
        var result = await _sut.ForgotPasswordStart(new ForgotPasswordStartRequest
        {
            PhoneNumber = "9999999999"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ForgotPasswordStart_OtpDisabled_ReturnsDirectResetOption()
    {
        await SeedUser("0123456789");
        await SeedSettings(enableOtpForForgotPassword: false);

        var result = await _sut.ForgotPasswordStart(new ForgotPasswordStartRequest
        {
            PhoneNumber = "0123456789"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = ok.Value;
        var otpRequired = json?.GetType().GetProperty("otpRequired")?.GetValue(json);
        Assert.Equal(false, otpRequired);
    }

    [Fact]
    public async Task ForgotPasswordStart_OtpEnabled_ReturnsOtpRequired()
    {
        await SeedUser("0123456789");
        await SeedSettings(enableOtpForForgotPassword: true);

        var result = await _sut.ForgotPasswordStart(new ForgotPasswordStartRequest
        {
            PhoneNumber = "0123456789"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = ok.Value;
        var otpRequired = json?.GetType().GetProperty("otpRequired")?.GetValue(json);
        Assert.Equal(true, otpRequired);
    }

    [Fact]
    public async Task ForgotPasswordResetDirect_OtpEnabled_ReturnsBadRequest()
    {
        await SeedSettings(enableOtpForForgotPassword: true);

        var result = await _sut.ForgotPasswordResetDirect(new ForgotPasswordCompleteRequest
        {
            PhoneNumber = "0123456789",
            NewPassword = "NewPass1!"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ForgotPasswordResetDirect_OtpDisabled_ResetsPassword()
    {
        var user = await SeedUser("0123456789", password: "OldPass1!");
        await SeedSettings(enableOtpForForgotPassword: false);

        var result = await _sut.ForgotPasswordResetDirect(new ForgotPasswordCompleteRequest
        {
            PhoneNumber = "0123456789",
            NewPassword = "NewPass1!"
        });

        Assert.IsType<OkObjectResult>(result);
        await _db.Entry(user).ReloadAsync();
        Assert.True(_passwordService.Verify(user, "NewPass1!"));
    }

    [Fact]
    public async Task ForgotPasswordComplete_UserNotFound_ReturnsBadRequest()
    {
        var result = await _sut.ForgotPasswordComplete(new ForgotPasswordCompleteRequest
        {
            PhoneNumber = "9999999999",
            NewPassword = "NewPass1!"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // --- Resend OTP ---

    [Fact]
    public async Task ResendRegisterOtp_OtpDisabled_ReturnsBadRequest()
    {
        await SeedSettings(enableOtpForRegistration: false);

        var result = await _sut.ResendRegisterOtp(new ResendOtpRequest
        {
            PhoneNumber = "0123456789"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResendRegisterOtp_AlreadyRegistered_ReturnsBadRequest()
    {
        await SeedUser("0123456789");
        await SeedSettings(enableOtpForRegistration: true);

        var result = await _sut.ResendRegisterOtp(new ResendOtpRequest
        {
            PhoneNumber = "0123456789"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResendForgotOtp_OtpDisabled_ReturnsBadRequest()
    {
        await SeedSettings(enableOtpForForgotPassword: false);

        var result = await _sut.ResendForgotOtp(new ResendOtpRequest
        {
            PhoneNumber = "0123456789"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResendForgotOtp_UserNotFound_ReturnsBadRequest()
    {
        await SeedSettings(enableOtpForForgotPassword: true);

        var result = await _sut.ResendForgotOtp(new ResendOtpRequest
        {
            PhoneNumber = "9999999999"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // --- Helpers ---

    private async Task<ApplicationUser> SeedUser(
        string phone = "0123456789",
        string password = "SecurePass1!",
        bool isActive = true)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = "Test User",
            Role = UserRole.USER,
            IsActive = isActive
        };
        user.PasswordHash = _passwordService.Hash(user, password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task SeedSettings(
        bool enableOtpForRegistration = false,
        bool enableOtpForForgotPassword = false)
    {
        _db.SiteSettings.Add(new SiteSettings
        {
            EnableOtpForRegistration = enableOtpForRegistration,
            EnableOtpForForgotPassword = enableOtpForForgotPassword
        });
        await _db.SaveChangesAsync();
    }
}
