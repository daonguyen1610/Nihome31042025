using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/business-documents")]
[Route("api/v1/business-documents")]
[Authorize]
public class BusinessDocumentsController(
    IBusinessDocumentStorageService storage,
    AppDbContext db,
    IAuditLogger audit) : ControllerBase
{
    [HttpPost("vendors")]
    [RequirePermission("proc.vendors", "manage")]
    public Task<ActionResult<BusinessDocumentUploadResponse>> UploadVendor(
        [FromForm] IFormFile? file,
        CancellationToken ct) => UploadVendorDocument(file, ct);

    [HttpDelete("vendors")]
    [RequirePermission("proc.vendors", "manage")]
    public async Task<IActionResult> DiscardVendor(
        [FromBody] DiscardVendorDocumentRequest request,
        CancellationToken ct)
    {
        if (!request.ClaimToken.HasValue) return BadRequest(new { message = "The upload claim token is required." });
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        var upload = await db.VendorDocumentUploads.SingleOrDefaultAsync(item => item.Token == request.ClaimToken.Value, ct);
        if (upload is null) return NoContent();
        if (upload.VendorId.HasValue)
            return Conflict(new { message = "The document is already referenced by a vendor and cannot be discarded." });
        db.VendorDocumentUploads.Remove(upload);
        await db.SaveChangesAsync(ct);
        if (!storage.Delete(upload.Path, BusinessDocumentArea.Vendors))
            throw new BusinessDocumentStorageException("The Vendor document could not be deleted; retry the discard operation.");
        if (transaction is not null) await transaction.CommitAsync(ct);
        audit.Log(new AuditEvent
        {
            Action = "vendor-document.discard",
            ResourceType = EntityTypes.Vendor,
            ResourceId = upload.Path,
            Message = "Unreferenced vendor capability document discarded.",
        });
        return NoContent();
    }

    [HttpPost("acceptance")]
    [RequirePermission("construction.acceptance", "manage")]
    public Task<ActionResult<BusinessDocumentUploadResponse>> UploadAcceptance(
        [FromForm] IFormFile? file,
        CancellationToken ct) => Upload(file, BusinessDocumentArea.Acceptance, ct);

    [HttpPost("as-built")]
    [RequirePermission("construction.asbuilt", "manage")]
    public Task<ActionResult<BusinessDocumentUploadResponse>> UploadAsBuilt(
        [FromForm] IFormFile? file,
        CancellationToken ct) => Upload(file, BusinessDocumentArea.AsBuilt, ct);

    [HttpPost("handover")]
    [RequirePermission("construction.handover", "manage")]
    public Task<ActionResult<BusinessDocumentUploadResponse>> UploadHandover(
        [FromForm] IFormFile? file,
        CancellationToken ct) => Upload(file, BusinessDocumentArea.Handover, ct);

    private async Task<ActionResult<BusinessDocumentUploadResponse>> Upload(
        IFormFile? file,
        BusinessDocumentArea area,
        CancellationToken ct)
    {
        try
        {
            return Ok(await storage.StoreAsync(file, area, ct));
        }
        catch (BusinessDocumentStorageException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<ActionResult<BusinessDocumentUploadResponse>> UploadVendorDocument(
        IFormFile? file, CancellationToken ct)
    {
        BusinessDocumentUploadResponse? result = null;
        try
        {
            result = await storage.StoreAsync(file, BusinessDocumentArea.Vendors, ct);
            var userId = GetUserId();
            if (userId is null)
            {
                storage.Delete(result.Path, BusinessDocumentArea.Vendors);
                return Unauthorized();
            }
            var upload = new VendorDocumentUpload
            {
                Path = result.Path,
                CreatedByUserId = userId.Value,
            };
            db.VendorDocumentUploads.Add(upload);
            await db.SaveChangesAsync(ct);
            result.ClaimToken = upload.Token;
            audit.Log(new AuditEvent
            {
                Action = "vendor-document.upload",
                ResourceType = EntityTypes.Vendor,
                ResourceId = result.Path,
                Message = "Vendor capability document uploaded pending assignment.",
            });
            return Ok(result);
        }
        catch (BusinessDocumentStorageException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch
        {
            if (result is not null) storage.Delete(result.Path, BusinessDocumentArea.Vendors);
            throw;
        }
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(value, out var userId) ? userId : null;
    }

}