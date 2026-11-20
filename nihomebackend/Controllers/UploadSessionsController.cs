using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

/// <summary>
/// Chunk transport for resumable uploads. A module opens the session (checking its own
/// permission, type and size rules) and takes the finished file; here the owner only
/// sends chunks, asks where to resume, or cancels.
/// </summary>
[ApiController]
[Route("api/upload-sessions")]
[Authorize]
public sealed class UploadSessionsController(UploadSessionService sessions) : ControllerBase
{
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

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
