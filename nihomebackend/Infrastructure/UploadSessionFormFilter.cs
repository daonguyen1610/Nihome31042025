using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using NihomeBackend.Authorization;
using NihomeBackend.Services;

namespace NihomeBackend.Infrastructure;

/// <summary>
/// Lets any multipart upload endpoint receive files that were sent beforehand in resumable
/// chunks. The client sends the ordinary upload request with
/// <c>X-Upload-Sessions: field=sessionId,…</c> instead of those files' bytes; this filter
/// adds each finished file to the form under its field before model binding, so the
/// module validates and stores it exactly like a direct upload. The sessions close when the
/// request succeeds or the module refuses the file for good; a failure worth retrying
/// (see <see cref="KeepsSessions"/>) keeps them, so it can be sent again without re-uploading.
/// </summary>
public sealed class UploadSessionFormFilter(UploadSessionService sessions) : IAsyncResourceFilter
{
    public const string HeaderName = "X-Upload-Sessions";

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static int? UserId(ClaimsPrincipal user) =>
        PermissionAuthorizationFilter.ResolveUserId(user) is > 0 and var id ? id : null;

    /// <summary>
    /// Failures after which the same request may succeed when sent again (sign-in expired,
    /// timeout, edit conflict, throttling, server error); the staged files stay for it.
    /// Kept in step with <c>RETRYABLE_UPLOAD_STATUSES</c> in nihomeweb/src/lib/chunkedUpload.ts.
    /// </summary>
    public static bool KeepsSessions(int status) => status is 401 or 408 or 409 or 429 or >= 500;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var header = http.Request.Headers[HeaderName];
        if (StringValues.IsNullOrEmpty(header))
        {
            await next();
            return;
        }

        if (UserId(http.User) is not { } userId)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        var fields = Parse(header.ToString());
        if (fields is null || fields.Count == 0 || !http.Request.HasFormContentType)
        {
            context.Result = NotReady();
            return;
        }

        var ct = http.RequestAborted;
        var target = $"{http.Request.PathBase}{http.Request.Path}";
        await using var held = await sessions.HoldCompletedAsync(fields.Select(item => item.Id).ToList(), userId, target, ct);
        if (held is null)
        {
            context.Result = NotReady();
            return;
        }

        var form = await http.Request.ReadFormAsync(ct);
        var files = new FormFileCollection();
        files.AddRange(form.Files);
        var streams = new List<FileStream>();
        try
        {
            foreach (var (field, id) in fields)
            {
                var staged = held.Files.First(file => file.Id == id);
                var stream = new FileStream(staged.StagingPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                streams.Add(stream);
                files.Add(new FormFile(stream, 0, staged.FileSize, field, staged.FileName)
                {
                    Headers = new HeaderDictionary
                    {
                        [HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("form-data")
                        {
                            Name = field,
                            FileName = staged.FileName,
                            FileNameStar = staged.FileName,
                        }.ToString(),
                        [HeaderNames.ContentType] = ContentTypes.TryGetContentType(staged.FileName, out var type)
                            ? type
                            : "application/octet-stream",
                    },
                });
            }
            var values = form.ToDictionary(item => item.Key, item => item.Value);
            // Tied to the request (not just the collection), as model binding expects.
            http.Features.Set<IFormFeature>(new FormFeature(http.Request) { Form = new FormCollection(values, files) });

            var executed = await next();
            var finished = (executed.Exception is null || executed.ExceptionHandled) &&
                           !KeepsSessions(http.Response.StatusCode);
            foreach (var stream in streams) await stream.DisposeAsync();
            streams.Clear();
            // Stored, or refused for good (wrong type, over the module's limit): the staged
            // copy is of no further use and should not hold disk space for a day.
            if (finished) await held.CloseAsync();
        }
        finally
        {
            foreach (var stream in streams) await stream.DisposeAsync();
        }
    }

    /// <summary>"field=id,field=id"; field names are URL-encoded. Null when malformed.</summary>
    private static List<(string Field, Guid Id)>? Parse(string header)
    {
        var fields = new List<(string, Guid)>();
        foreach (var pair in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) return null;
            var field = Uri.UnescapeDataString(parts[0]);
            if (string.IsNullOrWhiteSpace(field)) return null;
            fields.Add((field, id));
        }
        return fields.Select(item => item.Item2).Distinct().Count() == fields.Count ? fields : null;
    }

    private static ObjectResult NotReady() => new BadRequestObjectResult(new
    {
        code = "UPLOAD_NOT_READY",
        message = "Tệp chưa tải lên xong hoặc phiên tải lên không thuộc thao tác này. Hãy tải tiếp hoặc chọn lại tệp.",
    });
}
