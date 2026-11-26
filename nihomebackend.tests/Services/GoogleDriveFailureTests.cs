using System.Net;
using System.Net.Sockets;
using Google;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Requests;
using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Services;

public sealed class GoogleDriveFailureTests
{
    public static TheoryData<Exception, GoogleDriveFailureKind> Cases()
    {
        var data = new TheoryData<Exception, GoogleDriveFailureKind>
        {
            { new GoogleDriveReconnectRequiredException("reconnect"), GoogleDriveFailureKind.Connection },
            { Token("invalid_grant"), GoogleDriveFailureKind.Connection },
            { Token("invalid_client"), GoogleDriveFailureKind.Connection },
            { Token("unauthorized_client"), GoogleDriveFailureKind.Connection },
            { Token("admin_policy_enforced"), GoogleDriveFailureKind.Connection },
            { Token("invalid_rapt"), GoogleDriveFailureKind.Connection },
            { Api(HttpStatusCode.Unauthorized), GoogleDriveFailureKind.Connection },
            // A full Drive stays full until a person frees space.
            { Api(HttpStatusCode.Forbidden, "storageQuotaExceeded"), GoogleDriveFailureKind.Connection },
            { new InvalidOperationException("outer", Token("invalid_grant")), GoogleDriveFailureKind.Connection },
            { new AggregateException(Token("invalid_grant")), GoogleDriveFailureKind.Connection },
            // Connection wins over a Transient wrapper.
            { new HttpRequestException("send failed", Token("invalid_client")), GoogleDriveFailureKind.Connection },

            { new OperationCanceledException(), GoogleDriveFailureKind.Transient },
            { new TaskCanceledException(), GoogleDriveFailureKind.Transient },
            { new HttpRequestException("no response"), GoogleDriveFailureKind.Transient },
            { Http(HttpStatusCode.RequestTimeout), GoogleDriveFailureKind.Transient },
            { Http(HttpStatusCode.TooManyRequests), GoogleDriveFailureKind.Transient },
            { Http(HttpStatusCode.InternalServerError), GoogleDriveFailureKind.Transient },
            { new SocketException(), GoogleDriveFailureKind.Transient },
            { new IOException("connection reset"), GoogleDriveFailureKind.Transient },
            { new InvalidOperationException("outer", new SocketException()), GoogleDriveFailureKind.Transient },
            { Api(HttpStatusCode.RequestTimeout), GoogleDriveFailureKind.Transient },
            { Api(HttpStatusCode.TooManyRequests), GoogleDriveFailureKind.Transient },
            { Api(HttpStatusCode.ServiceUnavailable), GoogleDriveFailureKind.Transient },
            { Api(HttpStatusCode.Forbidden, "rateLimitExceeded"), GoogleDriveFailureKind.Transient },
            { Api(HttpStatusCode.Forbidden, "userRateLimitExceeded"), GoogleDriveFailureKind.Transient },
            { Api(HttpStatusCode.Forbidden, "sharingRateLimitExceeded"), GoogleDriveFailureKind.Transient },
            { Api(HttpStatusCode.Forbidden, "quotaExceeded"), GoogleDriveFailureKind.Transient },
            { Api(HttpStatusCode.Forbidden, "dailyLimitExceeded"), GoogleDriveFailureKind.Transient },
            { Token("temporarily_unavailable", HttpStatusCode.TooManyRequests), GoogleDriveFailureKind.Transient },
            { Token("internal_failure", HttpStatusCode.ServiceUnavailable), GoogleDriveFailureKind.Transient },

            { new FileNotFoundException(), GoogleDriveFailureKind.Permanent },
            { new DirectoryNotFoundException(), GoogleDriveFailureKind.Permanent },
            { Api(HttpStatusCode.NotFound), GoogleDriveFailureKind.Permanent },
            { Api(HttpStatusCode.Forbidden, "insufficientFilePermissions"), GoogleDriveFailureKind.Permanent },
            { Api(HttpStatusCode.Forbidden), GoogleDriveFailureKind.Permanent },
            { Http(HttpStatusCode.BadRequest), GoogleDriveFailureKind.Permanent },
            { Token("invalid_request"), GoogleDriveFailureKind.Permanent },
            { new InvalidOperationException("content mismatch"), GoogleDriveFailureKind.Permanent },
        };
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Classify_ReturnsTheKindThatDecidesWhoMustAct(Exception exception, GoogleDriveFailureKind expected) =>
        Assert.Equal(expected, GoogleDriveFailure.Classify(exception));

    private static TokenResponseException Token(string error, HttpStatusCode status = HttpStatusCode.BadRequest) =>
        new(new TokenErrorResponse { Error = error }, status);

    private static HttpRequestException Http(HttpStatusCode status) => new("failed", null, status);

    private static GoogleApiException Api(HttpStatusCode status, string? reason = null) => new("drive", "failed")
    {
        HttpStatusCode = status,
        Error = reason is null ? null : new RequestError { Errors = [new SingleError { Reason = reason }] },
    };
}
