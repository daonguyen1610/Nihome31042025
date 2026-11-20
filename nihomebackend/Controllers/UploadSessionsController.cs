using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Infrastructure;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

/// <summary>
/// Chunk transport for resumable uploads, shared by every module. The client opens a
/// session for the upload request a file belongs to, sends it in chunks, then sends that
/// request with the session id instead of the bytes; <see cref="UploadSessionFormFilter"/>
/// hands the finished file to the module, which applies its own permission, type and size
/// rules as for any upload.
/// </summary>
[ApiController]
[Route("api/upload-sessions")]
[Authorize]
public sealed class UploadSessionsController(UploadSessionService sessions, SiteSettingsService settings) : ControllerBase
{
    /// <summary>
    /// Opens a session for one file. Refused before any byte is sent when the file is
    /// larger than every upload limit in Settings; the receiving module checks its own.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartUploadSessionRequest req, CancellationToken ct)
    {
        if (GetUserId() is not { } userId) return Unauthorized();
        var apiRoot = $"{Request.PathBase}/api/";
        if (string.IsNullOrWhiteSpace(req.TargetPath) || !req.TargetPath.StartsWith(apiRoot, StringComparison.OrdinalIgnoreCase) ||
            req.TargetPath.Length > UploadSession.TargetPathMaxLength)
            return BadRequest(new { code = "UPLOAD_TARGET_INVALID", message = "Không xác định được nơi nhận tệp. Hãy tải lại trang rồi thử lại." });
        var name = req.FileName?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 255 || name != Path.GetFileName(name) || name.Any(char.IsControl))
            return BadRequest(new { code = "UPLOAD_FILE_NAME_INVALID", message = "Tên tệp không hợp lệ. Hãy đổi tên tệp (tối đa 255 ký tự) rồi thử lại." });

        var largestMb = 0;
        foreach (var kind in Enum.GetValues<UploadLimitKind>())
            largestMb = Math.Max(largestMb, await settings.GetUploadLimitMbAsync(kind, ct));
        if (req.FileSize <= 0 || req.FileSize > (long)largestMb * 1024 * 1024)
            return BadRequest(new { code = "UPLOAD_TOO_LARGE", maxFileSizeMb = largestMb, message = $"Tệp vượt quá {largestMb} MB, mức lớn nhất trong Cài đặt. Hãy chọn tệp nhỏ hơn rồi thử lại." });
        if (await sessions.CountOpenAsync(userId, ct) >= UploadSessionService.MaxOpenSessionsPerUser)
            return Conflict(new { code = "UPLOAD_TOO_MANY_SESSIONS", message = $"Bạn đang có {UploadSessionService.MaxOpenSessionsPerUser} tệp tải dở. Hãy chờ các tệp đó xong hoặc huỷ bớt rồi thử lại." });

        var session = await sessions.CreateAsync(userId, req.TargetPath, name, req.FileSize, ct);
        return Ok(new { session.Id, session.ReceivedBytes, ChunkSize = UploadSessionService.ChunkSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (GetUserId() is not { } userId) return Unauthorized();
        var session = await sessions.FindAsync(id, userId, ct);
        return session is null
            ? NotFound(new { message = "Phiên tải lên không còn hoặc đã hết hạn. Hãy chọn lại tệp để tải từ đầu." })
            : Ok(new
            {
                session.Id,
                session.FileName,
                session.FileSize,
                session.ReceivedBytes,
                ChunkSize = UploadSessionService.ChunkSize,
            });
    }

    /// <summary>
    /// Stores the raw bytes of the chunk that starts at <paramref name="offset"/>. A chunk
    /// that was already stored is accepted again; any other offset gets 409 with the
    /// offset to resume from.
    /// </summary>
    [HttpPut("{id:guid}/chunks")]
    [RequestSizeLimit(UploadSessionService.ChunkSize + 64 * 1024)]
    public async Task<IActionResult> PutChunk(Guid id, [FromQuery] long offset, CancellationToken ct)
    {
        if (GetUserId() is not { } userId) return Unauthorized();
        var result = await sessions.AppendAsync(id, userId, offset, Request.Body, Request.ContentLength, ct);
        return result.Status switch
        {
            UploadChunkStatus.Accepted => Ok(new { result.ReceivedBytes }),
            UploadChunkStatus.OffsetMismatch => Conflict(new
            {
                code = "UPLOAD_OFFSET_MISMATCH",
                result.ReceivedBytes,
                message = $"Máy chủ đã nhận {result.ReceivedBytes} byte; tiếp tục gửi từ vị trí đó.",
            }),
            UploadChunkStatus.Invalid => BadRequest(new
            {
                code = "UPLOAD_CHUNK_INVALID",
                result.ReceivedBytes,
                message = $"Phần tệp không hợp lệ: mỗi phần từ 1 byte đến {UploadSessionService.ChunkSize / 1024 / 1024} MB, gửi đủ và không vượt quá dung lượng tệp đã khai báo.",
            }),
            _ => NotFound(new { message = "Phiên tải lên không còn hoặc đã hết hạn. Hãy chọn lại tệp để tải từ đầu." }),
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (GetUserId() is not { } userId) return Unauthorized();
        return await sessions.CancelAsync(id, userId, ct) ? NoContent() : NotFound();
    }

    private int? GetUserId() => UploadSessionFormFilter.UserId(User);
}

public sealed class StartUploadSessionRequest
{
    /// <summary>The API path the file will be sent to, e.g. /api/quotes/5/documents.</summary>
    public string? TargetPath { get; set; }
    public string? FileName { get; set; }
    public long FileSize { get; set; }
}
