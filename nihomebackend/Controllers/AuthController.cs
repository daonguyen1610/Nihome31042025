using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests.Auth;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordService _passwordService;
    private readonly JwtService _jwtService;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly OtpService _otpService;
    private readonly IMapper _mapper;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext db,
        PasswordService passwordService,
        JwtService jwtService,
        RefreshTokenService refreshTokenService,
        OtpService otpService,
        IMapper mapper,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthController> logger)
    {
        _db = db;
        _passwordService = passwordService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _otpService = otpService;
        _mapper = mapper;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    [HttpPost("register/start")]
    public async Task<IActionResult> StartRegister(RegisterStartRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber))
        {
            return BadRequest(new { message = "Phone number already registered." });
        }

        var settings = await GetSiteSettingsAsync();
        if (!settings.EnableOtpForRegistration)
        {
            var user = new ApplicationUser
            {
                PhoneNumber = request.PhoneNumber,
                FullName = request.FullName,
                Email = request.Email
            };
            user.PasswordHash = _passwordService.Hash(user, request.Password);
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var response = await BuildAuthResponseAsync(user);
            response.OtpRequired = false;
            return Ok(response);
        }

        await _otpService.GenerateOtp(request.PhoneNumber, request.FullName, request.Email);
        return Ok(new
        {
            message = "OTP code sent to email.",
            phone = request.PhoneNumber,
            email = request.Email,
            otpRequired = true
        });
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
    public async Task<IActionResult> CompleteRegister(RegistrationCompleteRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber))
        {
            return BadRequest(new { message = "Phone number already registered." });
        }

        var settings = await GetSiteSettingsAsync();
        if (!settings.EnableOtpForRegistration)
        {
            return BadRequest(new { message = "OTP verification is disabled. Complete registration from register/start." });
        }

        var entry = await _otpService.GetLatestOtp(request.PhoneNumber);
        if (entry == null || entry.IsUsed || entry.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new { message = "OTP session not found or expired." });
        }

        await _otpService.MarkAsUsed(entry);

        var user = new ApplicationUser
        {
            PhoneNumber = entry.PhoneNumber,
            FullName = entry.FullName ?? string.Empty,
            Email = entry.Email
        };
        user.PasswordHash = _passwordService.Hash(user, request.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(await BuildAuthResponseAsync(user));
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

        var otp = await _otpService.ResendOtp(request.PhoneNumber);
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
            return Unauthorized(new { message = "Invalid credentials." });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Account is inactive." });
        }

        _logger.LogInformation("Login successful for {PhoneNumber}", request.PhoneNumber);
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

        var otp = await _otpService.ResendOtp(request.PhoneNumber);
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
