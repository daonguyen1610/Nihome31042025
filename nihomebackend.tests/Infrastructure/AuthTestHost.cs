using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
        AuthController controller)
    {
        Db = db;
        Passwords = passwordService;
        Jwt = jwtService;
        RefreshTokens = refreshTokenService;
        Otps = otpService;
        Email = emailService;
        Controller = controller;
    }

    public AppDbContext Db { get; }

    public PasswordService Passwords { get; }

    public JwtService Jwt { get; }

    public RefreshTokenService RefreshTokens { get; }

    public OtpService Otps { get; }

    public RecordingEmailService Email { get; }

    public AuthController Controller { get; }

    public static AuthTestHost Create(
        bool enableOtpForRegistration = true,
        bool enableOtpForForgotPassword = true)
    {
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

        var jwtService = new JwtService(jwtOptions, NullLogger<JwtService>.Instance);
        var refreshTokenService = new RefreshTokenService(db, jwtOptions);
        var emailService = new RecordingEmailService();
        var otpService = new OtpService(db, NullLogger<OtpService>.Instance, emailService);
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
            NullLogger<AuthController>.Instance);

        return new AuthTestHost(
            db,
            passwordService,
            jwtService,
            refreshTokenService,
            otpService,
            emailService,
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

        Db.RegistrationOtps.Add(otp);
        Db.SaveChanges();
        return otp;
    }

    public Task<IActionResult> StartRegister(
        string phoneNumber,
        string fullName = "Test User",
        string email = "test@example.com",
        string password = "secret123") =>
        Controller.StartRegister(new RegisterStartRequest
        {
            PhoneNumber = phoneNumber,
            FullName = fullName,
            Email = email,
            Password = password
        });

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
        Db.Dispose();
    }
}
