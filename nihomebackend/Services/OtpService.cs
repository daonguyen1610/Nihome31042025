using System.Security.Cryptography;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class OtpService
{
    private readonly AuthStore _authStore;
    private readonly ILogger<OtpService> _logger;
    private readonly IEmailService _emailService;

    public OtpService(AuthStore authStore, ILogger<OtpService> logger, IEmailService emailService)
    {
        _authStore = authStore;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<string> GenerateOtp(string phoneNumber, string fullName, string? email)
    {
        var otp = GenerateSecureOtp();
        var entry = new RegistrationOtp
        {
            PhoneNumber = phoneNumber,
            FullName = fullName,
            Email = email,
            OtpCode = otp,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _authStore.SaveOtp(entry);

        if (!string.IsNullOrWhiteSpace(email))
        {
            try
            {
                await _emailService.SendEmailAsync(
                    email,
                    "[Nihome] Your verification code",
                    $"<p>Your OTP code is <strong>{otp}</strong>. It expires in 5 minutes.</p>");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send OTP email to {Email}", email);
            }
        }

        _logger.LogInformation("Generated OTP for phone number {PhoneNumber}", phoneNumber);
        return otp;
    }

    public Task<RegistrationOtp?> VerifyOtp(string phoneNumber, string otp)
    {
        var entry = _authStore.GetLatestOtp(phoneNumber);
        if (entry == null || entry.IsUsed || entry.ExpiresAt < DateTime.UtcNow || entry.OtpCode != otp)
        {
            return Task.FromResult<RegistrationOtp?>(null);
        }

        return Task.FromResult<RegistrationOtp?>(entry);
    }

    public Task MarkAsUsed(RegistrationOtp entry)
    {
        entry.IsUsed = true;
        return Task.CompletedTask;
    }

    public Task<RegistrationOtp?> GetLatestOtp(string phoneNumber) =>
        Task.FromResult(_authStore.GetLatestOtp(phoneNumber));

    public async Task<string?> ResendOtp(string phoneNumber)
    {
        if (!await CanRequestOtp(phoneNumber))
        {
            return null;
        }

        var latest = await GetLatestOtp(phoneNumber);
        return await GenerateOtp(phoneNumber, latest?.FullName ?? string.Empty, latest?.Email);
    }

    public Task<bool> CanRequestOtp(string phoneNumber)
    {
        var lastOtp = _authStore.GetLatestOtp(phoneNumber);
        if (lastOtp == null)
        {
            return Task.FromResult(true);
        }

        var secondsSinceLastOtp = (DateTime.UtcNow - lastOtp.CreatedAt).TotalSeconds;
        if (secondsSinceLastOtp < 60)
        {
            return Task.FromResult(false);
        }

        if (!lastOtp.IsUsed && lastOtp.ExpiresAt > DateTime.UtcNow)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    private static string GenerateSecureOtp()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var value = BitConverter.ToUInt32(bytes, 0) % 900000 + 100000;
        return value.ToString("D6");
    }
}
