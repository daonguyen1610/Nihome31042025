using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests.Auth;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private const string RegisterStartScope = "auth.register.start";
    private const string RegisterCompleteScope = "auth.register.complete";

    private readonly AppDbContext _db;
    private readonly PasswordService _passwordService;
    private readonly JwtService _jwtService;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly OtpService _otpService;
    private readonly IdempotencyService _idempotency;
    private readonly FingerprintService _fingerprint;
    private readonly IMapper _mapper;
    private readonly JwtOptions _jwtOptions;
    private readonly IAuditLogger _audit;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext db,
        PasswordService passwordService,
        JwtService jwtService,
        RefreshTokenService refreshTokenService,
        OtpService otpService,
        IdempotencyService idempotency,
        FingerprintService fingerprint,
        IMapper mapper,
        IOptions<JwtOptions> jwtOptions,
        IAuditLogger audit,
        ILogger<AuthController> logger)
    {
        _db = db;
        _passwordService = passwordService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _otpService = otpService;
        _idempotency = idempotency;
        _fingerprint = fingerprint;
        _mapper = mapper;
        _jwtOptions = jwtOptions.Value;
        _audit = audit;
        _logger = logger;
    }

    [HttpPost("register/start")]
    [Idempotency(RegisterStartScope)]
    public async Task<IActionResult> StartRegister(
        RegisterStartRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var fingerprint = _fingerprint.Compute(HttpContext);

        var phone = (request.PhoneNumber ?? string.Empty).Trim();
        var normalizedEmail = EmailUniqueness.Normalize(request.Email);

        if (await _db.Users.AsNoTracking().AnyAsync(u => u.PhoneNumber == phone, ct))
        {
            return BadRequest(new { message = "Phone number already registered." });
        }

        if (await EmailUniqueness.IsTakenAsync(_db, normalizedEmail, excludeUserId: null, ct))
        {
            return Conflict(new { message = "Email already registered." });
        }

        // Block when another in-flight OTP registration already claimed this email.
        if (!string.IsNullOrEmpty(normalizedEmail) && await _db.RegistrationOtps
            .AsNoTracking()
            .AnyAsync(o =>
                o.Email == normalizedEmail &&
                o.PhoneNumber != phone &&
                !o.IsUsed &&
                o.ExpiresAt > DateTime.UtcNow, ct))
        {
            return Conflict(new { message = "Email already registered." });
        }

        var settings = await GetSiteSettingsAsync();
        if (!settings.EnableOtpForRegistration)
        {
            var user = new ApplicationUser
            {
                PhoneNumber = phone,
                FullName = request.FullName?.Trim() ?? string.Empty,
                Email = normalizedEmail,
            };
            user.PasswordHash = _passwordService.Hash(user, request.Password);
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            var response = await BuildAuthResponseAsync(user);
            response.OtpRequired = false;
            await _idempotency.SaveAsync(
                RegisterStartScope, idempotencyKey, fingerprint, user.Id, StatusCodes.Status200OK, response, ct);
            return Ok(response);
        }

        await _otpService.GenerateOtp(phone, request.FullName?.Trim(), normalizedEmail);
        var otpPayload = new
        {
            message = "OTP code sent to email.",
            phone,
            email = normalizedEmail,
            otpRequired = true,
        };
        await _idempotency.SaveAsync(
            RegisterStartScope, idempotencyKey, fingerprint, userId: null, StatusCodes.Status200OK, otpPayload, ct);
        return Ok(otpPayload);
    }

    [HttpPost("register/verify-otp")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest request)
    {
        var otpEntry = await _otpService.VerifyOtp(request.PhoneNumber, request.OtpCode);
        if (otpEntry == null)
        {
            return BadRequest(new { message = "Invalid OTP." });
        }

        return Ok(new { message = "OTP verified." });
    }

    [HttpPost("register/complete")]
    [Idempotency(RegisterCompleteScope)]
    public async Task<IActionResult> CompleteRegister(
        RegistrationCompleteRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var fingerprint = _fingerprint.Compute(HttpContext);

        var phone = (request.PhoneNumber ?? string.Empty).Trim();

        if (await _db.Users.AsNoTracking().AnyAsync(u => u.PhoneNumber == phone, ct))
        {
            return BadRequest(new { message = "Phone number already registered." });
        }

        var settings = await GetSiteSettingsAsync();
        if (!settings.EnableOtpForRegistration)
        {
            return BadRequest(new { message = "OTP verification is disabled. Complete registration from register/start." });
        }

        var entry = await _otpService.GetLatestOtp(phone);
        if (entry == null || entry.IsUsed || entry.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new { message = "OTP session not found or expired." });
        }

        var email = EmailUniqueness.Normalize(entry.Email);
        if (string.IsNullOrEmpty(email))
        {
            return BadRequest(new { message = "Email is required to complete registration." });
        }

        if (await EmailUniqueness.IsTakenAsync(_db, email, excludeUserId: null, ct))
        {
            return Conflict(new { message = "Email already registered." });
        }

        await _otpService.MarkAsUsed(entry);

        var user = new ApplicationUser
        {
            PhoneNumber = entry.PhoneNumber,
            FullName = entry.FullName ?? string.Empty,
            Email = email,
        };
        user.PasswordHash = _passwordService.Hash(user, request.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var response = await BuildAuthResponseAsync(user);
        await _idempotency.SaveAsync(
            RegisterCompleteScope, idempotencyKey, fingerprint, user.Id, StatusCodes.Status200OK, response, ct);
        return Ok(response);
    }

    [HttpPost("register/resend-otp")]
    public async Task<IActionResult> ResendRegisterOtp(ResendOtpRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber))
        {
            return BadRequest(new { message = "Phone number already registered." });
        }

        var settings = await GetSiteSettingsAsync();
        if (!settings.EnableOtpForRegistration)
        {
            return BadRequest(new { message = "OTP verification is disabled for registration." });
        }

        string? otp;
        try
        {
            otp = await _otpService.ResendOtp(request.PhoneNumber);
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { message = "OTP request limit exceeded." });
        }

        if (otp == null)
        {
            return BadRequest(new { message = "Please wait before requesting a new OTP." });
        }

        return Ok(new { message = "OTP resent to email." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null || !_passwordService.Verify(user, request.Password))
        {
            _audit.Log(new AuditEvent
            {
                Action = "auth.login",
                ResourceType = "User",
                ResourceId = user?.Id.ToString(),
                Message = $"Failed login for {request.PhoneNumber}",
                Status = AuditStatus.Failure,
                FailureReason = "invalid_credentials",
                Metadata = new { phoneNumber = request.PhoneNumber },
            });
            return Unauthorized(new { message = "Invalid credentials." });
        }

        if (!user.IsActive)
        {
            _audit.Log(new AuditEvent
            {
                Action = "auth.login",
                ResourceType = "User",
                ResourceId = user.Id.ToString(),
                Message = $"Inactive account login attempt for {user.PhoneNumber}",
                Status = AuditStatus.Denied,
                FailureReason = "account_inactive",
            });
            return Unauthorized(new { message = "Account is inactive." });
        }

        _logger.LogInformation("Login successful for {PhoneNumber}", request.PhoneNumber);
        _audit.Log(new AuditEvent
        {
            Action = "auth.login",
            ResourceType = "User",
            ResourceId = user.Id.ToString(),
            Message = $"User {user.PhoneNumber} logged in",
            Status = AuditStatus.Success,
        });
        return Ok(await BuildAuthResponseAsync(user));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var refreshToken = await _refreshTokenService.ValidateAsync(request.RefreshToken);
        if (refreshToken == null)
        {
            return Unauthorized(new { message = "Refresh token is invalid." });
        }

        var user = await _db.Users.FindAsync(refreshToken.UserId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { message = "User not found." });
        }

        await _refreshTokenService.RevokeAsync(refreshToken);
        return Ok(await BuildAuthResponseAsync(user));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var refreshToken = await _refreshTokenService.ValidateAsync(request.RefreshToken);
        if (refreshToken != null)
        {
            await _refreshTokenService.RevokeAsync(refreshToken);
            _audit.Log(new AuditEvent
            {
                Action = "auth.logout",
                ResourceType = "User",
                ResourceId = refreshToken.UserId.ToString(),
                Message = "User logged out",
                Status = AuditStatus.Success,
            });
        }

        return Ok(new { message = "Logout successful." });
    }

    [HttpPost("forgot/start")]
    public async Task<IActionResult> ForgotPasswordStart(ForgotPasswordStartRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null)
        {
            return BadRequest(new { message = "Account not found." });
        }

        var settings = await GetSiteSettingsAsync();
        if (!settings.EnableOtpForForgotPassword)
        {
            return Ok(new
            {
                message = "You can reset your password directly.",
                phone = user.PhoneNumber,
                otpRequired = false
            });
        }

        await _otpService.GenerateOtp(user.PhoneNumber, user.FullName, user.Email);
        return Ok(new
        {
            message = "OTP code sent to email.",
            phone = user.PhoneNumber,
            email = user.Email,
            otpRequired = true
        });
    }

    [HttpPost("forgot/reset-direct")]
    public async Task<IActionResult> ForgotPasswordResetDirect(ForgotPasswordCompleteRequest request)
    {
        var settings = await GetSiteSettingsAsync();
        if (settings.EnableOtpForForgotPassword)
        {
            return BadRequest(new { message = "OTP verification is required. Please use the standard forgot password flow." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null)
        {
            return BadRequest(new { message = "Account not found." });
        }

        user.PasswordHash = _passwordService.Hash(user, request.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Password reset completed." });
    }

    [HttpPost("forgot/verify-otp")]
    public async Task<IActionResult> ForgotPasswordVerifyOtp(VerifyOtpRequest request)
    {
        var otpEntry = await _otpService.VerifyOtp(request.PhoneNumber, request.OtpCode);
        if (otpEntry == null)
        {
            return BadRequest(new { message = "Invalid OTP." });
        }

        return Ok(new { message = "OTP verified." });
    }

    [HttpPost("forgot/complete")]
    public async Task<IActionResult> ForgotPasswordComplete(ForgotPasswordCompleteRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null)
        {
            return BadRequest(new { message = "Account not found." });
        }

        var settings = await GetSiteSettingsAsync();
        if (settings.EnableOtpForForgotPassword)
        {
            var otpEntry = await _otpService.GetLatestOtp(request.PhoneNumber);
            if (otpEntry == null || otpEntry.IsUsed || otpEntry.ExpiresAt < DateTime.UtcNow)
            {
                return BadRequest(new { message = "OTP session not found or expired." });
            }

            await _otpService.MarkAsUsed(otpEntry);
        }

        user.PasswordHash = _passwordService.Hash(user, request.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Password reset completed." });
    }

    [HttpPost("forgot/resend-otp")]
    public async Task<IActionResult> ResendForgotOtp(ResendOtpRequest request)
    {
        var settings = await GetSiteSettingsAsync();
        if (!settings.EnableOtpForForgotPassword)
        {
            return BadRequest(new { message = "OTP verification is disabled for forgot password." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null)
        {
            return BadRequest(new { message = "Account not found." });
        }

        string? otp;
        try
        {
            otp = await _otpService.ResendOtp(request.PhoneNumber);
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { message = "OTP request limit exceeded." });
        }

        if (otp == null)
        {
            return BadRequest(new { message = "Please wait before requesting a new OTP." });
        }

        return Ok(new { message = "OTP resent to email." });
    }

    private async Task<SiteSettings> GetSiteSettingsAsync() =>
        await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync()
        ?? new SiteSettings();

    private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user)
    {
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = await _refreshTokenService.IssueAsync(user);
        var response = _mapper.Map<AuthResponse>(user);
        response.AccessToken = accessToken;
        response.RefreshToken = refreshToken.Token;
        response.ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);
        response.Role = user.Role.ToString();
        return response;
    }
}
