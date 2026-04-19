using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NihomeBackend.Controllers;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests.Auth;
using NihomeBackend.Services;

namespace nihomebackend.tests.Infrastructure;

public sealed class AuthTestHost : IDisposable
{
    private AuthTestHost(
        AppDbContext db,
        PasswordService passwordService,
        JwtService jwtService,
        RefreshTokenService refreshTokenService,
        OtpService otpService,
        RecordingEmailService emailService,
        ScenarioLogSink logSink,
        AuthController controller)
    {
        Db = db;
        Passwords = passwordService;
        Jwt = jwtService;
        RefreshTokens = refreshTokenService;
        Otps = otpService;
        Email = emailService;
        Logs = logSink;
        Controller = controller;
    }

    public AppDbContext Db { get; }

    public PasswordService Passwords { get; }

    public JwtService Jwt { get; }

    public RefreshTokenService RefreshTokens { get; }

    public OtpService Otps { get; }

    public RecordingEmailService Email { get; }

    internal ScenarioLogSink Logs { get; }

    public AuthController Controller { get; }

    public static AuthTestHost Create(
        bool enableOtpForRegistration = true,
        bool enableOtpForForgotPassword = true,
        [System.Runtime.CompilerServices.CallerMemberName] string scenario = "")
    {
        var logSink = new ScenarioLogSink(scenario);
        logSink.Step(
            $"Creating auth test host with registrationOtp={enableOtpForRegistration}, forgotOtp={enableOtpForForgotPassword}");

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(dbOptions);
        DbSeeder.Seed(db);

        var settings = db.SiteSettings.Single();
        settings.EnableOtpForRegistration = enableOtpForRegistration;
        settings.EnableOtpForForgotPassword = enableOtpForForgotPassword;
        db.SaveChanges();

        var passwordService = new PasswordService();
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "Tests",
            Audience = "Tests",
            ActiveKeyId = "test",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
            Keys = new Dictionary<string, string>
            {
                ["test"] = "12345678901234567890123456789012"
            }
        });

        var jwtService = new JwtService(jwtOptions, new ScenarioLogger<JwtService>(logSink));
        var refreshTokenService = new RefreshTokenService(db, jwtOptions);
        var emailService = new RecordingEmailService();
        var otpService = new OtpService(db, new ScenarioLogger<OtpService>(logSink), emailService);
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile(new NihomeBackend.Mappings.AutoMapperProfile()))
            .CreateMapper();
        var controller = new AuthController(
            db,
            passwordService,
            jwtService,
            refreshTokenService,
            otpService,
            mapper,
            jwtOptions,
            new ScenarioLogger<AuthController>(logSink));

        return new AuthTestHost(
            db,
            passwordService,
            jwtService,
            refreshTokenService,
            otpService,
            emailService,
            logSink,
            controller);
    }

    public ApplicationUser CreateUser(
        string phoneNumber,
        string password,
        string fullName = "Existing User",
        string email = "existing@example.com",
        UserRole role = UserRole.USER,
        bool isActive = true)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phoneNumber,
            FullName = fullName,
            Email = email,
            Role = role,
            IsActive = isActive
        };
        user.PasswordHash = Passwords.Hash(user, password);

        Logs.Step($"Seeding user {phoneNumber} with role {role} and active={isActive}");
        Db.Users.Add(user);
        Db.SaveChanges();
        return user;
    }

    public ApplicationUser? FindUser(string phoneNumber) =>
        Db.Users.SingleOrDefault(user => user.PhoneNumber == phoneNumber);

    public RegistrationOtp AddOtp(
        string phoneNumber,
        string otpCode = "123456",
        bool isUsed = false,
        DateTime? createdAt = null,
        DateTime? expiresAt = null,
        string? fullName = "OTP User",
        string? email = "otp@example.com")
    {
        var otp = new RegistrationOtp
        {
            PhoneNumber = phoneNumber,
            OtpCode = otpCode,
            IsUsed = isUsed,
            FullName = fullName,
            Email = email,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(5)
        };

        Logs.Step($"Seeding OTP for {phoneNumber} used={isUsed} expiresAt={otp.ExpiresAt:O}");
        Db.RegistrationOtps.Add(otp);
        Db.SaveChanges();
        return otp;
    }

    public Task<IActionResult> StartRegister(
        string phoneNumber,
        string fullName = "Test User",
        string email = "test@example.com",
        string password = "secret123")
    {
        Logs.Step($"Calling register/start for {phoneNumber}");
        return Controller.StartRegister(new RegisterStartRequest
        {
            PhoneNumber = phoneNumber,
            FullName = fullName,
            Email = email,
            Password = password
        });
    }

    public Task<IActionResult> VerifyRegisterOtp(string phoneNumber, string otpCode)
    {
        Logs.Step($"Calling register/verify-otp for {phoneNumber}");
        return Controller.VerifyOtp(new VerifyOtpRequest
        {
            PhoneNumber = phoneNumber,
            OtpCode = otpCode
        });
    }

    public Task<IActionResult> CompleteRegister(string phoneNumber, string password = "secret123")
    {
        Logs.Step($"Calling register/complete for {phoneNumber}");
        return Controller.CompleteRegister(new RegistrationCompleteRequest
        {
            PhoneNumber = phoneNumber,
            Password = password
        });
    }

    public Task<IActionResult> ResendRegisterOtp(string phoneNumber)
    {
        Logs.Step($"Calling register/resend-otp for {phoneNumber}");
        return Controller.ResendRegisterOtp(new ResendOtpRequest
        {
            PhoneNumber = phoneNumber
        });
    }

    public Task<IActionResult> StartForgotPassword(string phoneNumber)
    {
        Logs.Step($"Calling forgot/start for {phoneNumber}");
        return Controller.ForgotPasswordStart(new ForgotPasswordStartRequest
        {
            PhoneNumber = phoneNumber
        });
    }

    public Task<IActionResult> VerifyForgotPasswordOtp(string phoneNumber, string otpCode)
    {
        Logs.Step($"Calling forgot/verify-otp for {phoneNumber}");
        return Controller.ForgotPasswordVerifyOtp(new VerifyOtpRequest
        {
            PhoneNumber = phoneNumber,
            OtpCode = otpCode
        });
    }

    public Task<IActionResult> CompleteForgotPassword(string phoneNumber, string newPassword)
    {
        Logs.Step($"Calling forgot/complete for {phoneNumber}");
        return Controller.ForgotPasswordComplete(new ForgotPasswordCompleteRequest
        {
            PhoneNumber = phoneNumber,
            NewPassword = newPassword
        });
    }

    public Task<IActionResult> ResetForgotPasswordDirect(string phoneNumber, string newPassword)
    {
        Logs.Step($"Calling forgot/reset-direct for {phoneNumber}");
        return Controller.ForgotPasswordResetDirect(new ForgotPasswordCompleteRequest
        {
            PhoneNumber = phoneNumber,
            NewPassword = newPassword
        });
    }

    public Task<IActionResult> ResendForgotOtp(string phoneNumber)
    {
        Logs.Step($"Calling forgot/resend-otp for {phoneNumber}");
        return Controller.ResendForgotOtp(new ResendOtpRequest
        {
            PhoneNumber = phoneNumber
        });
    }

    public Task<IActionResult> Login(string phoneNumber, string password)
    {
        Logs.Step($"Calling login for {phoneNumber}");
        return Controller.Login(new LoginRequest
        {
            PhoneNumber = phoneNumber,
            Password = password
        });
    }

    public Task<IActionResult> Refresh(string refreshToken)
    {
        Logs.Step("Calling refresh");
        return Controller.Refresh(new RefreshRequest
        {
            RefreshToken = refreshToken
        });
    }

    public Task<IActionResult> Logout(string refreshToken)
    {
        Logs.Step("Calling logout");
        return Controller.Logout(new RefreshRequest
        {
            RefreshToken = refreshToken
        });
    }

    public void SeedOtpRateLimit(
        string phoneNumber,
        int count = 5,
        string? fullName = "OTP User",
        string? email = "otp@example.com")
    {
        for (var index = 0; index < count; index++)
        {
            AddOtp(
                phoneNumber,
                otpCode: $"{123450 + index}",
                isUsed: true,
                createdAt: DateTime.UtcNow.AddMinutes(-10).AddSeconds(-index),
                expiresAt: DateTime.UtcNow.AddMinutes(-5),
                fullName: fullName,
                email: email);
        }
    }

    public void Dispose()
    {
        Logs.Step("Disposing auth test host");
        Db.Dispose();
    }
}
