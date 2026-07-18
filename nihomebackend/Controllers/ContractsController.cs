using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

/// <summary>
/// CRM Contract endpoints — NIH-102/103/104 combined surface.
///
/// * Sales users (<c>crm.contracts.view</c>) see only rows they own.
/// * Sales Manager / Legal / BOD / Admin gain <c>view.all</c> via the
///   RBAC bundle (through <c>crm.**</c>, <c>**.view</c>, etc.).
/// * VO approve/reject requires the same manager-tier permission.
/// </summary>
[ApiController]
[Route("api/contracts")]
[Route("api/v1/contracts")]
[Authorize]
public class ContractsController(
    IContractService svc,
    IContractAppendixService voSvc,
    IContractAttachmentService attSvc,
    IPermissionService permissions,
    IWebHostEnvironment env,
    AppDbContext db,
    IAuditLogger audit) : ControllerBase
{
    private const long MaxFileSizeBytes = 20L * 1024 * 1024; // 20MB per parent story
    private const string StorageSubfolder = "contracts";

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg",
        };

    [HttpGet]
    [RequirePermission("crm.contracts", "view")]
    public async Task<ActionResult<ContractListResponse>> List(
        [FromQuery] ContractStatus? status,
        [FromQuery] int? ownerUserId,
        [FromQuery] int? customerId,
        [FromQuery] string? search,
        [FromQuery] DateTime? signedFrom,
        [FromQuery] DateTime? signedTo,
        [FromQuery] decimal? valueMin,
        [FromQuery] decimal? valueMax,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        var result = await svc.ListAsync(
            userId.Value, canSeeAll, status, ownerUserId, customerId, search,
            signedFrom, signedTo, valueMin, valueMax, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("crm.contracts", "view")]
    public async Task<ActionResult<ContractResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        var found = await svc.GetAsync(id, userId.Value, canSeeAll, ct);
        return found == null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<ActionResult<ContractResponse>> Create(
        [FromBody] UpsertContractRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Only manager-tier callers (crm.contracts.view.all) can pin the
        // owner to a different user on create; sales users always own what
        // they create so they can still see it.
        var canReassignOwner = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        try
        {
            var created = await svc.CreateAsync(req, userId.Value, canReassignOwner, ct);
            audit.Log(new AuditEvent
            {
                Action = "contract.create",
                ResourceType = EntityTypes.Contract,
                ResourceId = created.Id.ToString(),
                Message = $"Contract #{created.Id} created ({created.ContractNumber}).",
                NewValue = created,
            });
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (ContractDuplicateNumberException ex)
        {
            return Conflict(new { message = ex.Message, contractNumber = ex.ContractNumber });
        }
        catch (ContractValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<ActionResult<ContractResponse>> Update(
        int id, [FromBody] UpsertContractRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        // Sales cannot reassign owner — only manager-tier callers can.
        var canReassignOwner = canSeeAll;
        try
        {
            var updated = await svc.UpdateAsync(id, req, userId.Value, canSeeAll, canReassignOwner, ct);
            if (updated == null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "contract.update",
                ResourceType = EntityTypes.Contract,
                ResourceId = id.ToString(),
                Message = $"Contract #{id} updated.",
                NewValue = updated,
            });
            return Ok(updated);
        }
        catch (ContractDuplicateNumberException ex)
        {
            return Conflict(new { message = ex.Message, contractNumber = ex.ContractNumber });
        }
        catch (ContractValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        var removed = await svc.DeleteAsync(id, userId.Value, canSeeAll, ct);
        if (!removed) return NotFound();

        audit.Log(new AuditEvent
        {
            Action = "contract.delete",
            ResourceType = EntityTypes.Contract,
            ResourceId = id.ToString(),
            Message = $"Contract #{id} deleted.",
        });
        return NoContent();
    }

    // -------- state transitions --------

    [HttpPost("{id:int}/transition")]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<ActionResult<ContractResponse>> Transition(
        int id, [FromBody] ContractStatusTransitionRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        try
        {
            var updated = await svc.TransitionStatusAsync(id, req.NewStatus, userId.Value, canSeeAll, ct);
            if (updated == null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "contract.transition",
                ResourceType = EntityTypes.Contract,
                ResourceId = id.ToString(),
                Message = $"Contract #{id} → {req.NewStatus}.",
            });
            return Ok(updated);
        }
        catch (ContractValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // -------- milestone status --------

    [HttpPatch("{id:int}/milestones/{milestoneId:int}/status")]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<ActionResult<ContractResponse>> UpdateMilestoneStatus(
        int id, int milestoneId, [FromBody] UpdateMilestoneStatusRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        var updated = await svc.UpdateMilestoneStatusAsync(id, milestoneId, req.Status, userId.Value, canSeeAll, ct);
        if (updated == null) return NotFound();

        audit.Log(new AuditEvent
        {
            Action = "contract.milestone.status",
            ResourceType = EntityTypes.Contract,
            ResourceId = id.ToString(),
            Message = $"Contract #{id} milestone #{milestoneId} → {req.Status}.",
        });
        return Ok(updated);
    }

    // -------- appendices (VO) --------

    [HttpGet("{id:int}/appendices")]
    [RequirePermission("crm.contracts", "view")]
    public async Task<ActionResult<List<ContractAppendixResponse>>> ListAppendices(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        var rows = await voSvc.ListAsync(id, userId.Value, canSeeAll, ct);
        return rows == null ? NotFound() : Ok(rows);
    }

    [HttpPost("{id:int}/appendices")]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<ActionResult<ContractAppendixResponse>> CreateAppendix(
        int id, [FromBody] UpsertContractAppendixRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        try
        {
            var created = await voSvc.CreateAsync(id, req, userId.Value, canSeeAll, ct);
            if (created == null) return NotFound();

            audit.Log(new AuditEvent
            {
                Action = "contract.vo.create",
                ResourceType = EntityTypes.Contract,
                ResourceId = id.ToString(),
                Message = $"Contract #{id} VO#{created.VoNumber} created.",
                NewValue = created,
            });
            return CreatedAtAction(nameof(ListAppendices), new { id }, created);
        }
        catch (ContractValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/appendices/{voId:int}")]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<ActionResult<ContractAppendixResponse>> UpdateAppendix(
        int id, int voId, [FromBody] UpsertContractAppendixRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        try
        {
            var updated = await voSvc.UpdateAsync(id, voId, req, userId.Value, canSeeAll, ct);
            if (updated == null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "contract.vo.update",
                ResourceType = EntityTypes.Contract,
                ResourceId = id.ToString(),
                Message = $"Contract #{id} VO#{updated.VoNumber} updated.",
                NewValue = updated,
            });
            return Ok(updated);
        }
        catch (ContractValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/appendices/{voId:int}/submit")]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<ActionResult<ContractAppendixResponse>> SubmitAppendix(int id, int voId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        try
        {
            var updated = await voSvc.SubmitAsync(id, voId, userId.Value, canSeeAll, ct);
            if (updated == null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "contract.vo.submit",
                ResourceType = EntityTypes.Contract,
                ResourceId = id.ToString(),
                Message = $"Contract #{id} VO#{updated.VoNumber} submitted for approval.",
            });
            return Ok(updated);
        }
        catch (ContractValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/appendices/{voId:int}/approve")]
    [RequirePermission("crm.contracts", "view.all")]
    public async Task<ActionResult<ContractAppendixResponse>> ApproveAppendix(
        int id, int voId, [FromBody] ContractAppendixDecisionRequest? req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            // Approvers implicitly see all rows via the manager permission.
            var updated = await voSvc.ApproveAsync(id, voId, req?.Note, userId.Value, canSeeAll: true, ct);
            if (updated == null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "contract.vo.approve",
                ResourceType = EntityTypes.Contract,
                ResourceId = id.ToString(),
                Message = $"Contract #{id} VO#{updated.VoNumber} approved ({updated.ValueDelta:N0}).",
            });
            return Ok(updated);
        }
        catch (ContractValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/appendices/{voId:int}/reject")]
    [RequirePermission("crm.contracts", "view.all")]
    public async Task<ActionResult<ContractAppendixResponse>> RejectAppendix(
        int id, int voId, [FromBody] ContractAppendixDecisionRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var updated = await voSvc.RejectAsync(id, voId, req?.Note, userId.Value, canSeeAll: true, ct);
            if (updated == null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "contract.vo.reject",
                ResourceType = EntityTypes.Contract,
                ResourceId = id.ToString(),
                Message = $"Contract #{id} VO#{updated.VoNumber} rejected.",
            });
            return Ok(updated);
        }
        catch (ContractValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}/appendices/{voId:int}")]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<IActionResult> DeleteAppendix(int id, int voId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        try
        {
            var removed = await voSvc.DeleteAsync(id, voId, userId.Value, canSeeAll, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "contract.vo.delete",
                ResourceType = EntityTypes.Contract,
                ResourceId = id.ToString(),
                Message = $"Contract #{id} VO#{voId} deleted.",
            });
            return NoContent();
        }
        catch (ContractValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Upload a single VO attachment. Returns the host-relative
    /// storage path; the FE feeds that into the VO create/update JSON call.</summary>
    [HttpPost("{id:int}/appendices/files")]
    [Consumes("multipart/form-data")]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<IActionResult> UploadAppendixFile(int id, [FromForm] IFormFile? file, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        var owns = await ContractExistsForCallerAsync(id, userId.Value, canSeeAll, ct);
        if (!owns) return NotFound();

        var stored = await StoreUploadedFileAsync(file, "vo", ct);
        return stored.Result ?? Ok(stored.Payload);
    }

    // -------- attachments --------

    [HttpGet("{id:int}/attachments")]
    [RequirePermission("crm.contracts", "view")]
    public async Task<ActionResult<List<ContractAttachmentResponse>>> ListAttachments(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        var rows = await attSvc.ListAsync(id, userId.Value, canSeeAll, ct);
        return rows == null ? NotFound() : Ok(rows);
    }

    /// <summary>
    /// Upload + register a single attachment in one multipart call. The
    /// file kind and optional label ride on the form body.
    /// </summary>
    [HttpPost("{id:int}/attachments")]
    [Consumes("multipart/form-data")]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<ActionResult<ContractAttachmentResponse>> UploadAttachment(
        int id,
        [FromForm] IFormFile? file,
        [FromForm] ContractAttachmentKind kind,
        [FromForm] string? label,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        var owns = await ContractExistsForCallerAsync(id, userId.Value, canSeeAll, ct);
        if (!owns) return NotFound();

        var stored = await StoreUploadedFileAsync(file, "att", ct);
        if (stored.Result is BadRequestObjectResult bad) return bad;
        if (stored.Payload == null) return BadRequest(new { message = "Không upload được file." });

        var created = await attSvc.CreateAsync(id, new CreateContractAttachmentRequest
        {
            Kind = kind,
            FilePath = stored.Payload!.filePath,
            OriginalFileName = stored.Payload.originalFileName,
            FileSize = stored.Payload.fileSize,
            ContentType = stored.Payload.contentType,
            Label = label,
        }, userId.Value, canSeeAll, ct);
        // ContractExistsForCallerAsync above already verified the caller
        // may write to this contract, so CreateAsync should never return
        // null here — but be defensive so a race between the ownership
        // check and the row disappearing doesn't 500.
        if (created == null) return NotFound();

        audit.Log(new AuditEvent
        {
            Action = "contract.attachment.create",
            ResourceType = EntityTypes.Contract,
            ResourceId = id.ToString(),
            Message = $"Contract #{id} attachment ({kind}) uploaded — {stored.Payload.originalFileName}.",
        });

        return CreatedAtAction(nameof(ListAttachments), new { id }, created);
    }

    [HttpDelete("{id:int}/attachments/{attachmentId:int}")]
    [RequirePermission("crm.contracts", "manage")]
    public async Task<IActionResult> DeleteAttachment(int id, int attachmentId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        var removed = await attSvc.DeleteAsync(id, attachmentId, userId.Value, canSeeAll, ct);
        if (!removed) return NotFound();

        audit.Log(new AuditEvent
        {
            Action = "contract.attachment.delete",
            ResourceType = EntityTypes.Contract,
            ResourceId = id.ToString(),
            Message = $"Contract #{id} attachment #{attachmentId} deleted.",
        });
        return NoContent();
    }

    // -------- timeline --------

    [HttpGet("{id:int}/timeline")]
    [RequirePermission("crm.contracts", "view")]
    public async Task<ActionResult<List<ContractTimelineEvent>>> Timeline(
        int id, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var canSeeAll = await permissions.HasAsync(userId.Value, "crm.contracts.view.all", ct);
        var owns = await ContractExistsForCallerAsync(id, userId.Value, canSeeAll, ct);
        if (!owns) return NotFound();

        if (limit < 1) limit = 1;
        if (limit > 500) limit = 500;

        var rows = await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.ResourceType == EntityTypes.Contract && a.ResourceId == id.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new
            {
                a.Id,
                a.CreatedAt,
                a.Action,
                a.Message,
                a.ActorUserId,
                UserName = a.ActorUserId != null
                    ? db.Users.Where(u => u.Id == a.ActorUserId).Select(u => u.FullName).FirstOrDefault()
                    : null,
            })
            .ToListAsync(ct);

        var events = rows.Select(a => new ContractTimelineEvent
        {
            Id = a.Id,
            OccurredAt = a.CreatedAt,
            Action = a.Action,
            Message = a.Message,
            UserId = a.ActorUserId,
            UserName = a.UserName,
        }).ToList();

        return Ok(events);
    }

    // -------- upload helpers --------

    private record UploadedPayload(string filePath, string originalFileName, long fileSize, string contentType);

    private struct StoredFile
    {
        public UploadedPayload? Payload;
        public IActionResult? Result;
    }

    private async Task<StoredFile> StoreUploadedFileAsync(IFormFile? file, string kindTag, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return new StoredFile { Result = BadRequest(new { message = "Chưa chọn file." }) };
        }
        if (file.Length > MaxFileSizeBytes)
        {
            return new StoredFile { Result = BadRequest(new { message = "File quá lớn (tối đa 20MB)." }) };
        }
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return new StoredFile { Result = BadRequest(new { message = "Định dạng file không được hỗ trợ (PDF/DOC/DOCX/XLS/XLSX/PNG/JPG)." }) };
        }

        var uploadDir = Path.Combine(env.ContentRootPath, "wwwroot", "files", StorageSubfolder);
        Directory.CreateDirectory(uploadDir);
        var storedName = $"{kindTag}-{Guid.NewGuid():N}{extension}";
        var storedPath = Path.Combine(uploadDir, storedName);
        await using (var stream = new FileStream(storedPath, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }
        return new StoredFile
        {
            Payload = new UploadedPayload(
                $"/files/{StorageSubfolder}/{storedName}",
                file.FileName,
                file.Length,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType),
        };
    }

    private async Task<bool> ContractExistsForCallerAsync(int contractId, int callerUserId, bool canSeeAll, CancellationToken ct)
    {
        var contract = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contractId, ct);
        if (contract == null) return false;
        if (!canSeeAll && contract.OwnerUserId != callerUserId) return false;
        return true;
    }

    private int? GetUserId()
    {
        var principal = HttpContext?.User;
        if (principal == null) return null;
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("uid");
        return int.TryParse(value, out var uid) ? uid : null;
    }
}
