using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
/// M3 permitting workspace endpoints. Permit records are created explicitly
/// together with their Legal work tasks through the project setup workflow.
/// </summary>
[ApiController]
[Route("api/permits")]
[Route("api/v1/permits")]
[Authorize]
public class PermitsController(
    IPermitChecklistService svc,
    IProjectAccessService projectAccess,
    AppDbContext db,
    IPermissionService permissions,
    IBusinessDocumentStorageService documentStorage,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("permit.checklists", "view")]
    public async Task<ActionResult<PermitChecklistListResponse>> List(
        [FromQuery] PermitChecklistListParams parameters, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var accessibleIds = await projectAccess.GetAccessibleOperationalProjectIdsAsync(userId.Value, ct);
        if (parameters.OperationalProjectId.HasValue && !accessibleIds.Contains(parameters.OperationalProjectId.Value))
            return NotFound();
        var result = await svc.ListAsync(parameters, ct, accessibleIds);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("permit.checklists", "view")]
    public async Task<ActionResult<PermitChecklistItemResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue || !await CanAccessResourceAsync(userId.Value, id, ct)) return NotFound();
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpGet("{id:int}/history")]
    [RequirePermission("permit.checklists", "view")]
    public async Task<ActionResult<IReadOnlyList<PermitChecklistHistoryResponse>>> History(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue || !await CanAccessResourceAsync(userId.Value, id, ct)) return NotFound();
        return Ok(await svc.HistoryAsync(id, ct));
    }

    [HttpGet("{id:int}/documents/{fileName}/content")]
    [RequirePermission("permit.checklists", "view")]
    public async Task<IActionResult> GetDocumentContent(int id, string fileName, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue || !await CanAccessResourceAsync(userId.Value, id, ct)) return NotFound();
        var permit = await svc.GetAsync(id, ct);
        if (permit is null || !new[] { permit.SubmittedFilePath, permit.IssuedFilePath }.Any(path => string.Equals(
            path,
            $"/files/business-documents/permits/{fileName}",
            StringComparison.Ordinal))) return NotFound();
        var content = documentStorage.GetContent(BusinessDocumentArea.Permits, fileName);
        return content is null
            ? NotFound()
            : PhysicalFile(content.FullPath, content.ContentType, content.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpPost]
    [RequirePermission("permit.checklists", "manage")]
    public async Task<ActionResult<PermitChecklistItemResponse>> Create(
        [FromBody] CreatePermitChecklistItemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await projectAccess.CanViewOperationalProjectAsync(userId.Value, request.OperationalProjectId, ct)) return NotFound();
        return StatusCode(StatusCodes.Status410Gone, new
        {
            code = "LEGAL_SETUP_REQUIRED",
            message = "Giấy phép và đầu việc phải được tạo cùng nhau qua bước Thiết lập Pháp lý.",
        });
    }

    [HttpPatch("{id:int}")]
    [RequirePermission("permit.checklists", "view")]
    public async Task<ActionResult<PermitChecklistItemResponse>> Update(
        int id, [FromBody] UpdatePermitChecklistItemRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await CanOperateResourceAsync(userId.Value, id, ct)) return Forbid();
        if (string.Equals(request.Status, nameof(PermitStatus.Issued), StringComparison.OrdinalIgnoreCase) &&
            !await permissions.HasAsync(userId.Value, "permit.checklists.mark-issued", ct))
            return Forbid();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "permit.update",
                ResourceType = EntityTypes.PermitChecklistItem,
                ResourceId = id.ToString(),
                Message = $"Permit checklist item #{id} updated ({response.PermitTypeCode} → {response.Status}).",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (PermitChecklistOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "permit.update",
                ResourceType = EntityTypes.PermitChecklistItem,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/documents/{kind}")]
    [RequirePermission("permit.checklists", "view")]
    public async Task<ActionResult<PermitChecklistItemResponse>> UploadDocument(
        int id,
        PermitDocumentKind kind,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await CanOperateResourceAsync(userId.Value, id, ct)) return Forbid();
        if (kind == PermitDocumentKind.IssuedPermit &&
            !await permissions.HasAsync(userId.Value, "permit.checklists.mark-issued", ct))
            return Forbid();
        try
        {
            var response = await svc.UploadDocumentAsync(id, kind, file, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "permit.upload-document",
                ResourceType = EntityTypes.PermitChecklistItem,
                ResourceId = id.ToString(),
                Message = $"Permit checklist item #{id} {kind} document uploaded.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (BusinessDocumentStorageException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "permit.upload-document",
                ResourceType = EntityTypes.PermitChecklistItem,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retained only as a compatibility endpoint for older clients. Automatic
    /// checklist generation is retired; callers must use Legal setup instead.
    /// </summary>
    [HttpPost("design-project/{projectId:int}/ensure")]
    [RequirePermission("permit.checklists", "manage")]
    public async Task<ActionResult<PermitChecklistListResponse>> Ensure(int projectId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await projectAccess.CanViewDesignProjectAsync(userId.Value, projectId, ct)) return NotFound();
        return StatusCode(StatusCodes.Status410Gone, new
        {
            code = "LEGAL_SETUP_REQUIRED",
            message = "Tạo tự động checklist đã ngừng hỗ trợ. Hãy dùng bước Thiết lập Pháp lý của Dự án.",
        });
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("permit.checklists", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (!userId.HasValue || !await CanAccessResourceAsync(userId.Value, id, ct)) return NotFound();
        return StatusCode(StatusCodes.Status409Conflict, new
        {
            code = "LEGAL_PERMIT_DELETE_BLOCKED",
            message = "Không thể xóa riêng giấy phép đã thuộc gói Pháp lý. Hãy điều chỉnh cấu hình gói hoặc dùng quy trình xóa tổng hợp có kiểm tra ảnh hưởng.",
        });
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private async Task<bool> CanAccessResourceAsync(int userId, int resourceId, CancellationToken ct)
    {
        var projectId = await db.PermitChecklistItems.AsNoTracking()
            .Where(item => item.Id == resourceId)
            .Select(item => (int?)item.OperationalProjectId)
            .SingleOrDefaultAsync(ct);
        return projectId.HasValue &&
            await projectAccess.CanViewOperationalProjectAsync(userId, projectId.Value, ct);
    }

    private async Task<bool> CanOperateResourceAsync(int userId, int resourceId, CancellationToken ct)
    {
        if (await db.Users.AsNoTracking().AnyAsync(item => item.Id == userId && item.IsActive &&
                (item.Role == UserRole.ADMIN || item.Role == UserRole.SUPER_ADMIN ||
                 item.RoleEntity != null && item.RoleEntity.IsActive && item.RoleEntity.IsSystem &&
                 (item.RoleEntity.Code == "ADMIN" || item.RoleEntity.Code == "SUPER_ADMIN")), ct))
            return true;
        return await db.PermitChecklistItems.AsNoTracking().AnyAsync(item => item.Id == resourceId &&
            (item.OperationalProject.ProjectManagerUserId == userId ||
             item.WorkTask != null && (item.WorkTask.AssigneeUserId == userId ||
                 item.WorkTask.WorkPackage.LeadUserId == userId)), ct);
    }
}
