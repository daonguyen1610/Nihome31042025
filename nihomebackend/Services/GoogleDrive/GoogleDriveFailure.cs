using System.Net;
using System.Net.Sockets;
using Google;
using Google.Apis.Auth.OAuth2.Responses;

namespace NihomeBackend.Services.GoogleDrive;

public enum GoogleDriveFailureKind
{
    /// <summary>Timeout, network, outage or rate/storage limit: retrying is enough.</summary>
    Transient,

    /// <summary>Configuration or credential problem: needs an administrator.</summary>
    Connection,

    /// <summary>A problem with this file or request: retrying will not fix it.</summary>
    Permanent,
}

public static class GoogleDriveFailure
{
    private static readonly HashSet<string> ConnectionTokenErrors = new(StringComparer.OrdinalIgnoreCase)
    {
        "invalid_client", "unauthorized_client", "admin_policy_enforced", "invalid_rapt",
    };

    private static readonly HashSet<string> LimitReasons = new(StringComparer.Ordinal)
    {
        "rateLimitExceeded", "userRateLimitExceeded", "sharingRateLimitExceeded",
        "quotaExceeded", "dailyLimitExceeded", "storageQuotaExceeded",
    };

    public static GoogleDriveFailureKind Classify(Exception exception)
    {
        if (GoogleDriveAuthenticationErrors.IsInvalidGrant(exception) || Chain(exception).Any(IsConnection))
            return GoogleDriveFailureKind.Connection;
        return Chain(exception).Any(IsTransient)
            ? GoogleDriveFailureKind.Transient
            : GoogleDriveFailureKind.Permanent;
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            yield return current;
    }

    private static bool IsConnection(Exception exception) => exception switch
    {
        TokenResponseException token => ConnectionTokenErrors.Contains(token.Error?.Error ?? string.Empty),
        GoogleApiException api => api.HttpStatusCode == HttpStatusCode.Unauthorized,
        _ => false,
    };

    private static bool IsTransient(Exception exception) => exception switch
    {
        // A request timeout; the worker handles a real shutdown before classifying.
        OperationCanceledException => true,
        HttpRequestException http => http.StatusCode is null || IsTemporary(http.StatusCode),
        SocketException => true,
        FileNotFoundException or DirectoryNotFoundException => false,
        IOException => true,
        GoogleApiException api => IsTemporary(api.HttpStatusCode) ||
            api.HttpStatusCode == HttpStatusCode.Forbidden &&
            api.Error?.Errors?.Any(error => LimitReasons.Contains(error.Reason ?? string.Empty)) == true,
        TokenResponseException token => token.StatusCode is HttpStatusCode.TooManyRequests or
            >= HttpStatusCode.InternalServerError,
        _ => false,
    };

    private static bool IsTemporary(HttpStatusCode? status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
            >= HttpStatusCode.InternalServerError;
}
