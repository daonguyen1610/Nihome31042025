using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services.GoogleDrive;

public sealed record GoogleDriveAdminConfigurationResponse(
    bool Enabled,
    string ClientId,
    bool HasClientSecret,
    bool HasRefreshToken,
    string OAuthRedirectUri,
    string FrontendReturnUrl,
    string RootFolderId,
    string InstanceId,
    string ApplicationName,
    GoogleDriveFolderOptions Folders,
    bool SupportsAllDrives,
    int PollIntervalSeconds,
    string? AccountEmail,
    DateTime? ConnectedAt,
    string RowVersion);

public sealed class UpdateGoogleDriveConfigurationRequest
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string OAuthRedirectUri { get; set; } = string.Empty;
    public string FrontendReturnUrl { get; set; } = string.Empty;
    public string RootFolderId { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public GoogleDriveFolderOptions Folders { get; set; } = new();
    public bool SupportsAllDrives { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
    public string? RowVersion { get; set; }
}

public interface IGoogleDriveSettingsStore
{
    Task<GoogleDriveOptions> GetRuntimeAsync(CancellationToken ct = default);
    Task<GoogleDriveAdminConfigurationResponse> GetAdminAsync(CancellationToken ct = default);
    Task<GoogleDriveAdminConfigurationResponse> UpdateAsync(
        UpdateGoogleDriveConfigurationRequest request,
        int updatedByUserId,
        CancellationToken ct = default);
    Task SaveRefreshTokenAsync(
        string refreshToken,
        string? accountEmail,
        int connectedByUserId,
        string expectedConfigurationVersion,
        CancellationToken ct = default);
    Task ClearRefreshTokenAsync(
        int updatedByUserId,
        string expectedConfigurationVersion,
        CancellationToken ct = default);
}

public sealed class GoogleDriveSettingsStore(
    AppDbContext db,
    IDataProtectionProvider dataProtectionProvider) : IGoogleDriveSettingsStore
{
    private static readonly Regex ClientIdPattern = new(
        "^[A-Za-z0-9._-]+\\.apps\\.googleusercontent\\.com$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DriveIdPattern = new(
        "^[A-Za-z0-9_-]{10,200}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex InstanceIdPattern = new(
        "^[A-Za-z0-9._-]{3,100}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly IDataProtector clientSecretProtector = dataProtectionProvider.CreateProtector(
        "Nicon.GoogleDrive.ClientSecret.v1");
    private readonly IDataProtector refreshTokenProtector = dataProtectionProvider.CreateProtector(
        "Nicon.GoogleDrive.RefreshToken.v1");

    public async Task<GoogleDriveOptions> GetRuntimeAsync(CancellationToken ct = default)
    {
        var settings = await db.GoogleDriveCredentials.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == 1, ct);
        if (settings is null) return new GoogleDriveOptions();

        return new GoogleDriveOptions
        {
            Enabled = settings.Enabled,
            ClientId = settings.ClientId,
            ClientSecret = Unprotect(settings.ProtectedClientSecret, clientSecretProtector),
            RefreshToken = Unprotect(settings.ProtectedRefreshToken, refreshTokenProtector),
            OAuthRedirectUri = settings.OAuthRedirectUri,
            FrontendReturnUrl = settings.FrontendReturnUrl,
            RootFolderId = settings.RootFolderId,
            InstanceId = settings.InstanceId,
            ApplicationName = settings.ApplicationName,
            Folders = ToFolders(settings),
            SupportsAllDrives = settings.SupportsAllDrives,
            PollIntervalSeconds = settings.PollIntervalSeconds,
            ConfigurationVersion = ToVersion(settings.RowVersion),
        };
    }

    public async Task<GoogleDriveAdminConfigurationResponse> GetAdminAsync(CancellationToken ct = default)
    {
        var settings = await db.GoogleDriveCredentials.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == 1, ct);
        return settings is null ? EmptyAdminResponse() : ToAdminResponse(settings);
    }

    public async Task<GoogleDriveAdminConfigurationResponse> UpdateAsync(
        UpdateGoogleDriveConfigurationRequest request,
        int updatedByUserId,
        CancellationToken ct = default)
    {
        var normalized = NormalizeAndValidate(request);
        var settings = await db.GoogleDriveCredentials.SingleOrDefaultAsync(item => item.Id == 1, ct);
        if (settings is null)
        {
            if (!string.IsNullOrWhiteSpace(request.RowVersion))
                throw new GoogleDriveSettingsConcurrencyException();
            settings = new GoogleDriveCredential();
            db.GoogleDriveCredentials.Add(settings);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RowVersion))
            {
                if (settings.RowVersion.Length > 0)
                    throw new GoogleDriveSettingsValidationException(
                        "RowVersion là bắt buộc. Ví dụ: tải lại trang rồi lưu lại cấu hình.");
            }
            else
            {
                byte[] suppliedRowVersion;
                try
                {
                    suppliedRowVersion = Convert.FromBase64String(request.RowVersion);
                }
                catch (FormatException)
                {
                    throw new GoogleDriveSettingsValidationException(
                        "RowVersion không hợp lệ. Ví dụ: tải lại trang rồi lưu lại cấu hình.");
                }
                if (!suppliedRowVersion.SequenceEqual(settings.RowVersion))
                    throw new GoogleDriveSettingsConcurrencyException();
                db.Entry(settings).Property(item => item.RowVersion).OriginalValue = suppliedRowVersion;
            }
        }

        var newSecret = request.ClientSecret?.Trim();
        if (normalized.Enabled && string.IsNullOrWhiteSpace(newSecret) &&
            string.IsNullOrWhiteSpace(settings.ProtectedClientSecret))
            throw new GoogleDriveSettingsValidationException(
                "ClientSecret là bắt buộc khi bật Google Drive. Ví dụ: GOCSPX-... từ Google Cloud Console.");

        var oauthIdentityChanged = !string.Equals(settings.ClientId, normalized.ClientId, StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(newSecret);
        var folderTopologyChanged =
            !string.Equals(settings.RootFolderId, normalized.RootFolderId, StringComparison.Ordinal) ||
            !string.Equals(settings.InstanceId, normalized.InstanceId, StringComparison.Ordinal) ||
            !FolderPathsEqual(settings, normalized.Folders);

        if (folderTopologyChanged)
            db.ProjectDriveFolders.RemoveRange(await db.ProjectDriveFolders.ToListAsync(ct));

        settings.Enabled = normalized.Enabled;
        settings.ClientId = normalized.ClientId;
        if (!string.IsNullOrWhiteSpace(newSecret))
            settings.ProtectedClientSecret = clientSecretProtector.Protect(newSecret);
        settings.OAuthRedirectUri = normalized.OAuthRedirectUri;
        settings.FrontendReturnUrl = normalized.FrontendReturnUrl;
        settings.RootFolderId = normalized.RootFolderId;
        settings.InstanceId = normalized.InstanceId;
        settings.ApplicationName = normalized.ApplicationName;
        ApplyFolders(settings, normalized.Folders);
        settings.SupportsAllDrives = normalized.SupportsAllDrives;
        settings.PollIntervalSeconds = normalized.PollIntervalSeconds;
        settings.UpdatedByUserId = updatedByUserId;
        settings.UpdatedAt = DateTime.UtcNow;

        if (oauthIdentityChanged)
        {
            settings.ProtectedRefreshToken = null;
            settings.AccountEmail = null;
            settings.ConnectedByUserId = null;
            settings.ConnectedAt = null;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GoogleDriveSettingsConcurrencyException();
        }
        return ToAdminResponse(settings);
    }

    public async Task SaveRefreshTokenAsync(
        string refreshToken,
        string? accountEmail,
        int connectedByUserId,
        string expectedConfigurationVersion,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("Refresh token không được để trống.", nameof(refreshToken));
        var settings = await db.GoogleDriveCredentials.SingleOrDefaultAsync(item => item.Id == 1, ct)
            ?? throw new GoogleDriveSettingsValidationException(
                "Cấu hình Google Drive chưa được lưu. Hãy lưu cấu hình trong trang Admin trước khi kết nối.");
        byte[] expectedVersion;
        try
        {
            expectedVersion = Convert.FromBase64String(expectedConfigurationVersion);
        }
        catch (FormatException)
        {
            throw new GoogleDriveSettingsConcurrencyException();
        }
        if (!expectedVersion.SequenceEqual(settings.RowVersion))
            throw new GoogleDriveSettingsConcurrencyException();
        db.Entry(settings).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        settings.ProtectedRefreshToken = refreshTokenProtector.Protect(refreshToken);
        settings.AccountEmail = string.IsNullOrWhiteSpace(accountEmail) ? null : accountEmail.Trim();
        settings.ConnectedByUserId = connectedByUserId;
        settings.ConnectedAt = DateTime.UtcNow;
        settings.UpdatedByUserId = connectedByUserId;
        settings.UpdatedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GoogleDriveSettingsConcurrencyException();
        }
    }

    public async Task ClearRefreshTokenAsync(
        int updatedByUserId,
        string expectedConfigurationVersion,
        CancellationToken ct = default)
    {
        var settings = await db.GoogleDriveCredentials.SingleOrDefaultAsync(item => item.Id == 1, ct)
            ?? throw new GoogleDriveSettingsValidationException(
                "Cấu hình Google Drive chưa được lưu. Hãy lưu cấu hình trong trang Admin trước khi ngắt kết nối.");
        byte[] expectedVersion;
        try
        {
            expectedVersion = Convert.FromBase64String(expectedConfigurationVersion);
        }
        catch (FormatException)
        {
            throw new GoogleDriveSettingsConcurrencyException();
        }
        if (!expectedVersion.SequenceEqual(settings.RowVersion))
            throw new GoogleDriveSettingsConcurrencyException();
        db.Entry(settings).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        settings.ProtectedRefreshToken = null;
        settings.AccountEmail = null;
        settings.ConnectedByUserId = null;
        settings.ConnectedAt = null;
        settings.UpdatedByUserId = updatedByUserId;
        settings.UpdatedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GoogleDriveSettingsConcurrencyException();
        }
    }

    private static UpdateGoogleDriveConfigurationRequest NormalizeAndValidate(
        UpdateGoogleDriveConfigurationRequest request)
    {
        if (request.Folders is null)
            throw new GoogleDriveSettingsValidationException(
                "Folders là bắt buộc. Ví dụ: cung cấp đủ chín đường dẫn thư mục nghiệp vụ.");
        var normalized = new UpdateGoogleDriveConfigurationRequest
        {
            Enabled = request.Enabled,
            ClientId = Trim(request.ClientId),
            OAuthRedirectUri = Trim(request.OAuthRedirectUri),
            FrontendReturnUrl = Trim(request.FrontendReturnUrl),
            RootFolderId = Trim(request.RootFolderId),
            InstanceId = Trim(request.InstanceId),
            ApplicationName = Trim(request.ApplicationName),
            Folders = new GoogleDriveFolderOptions
            {
                SurveyMedia = Trim(request.Folders.SurveyMedia),
                CrmPreDesign = Trim(request.Folders.CrmPreDesign),
                DesignConcept = Trim(request.Folders.DesignConcept),
                DesignBasic = Trim(request.Folders.DesignBasic),
                DesignShopDrawing = Trim(request.Folders.DesignShopDrawing),
                LegalPermits = Trim(request.Folders.LegalPermits),
                ConstructionAcceptance = Trim(request.Folders.ConstructionAcceptance),
                Procurement = Trim(request.Folders.Procurement),
                FinanceContracts = Trim(request.Folders.FinanceContracts),
            },
            SupportsAllDrives = request.SupportsAllDrives,
            PollIntervalSeconds = request.PollIntervalSeconds,
            RowVersion = request.RowVersion,
        };

        var clientSecret = request.ClientSecret?.Trim();
        if (!string.IsNullOrWhiteSpace(clientSecret) &&
            (clientSecret.Length is < 8 or > 512 || HasControlCharacters(clientSecret)))
            throw new GoogleDriveSettingsValidationException(
                "ClientSecret phải dài 8-512 ký tự và không chứa ký tự điều khiển. Ví dụ: secret từ Google Cloud Console.");
        if (normalized.ClientId.Length > 255)
            throw new GoogleDriveSettingsValidationException(
                "ClientId không được vượt quá 255 ký tự. Ví dụ: 123.apps.googleusercontent.com.");
        if (normalized.OAuthRedirectUri.Length > 2048)
            throw new GoogleDriveSettingsValidationException(
                "OAuthRedirectUri không được vượt quá 2048 ký tự. Ví dụ: https://nicon.vn/api/site-settings/google-drive/oauth/callback.");
        if (normalized.FrontendReturnUrl.Length > 2048)
            throw new GoogleDriveSettingsValidationException(
                "FrontendReturnUrl không được vượt quá 2048 ký tự. Ví dụ: /admin/settings?tab=drive.");
        if (normalized.RootFolderId.Length > 200)
            throw new GoogleDriveSettingsValidationException(
                "RootFolderId không được vượt quá 200 ký tự. Ví dụ: 1AbCdEfGhIjKlMnOpQrStUv.");
        if (normalized.PollIntervalSeconds is < 5 or > 300)
            throw new GoogleDriveSettingsValidationException(
                "PollIntervalSeconds phải từ 5 đến 300 giây. Ví dụ: 15.");
        ValidateOptionalDraft(normalized);
        if (!normalized.Enabled) return normalized;
        if (!ClientIdPattern.IsMatch(normalized.ClientId))
            throw new GoogleDriveSettingsValidationException(
                "ClientId không đúng định dạng Google OAuth. Ví dụ: 123.apps.googleusercontent.com.");
        ValidateRedirectUri(normalized.OAuthRedirectUri);
        ValidateFrontendReturnUrl(normalized.FrontendReturnUrl);
        if (!DriveIdPattern.IsMatch(normalized.RootFolderId))
            throw new GoogleDriveSettingsValidationException(
                "RootFolderId không hợp lệ. Ví dụ: 1AbCdEfGhIjKlMnOpQrStUv.");
        if (!InstanceIdPattern.IsMatch(normalized.InstanceId))
            throw new GoogleDriveSettingsValidationException(
                "InstanceId phải dài 3-100 ký tự và chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang. Ví dụ: nicon-production.");
        if (normalized.ApplicationName.Length is < 1 or > 100 || HasControlCharacters(normalized.ApplicationName))
            throw new GoogleDriveSettingsValidationException(
                "ApplicationName phải dài 1-100 ký tự. Ví dụ: Nicon Google Drive Integration.");
        ValidateFolderPaths(normalized.Folders);
        return normalized;
    }

    private static void ValidateRedirectUri(string value)
    {
        if (value.Length > 2048 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            !(uri.Scheme == Uri.UriSchemeHttps ||
              (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
            !uri.AbsolutePath.EndsWith("/api/site-settings/google-drive/oauth/callback", StringComparison.Ordinal))
            throw new GoogleDriveSettingsValidationException(
                "OAuthRedirectUri phải là URL HTTPS callback hợp lệ; localhost được phép dùng HTTP. Ví dụ: https://nicon.vn/api/site-settings/google-drive/oauth/callback.");
    }

    private static void ValidateFrontendReturnUrl(string value)
    {
        if (value.Length > 2048 || !value.StartsWith("/admin/", StringComparison.Ordinal) ||
            value.StartsWith("//", StringComparison.Ordinal) || value.Contains('\\') || value.Contains('#'))
            throw new GoogleDriveSettingsValidationException(
                "FrontendReturnUrl phải là đường dẫn Admin nội bộ. Ví dụ: /admin/settings?tab=drive.");
    }

    private static void ValidateFolderPaths(GoogleDriveFolderOptions folders)
    {
        var values = new Dictionary<string, string>
        {
            [nameof(folders.SurveyMedia)] = folders.SurveyMedia,
            [nameof(folders.CrmPreDesign)] = folders.CrmPreDesign,
            [nameof(folders.DesignConcept)] = folders.DesignConcept,
            [nameof(folders.DesignBasic)] = folders.DesignBasic,
            [nameof(folders.DesignShopDrawing)] = folders.DesignShopDrawing,
            [nameof(folders.LegalPermits)] = folders.LegalPermits,
            [nameof(folders.ConstructionAcceptance)] = folders.ConstructionAcceptance,
            [nameof(folders.Procurement)] = folders.Procurement,
            [nameof(folders.FinanceContracts)] = folders.FinanceContracts,
        };
        foreach (var (name, value) in values)
        {
            var segments = value.Split('/');
            if (value.Length is < 1 or > 500 || value.StartsWith('/') || value.EndsWith('/') ||
                value.Contains('\\') || HasControlCharacters(value) ||
                segments.Any(segment => segment.Length is < 1 or > 100 || segment is "." or ".."))
                throw new GoogleDriveSettingsValidationException(
                    $"Folders.{name} phải là đường dẫn thư mục hợp lệ. Ví dụ: 02_Thiet_ke/01_So_bo_Concept.");
        }
        if (values.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
            throw new GoogleDriveSettingsValidationException(
                "Các đường dẫn trong Folders không được trùng nhau. Ví dụ: mỗi nghiệp vụ dùng một thư mục riêng.");
    }

    private static void ValidateOptionalDraft(UpdateGoogleDriveConfigurationRequest settings)
    {
        if (settings.InstanceId.Length > 100 ||
            (settings.InstanceId.Length > 0 && !InstanceIdPattern.IsMatch(settings.InstanceId)))
            throw new GoogleDriveSettingsValidationException(
                "InstanceId phải dài 3-100 ký tự và chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang. Ví dụ: nicon-production.");
        if (settings.ApplicationName.Length > 100 || HasControlCharacters(settings.ApplicationName))
            throw new GoogleDriveSettingsValidationException(
                "ApplicationName không được vượt quá 100 ký tự. Ví dụ: Nicon Google Drive Integration.");
        var paths = FolderValues(settings.Folders);
        if (paths.Any(path => path.Length > 0) && paths.Any(path => path.Length == 0))
            throw new GoogleDriveSettingsValidationException(
                "Khi nhập cấu trúc thư mục, cần cung cấp đủ chín đường dẫn nghiệp vụ.");
        if (paths.All(path => path.Length > 0)) ValidateFolderPaths(settings.Folders);
    }

    private static string[] FolderValues(GoogleDriveFolderOptions folders) =>
    [
        folders.SurveyMedia, folders.CrmPreDesign, folders.DesignConcept,
        folders.DesignBasic, folders.DesignShopDrawing, folders.LegalPermits,
        folders.ConstructionAcceptance, folders.Procurement, folders.FinanceContracts,
    ];

    private static bool FolderPathsEqual(GoogleDriveCredential settings, GoogleDriveFolderOptions folders) =>
        FolderValues(ToFolders(settings)).SequenceEqual(FolderValues(folders), StringComparer.Ordinal);

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;

    private static bool HasControlCharacters(string value) => value.Any(char.IsControl);

    private string Unprotect(string? value, IDataProtector protector)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        try
        {
            return protector.Unprotect(value);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new GoogleDriveReconnectRequiredException(
                "Không thể giải mã cấu hình Google Drive. Hãy nhập lại ClientSecret và kết nối lại trong Cài đặt.",
                exception);
        }
    }

    private static GoogleDriveFolderOptions ToFolders(GoogleDriveCredential settings) => new()
    {
        SurveyMedia = settings.SurveyMediaFolder,
        CrmPreDesign = settings.CrmPreDesignFolder,
        DesignConcept = settings.DesignConceptFolder,
        DesignBasic = settings.DesignBasicFolder,
        DesignShopDrawing = settings.DesignShopDrawingFolder,
        LegalPermits = settings.LegalPermitsFolder,
        ConstructionAcceptance = settings.ConstructionAcceptanceFolder,
        Procurement = settings.ProcurementFolder,
        FinanceContracts = settings.FinanceContractsFolder,
    };

    private static void ApplyFolders(GoogleDriveCredential settings, GoogleDriveFolderOptions folders)
    {
        settings.SurveyMediaFolder = folders.SurveyMedia;
        settings.CrmPreDesignFolder = folders.CrmPreDesign;
        settings.DesignConceptFolder = folders.DesignConcept;
        settings.DesignBasicFolder = folders.DesignBasic;
        settings.DesignShopDrawingFolder = folders.DesignShopDrawing;
        settings.LegalPermitsFolder = folders.LegalPermits;
        settings.ConstructionAcceptanceFolder = folders.ConstructionAcceptance;
        settings.ProcurementFolder = folders.Procurement;
        settings.FinanceContractsFolder = folders.FinanceContracts;
    }

    private static GoogleDriveAdminConfigurationResponse EmptyAdminResponse()
    {
        var defaults = new GoogleDriveOptions();
        return new(false, string.Empty, false, false, string.Empty,
            defaults.FrontendReturnUrl, string.Empty, string.Empty, defaults.ApplicationName,
            defaults.Folders, defaults.SupportsAllDrives, defaults.PollIntervalSeconds,
            null, null, string.Empty);
    }

    private static GoogleDriveAdminConfigurationResponse ToAdminResponse(GoogleDriveCredential settings) => new(
        settings.Enabled,
        settings.ClientId,
        !string.IsNullOrWhiteSpace(settings.ProtectedClientSecret),
        !string.IsNullOrWhiteSpace(settings.ProtectedRefreshToken),
        settings.OAuthRedirectUri,
        settings.FrontendReturnUrl,
        settings.RootFolderId,
        settings.InstanceId,
        settings.ApplicationName,
        ToFolders(settings),
        settings.SupportsAllDrives,
        settings.PollIntervalSeconds,
        settings.AccountEmail,
        settings.ConnectedAt,
        ToVersion(settings.RowVersion));

    private static string ToVersion(byte[] rowVersion) =>
        rowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(rowVersion);
}

public sealed class GoogleDriveSettingsValidationException(string message) : InvalidOperationException(message);
public sealed class GoogleDriveSettingsConcurrencyException()
    : InvalidOperationException("Cấu hình Google Drive đã được thay đổi. Hãy tải lại trang và thử lại.");