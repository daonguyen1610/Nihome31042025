using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class OtpService
{
    private readonly AppDbContext _db;
    private readonly ILogger<OtpService> _logger;
    private readonly IEmailService _emailService;

    public OtpService(AppDbContext db, ILogger<OtpService> logger, IEmailService emailService)
    {
        _db = db;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<string> GenerateOtp(string phoneNumber, string? fullName, string? email)
    {
        var otp = GenerateSecureOtp();
        var entry = new RegistrationOtp
        {
            PhoneNumber = phoneNumber,
            FullName = fullName,
            Email = email,
            OtpCode = otp,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };

        _db.RegistrationOtps.Add(entry);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(email))
        {
            try
            {
                var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
                var siteName = settings?.SiteName ?? "Nihome";

                var (subject, body) = EmailTemplateFormatter.BuildOtpEmail(
                    settings?.OtpEmailSubjectTemplate,
                    settings?.OtpEmailBodyTemplate,
                    siteName,
                    otp);

                await _emailService.SendEmailAsync(
                    email,
                    subject,
                    body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send OTP email to {Email}", email);
            }
        }

        _logger.LogInformation("Generated OTP for phone number {PhoneNumber}", phoneNumber);
        return otp;
    }

    public async Task<RegistrationOtp?> VerifyOtp(string phoneNumber, string otp)
    {
        return await _db.RegistrationOtps
            .Where(x => x.PhoneNumber == phoneNumber &&
                x.OtpCode == otp &&
                !x.IsUsed &&
                x.ExpiresAt >= DateTime.UtcNow)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
    }

    public async Task MarkAsUsed(RegistrationOtp entry)
    {
        entry.IsUsed = true;
        await _db.SaveChangesAsync();
    }

    public async Task<RegistrationOtp?> GetLatestOtp(string phoneNumber) =>
        await _db.RegistrationOtps
            .Where(o => o.PhoneNumber == phoneNumber)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync();

    public async Task<string?> ResendOtp(string phoneNumber)
    {
        if (!await CanRequestOtp(phoneNumber))
        {
            return null;
        }

        var requestCount = await CountOtpLastHour(phoneNumber);
        if (requestCount >= 5)
        {
            throw new InvalidOperationException("OTP request limit exceeded.");
        }

        var latest = await GetLatestOtp(phoneNumber);
        return await GenerateOtp(phoneNumber, latest?.FullName ?? string.Empty, latest?.Email);
    }

    public async Task<bool> CanRequestOtp(string phoneNumber)
    {
        var lastOtp = await GetLatestOtp(phoneNumber);
        if (lastOtp == null)
        {
            return true;
        }

        var secondsSinceLastOtp = (DateTime.UtcNow - lastOtp.CreatedAt).TotalSeconds;
        if (secondsSinceLastOtp < 60)
        {
            return false;
        }

        if (!lastOtp.IsUsed && lastOtp.ExpiresAt > DateTime.UtcNow)
        {
            return false;
        }

        return true;
    }

    public async Task<int> CountOtpLastHour(string phoneNumber)
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        return await _db.RegistrationOtps
            .Where(o => o.PhoneNumber == phoneNumber && o.CreatedAt >= oneHourAgo)
            .CountAsync();
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
