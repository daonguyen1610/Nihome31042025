using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/vendor-rfqs")]
[Route("api/v1/vendor-rfqs")]
public sealed class VendorRfqPortalController(RfqService service, IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var token = Request.Headers["X-RFQ-Portal-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        var result = await service.GetPortalAsync(token, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("bids")]
    [Idempotency("proc.rfqs.portal-bid", requireKey: true)]
    public async Task<IActionResult> Submit(VendorPortalBidRequest request, CancellationToken ct)
    {
        var token = Request.Headers["X-RFQ-Portal-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        try
        {
            var result = await service.SubmitPortalBidAsync(token, request, ct);
            if (result is null) return NotFound();
            audit.Log("proc.rfqs.portal-bid-submitted", "Rfq", result.Code,
                "A vendor submitted a quotation through an expiring portal invitation.");
            return Ok(result);
        }
        catch (ProcurementOperationException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (CrmConcurrencyException)
        {
            return Conflict(new { message = "The RFQ changed. Reload the secure link and submit again." });
        }
    }
}