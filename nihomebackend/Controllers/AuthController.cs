using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests.Auth;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthStore _authStore;
    private readonly PasswordService _passwordService;
    private readonly JwtService _jwtService;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly OtpService _otpService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AuthStore authStore,
        PasswordService passwordService,
        JwtService jwtService,
        RefreshTokenService refreshTokenService,
        OtpService otpService,
        ILogger<AuthController> logger)
    {
        _authStore = authStore;
        _passwordService = passwordService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _otpService = otpService;
        _logger = logger;
    }

    [HttpPost("register/start")]
    public async Task<IActionResult> StartRegister(RegisterStartRequest request)
    {
        if (_authStore.FindUserByPhone(request.PhoneNumber) != null)
        {
            return BadRequest(new { message = "Phone number already registered." });
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
        if (_authStore.FindUserByPhone(request.PhoneNumber) != null)
        {
            return BadRequest(new { message = "Phone number already registered." });
        }

        var entry = await _otpService.GetLatestOtp(request.PhoneNumber);
        if (entry == null || entry.ExpiresAt < DateTime.UtcNow)
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
        _authStore.AddUser(user);

        return Ok(await BuildAuthResponseAsync(user));
    }

    [HttpPost("register/resend-otp")]
    public async Task<IActionResult> ResendRegisterOtp(ResendOtpRequest request)
    {
        if (_authStore.FindUserByPhone(request.PhoneNumber) != null)
        {
            return BadRequest(new { message = "Phone number already registered." });
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
        var user = _authStore.FindUserByPhone(request.PhoneNumber);
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

        var user = _authStore.FindUserById(refreshToken.UserId);
        if (user == null)
        {
            return Unauthorized(new { message = "User not found." });
        }

        await _refreshTokenService.RevokeAsync(refreshToken);
        return Ok(await BuildAuthResponseAsync(user));
    }

    [Authorize]
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
        var user = _authStore.FindUserByPhone(request.PhoneNumber);
        if (user == null)
        {
            return BadRequest(new { message = "Account not found." });
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

    [HttpPost("forgot/complete")]
    public IActionResult ForgotPasswordComplete(ForgotPasswordCompleteRequest request)
    {
        var user = _authStore.FindUserByPhone(request.PhoneNumber);
        if (user == null)
        {
            return BadRequest(new { message = "Account not found." });
        }

        user.PasswordHash = _passwordService.Hash(user, request.NewPassword);
        _authStore.UpdateUser(user);
        return Ok(new { message = "Password reset completed." });
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user)
    {
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = await _refreshTokenService.IssueAsync(user);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            UserId = user.Id,
            PhoneNumber = user.PhoneNumber,
            FullName = user.FullName,
            Role = user.Role.ToString().ToUpperInvariant(),
            IsActive = user.IsActive,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl
        };
    }
}
