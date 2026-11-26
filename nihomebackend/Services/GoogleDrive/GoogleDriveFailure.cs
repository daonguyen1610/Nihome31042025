using System.Net;
using System.Net.Sockets;
using Google;
using Google.Apis.Auth.OAuth2.Responses;

namespace NihomeBackend.Services.GoogleDrive;

public enum GoogleDriveFailureKind
{
    /// <summary>Timeout, network, outage or rate limit: retrying is enough.</summary>
    Transient,

    /// <summary>Configuration, credential or full-storage problem: needs an administrator.</summary>
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
        "quotaExceeded", "dailyLimitExceeded",
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
        // Someone has to restore or recreate the folder; the file itself is fine.
        ProjectDriveFolderMissingException => true,
        TokenResponseException token => ConnectionTokenErrors.Contains(token.Error?.Error ?? string.Empty),
        // A full Drive does not empty by itself.
        GoogleApiException api => api.HttpStatusCode == HttpStatusCode.Unauthorized ||
            IsForbiddenFor(api, reason => reason == "storageQuotaExceeded"),
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
            IsForbiddenFor(api, LimitReasons.Contains),
        TokenResponseException token => token.StatusCode is HttpStatusCode.TooManyRequests or
            >= HttpStatusCode.InternalServerError,
        _ => false,
    };

    private static bool IsForbiddenFor(GoogleApiException api, Func<string, bool> reasonMatches) =>
        api.HttpStatusCode == HttpStatusCode.Forbidden &&
        api.Error?.Errors?.Any(error => reasonMatches(error.Reason ?? string.Empty)) == true;

    private static bool IsTemporary(HttpStatusCode? status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
            >= HttpStatusCode.InternalServerError;
}
