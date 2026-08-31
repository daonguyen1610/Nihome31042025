using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services.GoogleDrive;

public sealed record GoogleDriveCredentialMetadata(
    bool HasDatabaseCredential,
    bool HasConfiguredFallback,
    string? AccountEmail,
    DateTime? ConnectedAt);

public interface IGoogleDriveCredentialStore
{
    Task<string?> GetRefreshTokenAsync(CancellationToken ct = default);
    Task<GoogleDriveCredentialMetadata> GetMetadataAsync(CancellationToken ct = default);
    Task SaveAsync(string refreshToken, string? accountEmail, int connectedByUserId, CancellationToken ct = default);
}

public sealed class GoogleDriveCredentialStore(
    AppDbContext db,
    IDataProtectionProvider dataProtectionProvider,
    GoogleDriveOptions options) : IGoogleDriveCredentialStore
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector(
        "Nicon.GoogleDrive.RefreshToken.v1");

    public async Task<string?> GetRefreshTokenAsync(CancellationToken ct = default)
    {
        var protectedToken = await db.GoogleDriveCredentials.AsNoTracking()
            .Where(credential => credential.Id == 1)
            .Select(credential => credential.ProtectedRefreshToken)
            .SingleOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(protectedToken))
        {
            try
            {
                return protector.Unprotect(protectedToken);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                throw new GoogleDriveReconnectRequiredException(
                    "Không thể giải mã thông tin kết nối Google Drive. Hãy kết nối lại trong Cài đặt.",
                    exception);
            }
        }

        return string.IsNullOrWhiteSpace(options.RefreshToken) ? null : options.RefreshToken;
    }

    public async Task<GoogleDriveCredentialMetadata> GetMetadataAsync(CancellationToken ct = default)
    {
        var credential = await db.GoogleDriveCredentials.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == 1, ct);
        return new GoogleDriveCredentialMetadata(
            credential is not null,
            !string.IsNullOrWhiteSpace(options.RefreshToken),
            credential?.AccountEmail,
            credential?.ConnectedAt);
    }

    public async Task SaveAsync(
        string refreshToken,
        string? accountEmail,
        int connectedByUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Refresh token không được để trống.", nameof(refreshToken));

        var now = DateTime.UtcNow;
        var credential = await db.GoogleDriveCredentials.SingleOrDefaultAsync(item => item.Id == 1, ct);
        if (credential is null)
        {
            credential = new GoogleDriveCredential { Id = 1 };
            db.GoogleDriveCredentials.Add(credential);
        }

        credential.ProtectedRefreshToken = protector.Protect(refreshToken);
        credential.AccountEmail = string.IsNullOrWhiteSpace(accountEmail) ? null : accountEmail.Trim();
        credential.ConnectedByUserId = connectedByUserId;
        credential.ConnectedAt = now;
        credential.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
    }
}