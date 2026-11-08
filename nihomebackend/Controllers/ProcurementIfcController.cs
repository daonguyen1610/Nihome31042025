using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController, Authorize]
[Route("api/operational-projects/{projectId:int}/procurement/ifc")]
[Route("api/v1/operational-projects/{projectId:int}/procurement/ifc")]
public sealed class ProcurementIfcController(ProcurementIfcService service, IShopDrawingService drawings,
    IAuditLogger audit, ILogger<ProcurementIfcController> logger) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet, RequirePermission("proc.ifc", "view")]
    public async Task<IActionResult> List(int projectId, [FromQuery, Range(1, int.MaxValue / 20)] int page = 1, CancellationToken ct = default) =>
        !await service.IsMemberAsync(projectId, UserId, ct) ? NotFound() : Ok(await service.ListAsync(projectId, UserId, page, ct));

    [HttpPost("{recipientId:int}/acknowledge"), RequirePermission("proc.ifc", "acknowledge"), RequirePermission("proc.ifc", "view")]
    public async Task<IActionResult> Acknowledge(int projectId, int recipientId,
        AcknowledgeIfcReleaseRecipientRequest request, CancellationToken ct)
    {
        if (!await service.IsMemberAsync(projectId, UserId, ct)) return NotFound();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        var result = await service.AcknowledgeAsync(projectId, recipientId, UserId, request, ct);
        if (result is null) return NotFound();
        if (result.Recorded) audit.Log(new AuditEvent
        {
            Action = "ifc.procurement.received",
            ResourceType = "IfcReleaseRecipient",
            ResourceId = recipientId.ToString(),
            Message = $"IFC recipient {recipientId} received by assigned user {UserId}."
        });
        return Ok(result.Packet);
    }

    [HttpGet("{recipientId:int}/items/{itemId:int}/content"), RequirePermission("proc.ifc", "view")]
    public async Task<IActionResult> Content(int projectId, int recipientId, int itemId, CancellationToken ct)
    {
        if (!await service.IsMemberAsync(projectId, UserId, ct)) return NotFound();
        var item = await service.ItemAsync(projectId, recipientId, itemId, UserId, ct);
        if (item is null) return NotFound();
        var content = await drawings.GetContentAsync(item.ShopDrawingId, ct);
        if (content is null) return NotFound();
        // Serve the verified private copy, so a later source replacement cannot
        // alter the bytes after verification and before the response completes.
        var stream = new FileStream(Path.GetTempFileName(), FileMode.Open, FileAccess.ReadWrite,
            FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        string hash;
        try
        {
            await using var source = new FileStream(content.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await source.CopyToAsync(stream, ct);
            stream.Position = 0;
            hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
        if (string.IsNullOrWhiteSpace(item.ReleasedFileSha256) || !string.Equals(hash, item.ReleasedFileSha256, StringComparison.OrdinalIgnoreCase))
        {
            await stream.DisposeAsync();
            logger.LogWarning("IFC download blocked for project {ProjectId}, item {ItemId}: released checksum mismatch. Contact Design to restore released evidence.", projectId, itemId);
            return Conflict(new { code = "IFC_RELEASED_FILE_CHANGED", message = "Tệp không khớp hồ sơ đã phát hành. Liên hệ Thiết kế kiểm tra phiên bản IFC." });
        }
        stream.Position = 0;
        return File(stream, content.ContentType, content.OriginalFileName, enableRangeProcessing: true);
    }
}
