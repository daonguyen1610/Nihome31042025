using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize]
[Route("api/procurement/vendors")]
[Route("api/v1/procurement/vendors")]
public class VendorsController(
    IVendorService service,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("procurement.vendors", "view")]
    public async Task<ActionResult<VendorListResponse>> List(
        [FromQuery] string? search, [FromQuery] VendorType? type, [FromQuery] bool? isActive,
        [FromQuery] int? ownerUserId, [FromQuery] string? serviceGroupCode,
        [FromQuery] string? sortBy = "companyName", [FromQuery] string? sortDirection = "asc",
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        return Ok(await service.ListAsync(context.Value.UserId, context.Value.CanSeeAll, search, type, isActive,
            ownerUserId, serviceGroupCode, sortBy, sortDirection, page, pageSize, ct));
    }

    [HttpGet("export")]
    [RequirePermission("procurement.vendors", "view")]
    [RequirePermission("procurement.vendors", "export")]
    public async Task<ActionResult<List<VendorResponse>>> Export(
        [FromQuery] string? search, [FromQuery] VendorType? type, [FromQuery] bool? isActive,
        [FromQuery] int? ownerUserId, [FromQuery] string? serviceGroupCode,
        [FromQuery] string? sortBy = "companyName", [FromQuery] string? sortDirection = "asc",
        CancellationToken ct = default)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        var rows = await service.ExportAsync(context.Value.UserId, context.Value.CanSeeAll, search, type, isActive,
            ownerUserId, serviceGroupCode, sortBy, sortDirection, ct);
        audit.Log("vendor.export", EntityTypes.Vendor, null, $"Exported {rows.Count} filtered vendors.");
        return Ok(rows);
    }

    [HttpGet("owner-options")]
    [RequirePermission("procurement.vendors", "view")]
    [RequirePermission("procurement.vendors", "view.all")]
    public async Task<ActionResult<List<VendorOwnerOptionResponse>>> OwnerOptions(CancellationToken ct) =>
        Ok(await service.GetOwnerOptionsAsync(ct));

    [HttpGet("project-options")]
    [RequirePermission("procurement.vendors", "view")]
    [RequirePermission("procurement.vendors", "evaluate")]
    public async Task<ActionResult<List<VendorProjectOptionResponse>>> ProjectOptions(CancellationToken ct) =>
        Ok(await service.GetProjectOptionsAsync(ct));

    [HttpGet("{id:int}")]
    [RequirePermission("procurement.vendors", "view")]
    public async Task<ActionResult<VendorResponse>> Get(int id, CancellationToken ct = default)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        var vendor = await service.GetAsync(id, context.Value.UserId, context.Value.CanSeeAll, ct);
        return vendor is null ? NotFound() : Ok(vendor);
    }

    [HttpGet("{id:int}/history")]
    [RequirePermission("procurement.vendors", "view")]
    public async Task<ActionResult<List<VendorAuditResponse>>> History(int id, CancellationToken ct = default)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        var history = await service.GetHistoryAsync(id, context.Value.UserId, context.Value.CanSeeAll, ct);
        return history is null ? NotFound() : Ok(history);
    }

    [HttpPost]
    [RequirePermission("procurement.vendors", "view")]
    [RequirePermission("procurement.vendors", "manage")]
    public async Task<ActionResult<VendorResponse>> Create([FromBody] CreateVendorRequest request, CancellationToken ct)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        try
        {
            var vendor = await service.CreateAsync(request, context.Value.UserId, context.Value.CanSeeAll, ct);
            LogSuccess("vendor.create", vendor.Id, $"Vendor {vendor.VendorCode} created.", vendor);
            return CreatedAtAction(nameof(Get), new { id = vendor.Id }, vendor);
        }
        catch (VendorOperationException ex) { return OperationFailure("vendor.create", null, ex); }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("procurement.vendors", "view")]
    [RequirePermission("procurement.vendors", "manage")]
    public async Task<ActionResult<VendorResponse>> Update(int id, [FromBody] UpdateVendorRequest request, CancellationToken ct)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        try
        {
            var previous = await service.GetAsync(id, context.Value.UserId, context.Value.CanSeeAll, ct);
            if (previous is null) return NotFound();
            var vendor = await service.UpdateAsync(id, request, context.Value.UserId, context.Value.CanSeeAll, ct);
            if (vendor is null) return NotFound();
            LogSuccess("vendor.update", id, $"Vendor {vendor.VendorCode} updated; active={vendor.IsActive}.", vendor, previous);
            return Ok(vendor);
        }
        catch (VendorOperationException ex) { return OperationFailure("vendor.update", id, ex); }
    }

    [HttpPost("{id:int}/documents")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = VendorService.MaxDocumentRequestSizeBytes)]
    [RequestSizeLimit(VendorService.MaxDocumentRequestSizeBytes)]
    [RequirePermission("procurement.vendors", "view")]
    [RequirePermission("procurement.vendors", "manage")]
    public async Task<ActionResult<VendorDocumentResponse>> UploadDocument(
        int id, [FromForm] VendorDocumentType documentType, [FromForm] IFormFile file, CancellationToken ct)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        try
        {
            VendorService.ValidateDocument(file);
            var document = await service.UploadDocumentAsync(id, documentType, file, context.Value.UserId, context.Value.CanSeeAll, ct);
            if (document is null) return NotFound();
            LogSuccess("vendor.document.upload", id, $"Document #{document.Id} uploaded for vendor #{id}.", document);
            return CreatedAtAction(nameof(DownloadDocument), new { id, documentId = document.Id }, document);
        }
        catch (VendorOperationException ex) { return OperationFailure("vendor.document.upload", id, ex); }
    }

    [HttpGet("{id:int}/documents/{documentId:int}/download")]
    [RequirePermission("procurement.vendors", "view")]
    public async Task<IActionResult> DownloadDocument(int id, int documentId, CancellationToken ct)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        try
        {
            var document = await service.DownloadDocumentAsync(id, documentId, context.Value.UserId, context.Value.CanSeeAll, ct);
            if (document is null) return NotFound();
            LogSuccess("vendor.document.download", id, $"Document #{documentId} downloaded for vendor #{id}.");
            return File(document.Content, document.ContentType, document.FileName, enableRangeProcessing: true);
        }
        catch (VendorDocumentMissingException ex)
        {
            LogFailure("vendor.document.download", id, ex.Message);
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}/documents/{documentId:int}")]
    [RequirePermission("procurement.vendors", "view")]
    [RequirePermission("procurement.vendors", "manage")]
    public async Task<IActionResult> DeleteDocument(int id, int documentId, CancellationToken ct)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        var vendor = await service.GetAsync(id, context.Value.UserId, context.Value.CanSeeAll, ct);
        if (vendor is null) return NotFound();
        var previous = vendor.Documents.FirstOrDefault(document => document.Id == documentId);
        if (previous is null) return NotFound();
        var removed = await service.DeleteDocumentAsync(id, documentId, context.Value.UserId, context.Value.CanSeeAll, ct);
        if (!removed) return NotFound();
        LogSuccess("vendor.document.delete", id, $"Document #{documentId} deleted from vendor #{id}.", oldValue: previous);
        return NoContent();
    }

    [HttpPost("{id:int}/evaluations")]
    [RequirePermission("procurement.vendors", "view")]
    [RequirePermission("procurement.vendors", "evaluate")]
    public async Task<ActionResult<VendorEvaluationResponse>> CreateEvaluation(
        int id, [FromBody] UpsertVendorEvaluationRequest request, CancellationToken ct)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        try
        {
            var evaluation = await service.CreateEvaluationAsync(id, request, context.Value.UserId, context.Value.CanSeeAll, ct);
            if (evaluation is null) return NotFound();
            LogSuccess("vendor.evaluation.create", id, $"Evaluation #{evaluation.Id} created for vendor #{id}.", evaluation);
            return CreatedAtAction(nameof(Get), new { id }, evaluation);
        }
        catch (VendorOperationException ex) { return OperationFailure("vendor.evaluation.create", id, ex); }
    }

    [HttpPut("{id:int}/evaluations/{evaluationId:int}")]
    [RequirePermission("procurement.vendors", "view")]
    [RequirePermission("procurement.vendors", "evaluate")]
    public async Task<ActionResult<VendorEvaluationResponse>> UpdateEvaluation(
        int id, int evaluationId, [FromBody] UpsertVendorEvaluationRequest request, CancellationToken ct)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        try
        {
            var vendor = await service.GetAsync(id, context.Value.UserId, context.Value.CanSeeAll, ct);
            if (vendor is null) return NotFound();
            var previous = vendor.Evaluations.FirstOrDefault(evaluation => evaluation.Id == evaluationId);
            if (previous is null) return NotFound();
            var evaluation = await service.UpdateEvaluationAsync(id, evaluationId, request, context.Value.UserId, context.Value.CanSeeAll, ct);
            if (evaluation is null) return NotFound();
            LogSuccess("vendor.evaluation.update", id, $"Evaluation #{evaluationId} updated for vendor #{id}.", evaluation, previous);
            return Ok(evaluation);
        }
        catch (VendorOperationException ex) { return OperationFailure("vendor.evaluation.update", id, ex); }
    }

    [HttpDelete("{id:int}/evaluations/{evaluationId:int}")]
    [RequirePermission("procurement.vendors", "view")]
    [RequirePermission("procurement.vendors", "evaluate")]
    public async Task<IActionResult> DeleteEvaluation(int id, int evaluationId, CancellationToken ct)
    {
        var context = await GetScopeAsync(ct);
        if (context is null) return Unauthorized();
        var vendor = await service.GetAsync(id, context.Value.UserId, context.Value.CanSeeAll, ct);
        if (vendor is null) return NotFound();
        var previous = vendor.Evaluations.FirstOrDefault(evaluation => evaluation.Id == evaluationId);
        if (previous is null) return NotFound();
        var removed = await service.DeleteEvaluationAsync(id, evaluationId, context.Value.UserId, context.Value.CanSeeAll, ct);
        if (!removed) return NotFound();
        LogSuccess("vendor.evaluation.delete", id, $"Evaluation #{evaluationId} deleted from vendor #{id}.", oldValue: previous);
        return NoContent();
    }

    private async Task<(int UserId, bool CanSeeAll)?> GetScopeAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return null;
        return (userId.Value, await permissions.HasAsync(userId.Value, "procurement.vendors.view.all", ct));
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(value, out var userId) ? userId : null;
    }

    private void LogSuccess(
        string action, int? vendorId, string message, object? value = null, object? oldValue = null) => audit.Log(new AuditEvent
        {
            Action = action,
            ResourceType = EntityTypes.Vendor,
            ResourceId = vendorId?.ToString(),
            Message = message,
            OldValue = oldValue,
            NewValue = value,
        });

    private void LogFailure(string action, int? vendorId, string reason) => audit.Log(new AuditEvent
    {
        Action = action,
        ResourceType = EntityTypes.Vendor,
        ResourceId = vendorId?.ToString(),
        Message = reason,
        Status = AuditStatus.Failure,
        FailureReason = reason,
    });

    private ActionResult OperationFailure(string action, int? vendorId, VendorOperationException exception)
    {
        LogFailure(action, vendorId, exception.Message);
        return BadRequest(new { message = exception.Message });
    }
}
