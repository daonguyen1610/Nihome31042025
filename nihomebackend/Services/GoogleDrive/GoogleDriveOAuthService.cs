using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.AspNetCore.DataProtection;

namespace NihomeBackend.Services.GoogleDrive;

public sealed record GoogleDriveOAuthStartResponse(string AuthorizationUrl);
public sealed record GoogleDriveDisconnectResponse(bool HadStoredCredential, bool ProviderRevoked);

public sealed record GoogleDriveAdminStatusResponse(
    string Status,
    bool OAuthConfigured,
    bool HasStoredCredential,
    string? AccountEmail,
    DateTime? ConnectedAt,
    string? RootFolderName,
    string? RootFolderLink,
    string? Error);

public enum GoogleDriveOAuthResult
{
    Success,
    Denied,
    InvalidState,
    AuthorizationExpired,
    TokenExchangeFailed,
    MissingRefreshToken,
    RootValidationFailed,
    ConfigurationChanged,
}

internal sealed record GoogleDriveOAuthState(
    int UserId,
    DateTime ExpiresAt,
    string CodeVerifier,
    string ConfigurationVersion);

internal sealed record GoogleDriveTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("error_description")] string? ErrorDescription);

public sealed class GoogleDriveOAuthService(
    IDataProtectionProvider dataProtectionProvider,
    IGoogleDriveSettingsStore settingsStore,
    IHttpClientFactory httpClientFactory,
    IGoogleDriveAdapter drive,
    IPermissionService permissions,
    ILogger<GoogleDriveOAuthService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector stateProtector = dataProtectionProvider.CreateProtector(
        "Nicon.GoogleDrive.OAuthState.v1");

    public async Task<GoogleDriveOAuthStartResponse> CreateAuthorizationRequestAsync(
        int userId,
        CancellationToken ct = default)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        EnsureOAuthConfiguration(options);
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = stateProtector.Protect(JsonSerializer.Serialize(
            new GoogleDriveOAuthState(
                userId, DateTime.UtcNow.AddMinutes(10), verifier, options.ConfigurationVersion), JsonOptions));
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.OAuthRedirectUri,
            ["response_type"] = "code",
            ["scope"] = "https://www.googleapis.com/auth/drive",
            ["access_type"] = "offline",
            ["prompt"] = "consent select_account",
            ["include_granted_scopes"] = "true",
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        };
        return new GoogleDriveOAuthStartResponse(
            Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
                "https://accounts.google.com/o/oauth2/v2/auth", query));
    }

    public async Task<GoogleDriveOAuthResult> CompleteAsync(
        string? code,
        string? state,
        string? providerError,
        CancellationToken ct = default)
    {
        GoogleDriveOAuthState oauthState;
        try
        {
            if (string.IsNullOrWhiteSpace(state)) return GoogleDriveOAuthResult.InvalidState;
            oauthState = JsonSerializer.Deserialize<GoogleDriveOAuthState>(
                stateProtector.Unprotect(state), JsonOptions)
                ?? throw new InvalidOperationException("OAuth state is empty.");
            if (oauthState.ExpiresAt < DateTime.UtcNow || oauthState.UserId <= 0)
                return GoogleDriveOAuthResult.InvalidState;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.LogWarning("Google Drive OAuth callback rejected an invalid or expired state.");
            return GoogleDriveOAuthResult.InvalidState;
        }

        if (!string.IsNullOrWhiteSpace(providerError))
            return GoogleDriveOAuthResult.Denied;

        if (string.IsNullOrWhiteSpace(code)) return GoogleDriveOAuthResult.TokenExchangeFailed;
        if (!await permissions.HasAsync(oauthState.UserId, "system.settings.manage", ct))
        {
            logger.LogWarning(
                "Google Drive OAuth callback rejected because the initiating user is no longer authorized.");
            return GoogleDriveOAuthResult.AuthorizationExpired;
        }

        var options = await settingsStore.GetRuntimeAsync(ct);
        if (!string.Equals(
                options.ConfigurationVersion,
                oauthState.ConfigurationVersion,
                StringComparison.Ordinal))
            return GoogleDriveOAuthResult.ConfigurationChanged;
        EnsureOAuthConfiguration(options);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["code"] = code,
                ["code_verifier"] = oauthState.CodeVerifier,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = options.OAuthRedirectUri,
            }),
        };
        using var response = await httpClientFactory.CreateClient(nameof(GoogleDriveOAuthService))
            .SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        var token = JsonSerializer.Deserialize<GoogleDriveTokenResponse>(payload, JsonOptions);
        if (!response.IsSuccessStatusCode || token is null)
        {
            logger.LogWarning(
                "Google Drive authorization-code exchange failed with status {StatusCode} and error {OAuthError}.",
                (int)response.StatusCode,
                token?.Error ?? "unknown");
            return GoogleDriveOAuthResult.TokenExchangeFailed;
        }
        if (string.IsNullOrWhiteSpace(token.RefreshToken))
            return GoogleDriveOAuthResult.MissingRefreshToken;

        if (!await CanManageConfiguredRootAsync(options, token.AccessToken, ct))
            return GoogleDriveOAuthResult.RootValidationFailed;

        var accountEmail = await TryGetAccountEmailAsync(token.AccessToken, ct);
        try
        {
            await settingsStore.SaveRefreshTokenAsync(
                token.RefreshToken,
                accountEmail,
                oauthState.UserId,
                oauthState.ConfigurationVersion,
                ct);
        }
        catch (GoogleDriveSettingsConcurrencyException)
        {
            return GoogleDriveOAuthResult.ConfigurationChanged;
        }
        return GoogleDriveOAuthResult.Success;
    }

    public async Task<GoogleDriveDisconnectResponse> DisconnectAsync(
        int userId,
        CancellationToken ct = default)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        var hadStoredCredential = !string.IsNullOrWhiteSpace(options.RefreshToken);
        if (!hadStoredCredential)
            return new GoogleDriveDisconnectResponse(false, false);

        await settingsStore.ClearRefreshTokenAsync(userId, options.ConfigurationVersion, ct);

        var providerRevoked = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/revoke")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = options.RefreshToken,
                }),
            };
            using var response = await httpClientFactory.CreateClient(nameof(GoogleDriveOAuthService))
                .SendAsync(request, ct);
            providerRevoked = response.IsSuccessStatusCode;
            if (!providerRevoked)
                logger.LogWarning(
                    "Google Drive credential revocation returned status {StatusCode}; the local credential will still be removed.",
                    (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                "Google Drive credential revocation timed out; the local credential was already removed.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Google Drive credential revocation failed ({ExceptionType}); the local credential will still be removed.",
                exception.GetType().Name);
        }

        return new GoogleDriveDisconnectResponse(hadStoredCredential, providerRevoked);
    }

    public async Task<GoogleDriveAdminStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        var metadata = await settingsStore.GetAdminAsync(ct);
        var options = await settingsStore.GetRuntimeAsync(ct);
        var configured = options.Enabled &&
            !string.IsNullOrWhiteSpace(options.ClientId) &&
            !string.IsNullOrWhiteSpace(options.ClientSecret) &&
            !string.IsNullOrWhiteSpace(options.OAuthRedirectUri);
        if (!options.Enabled)
            return new("Disabled", configured, metadata.HasRefreshToken,
                metadata.AccountEmail, metadata.ConnectedAt, null, null, null);
        if (!metadata.HasRefreshToken)
            return new("ReconnectRequired", configured, false,
                null, null, null, null, "Chưa có tài khoản Google Drive được kết nối.");

        try
        {
            var connection = await drive.CheckConnectionAsync(ct);
            var status = !connection.IsFolder || connection.IsTrashed
                ? "InvalidRoot"
                : connection.CanAddChildren ? "Connected" : "ReadOnly";
            return new(status, configured, metadata.HasRefreshToken,
                connection.AccountEmail ?? metadata.AccountEmail, metadata.ConnectedAt,
                connection.FolderName, connection.FolderLink,
                status == "InvalidRoot"
                    ? "RootFolderId phải trỏ đến một thư mục Google Drive chưa bị chuyển vào thùng rác."
                    : null);
        }
        catch (Exception exception)
        {
            var reconnectRequired = GoogleDriveAuthenticationErrors.IsInvalidGrant(exception);
            logger.LogWarning(
                "Google Drive health check failed ({ExceptionType}, reconnect required: {ReconnectRequired}).",
                exception.GetType().Name,
                reconnectRequired);
            return new(reconnectRequired ? "ReconnectRequired" : "Unavailable", configured,
                metadata.HasRefreshToken,
                metadata.AccountEmail, metadata.ConnectedAt, null, null,
                reconnectRequired
                    ? "Quyền truy cập Google Drive đã hết hạn hoặc bị thu hồi. Hãy kết nối lại."
                    : "Không thể xác thực hoặc truy cập Google Drive.");
        }
    }

    public async Task<string> BuildFrontendResultUrlAsync(
        GoogleDriveOAuthResult result,
        CancellationToken ct = default)
    {
        var resultValue = result switch
        {
            GoogleDriveOAuthResult.Success => "success",
            GoogleDriveOAuthResult.Denied => "denied",
            GoogleDriveOAuthResult.InvalidState => "invalid_state",
            GoogleDriveOAuthResult.AuthorizationExpired => "authorization_expired",
            GoogleDriveOAuthResult.MissingRefreshToken => "missing_refresh_token",
            GoogleDriveOAuthResult.RootValidationFailed => "root_validation_failed",
            GoogleDriveOAuthResult.ConfigurationChanged => "configuration_changed",
            _ => "failed",
        };
        var options = await settingsStore.GetRuntimeAsync(ct);
        var returnUrl = string.IsNullOrWhiteSpace(options.FrontendReturnUrl)
            ? "/admin/settings?tab=drive"
            : options.FrontendReturnUrl;
        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
            returnUrl, "driveOAuth", resultValue);
    }

    private async Task<string?> TryGetAccountEmailAsync(string? accessToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return null;
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://www.googleapis.com/drive/v3/about?fields=user(emailAddress)");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClientFactory.CreateClient(nameof(GoogleDriveOAuthService))
            .SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.TryGetProperty("user", out var user) &&
               user.TryGetProperty("emailAddress", out var email)
            ? email.GetString()
            : null;
    }

    private async Task<bool> CanManageConfiguredRootAsync(
        GoogleDriveOptions options,
        string? accessToken,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(options.RootFolderId))
            return false;
        var rootFolderId = Uri.EscapeDataString(options.RootFolderId);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://www.googleapis.com/drive/v3/files/{rootFolderId}?fields=mimeType,trashed,capabilities(canAddChildren)&supportsAllDrives=true");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClientFactory.CreateClient(nameof(GoogleDriveOAuthService))
            .SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "New Google Drive credential cannot access the configured root ({StatusCode}).",
                (int)response.StatusCode);
            return false;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;
        return root.TryGetProperty("mimeType", out var mimeType) &&
               mimeType.GetString() == "application/vnd.google-apps.folder" &&
               (!root.TryGetProperty("trashed", out var trashed) || !trashed.GetBoolean()) &&
               root.TryGetProperty("capabilities", out var capabilities) &&
               capabilities.TryGetProperty("canAddChildren", out var canAddChildren) &&
               canAddChildren.GetBoolean();
    }

    private static void EnsureOAuthConfiguration(GoogleDriveOptions options)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret) ||
            string.IsNullOrWhiteSpace(options.OAuthRedirectUri))
        {
            throw new InvalidOperationException(
                "Google Drive OAuth chưa được cấu hình đầy đủ để kết nối tài khoản.");
        }
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public static class GoogleDriveAuthenticationErrors
{
    public static bool IsInvalidGrant(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is GoogleDriveReconnectRequiredException) return true;
            if (current is TokenResponseException tokenException &&
                string.Equals(tokenException.Error?.Error, "invalid_grant", StringComparison.OrdinalIgnoreCase))
                return true;
            if (current.Message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

public sealed class GoogleDriveReconnectRequiredException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);