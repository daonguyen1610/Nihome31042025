using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/business-documents")]
[Route("api/v1/business-documents")]
[Authorize]
public class BusinessDocumentsController(IBusinessDocumentStorageService storage) : ControllerBase
{
    [HttpPost("vendors")]
    [RequirePermission("proc.vendors", "manage")]
    public Task<ActionResult<BusinessDocumentUploadResponse>> UploadVendor(
        [FromForm] IFormFile? file,
        CancellationToken ct) => Upload(file, BusinessDocumentArea.Vendors, ct);

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
}