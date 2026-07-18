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

/// <summary>
/// CRM Contract endpoints — NIH-102 covers list + minimal CRUD. Payment
/// milestones, VOs and the detail-tab page are follow-up stories.
///
/// * Sales users (<c>crm.contracts.view</c>) see only rows they own.
/// * Sales Manager / Legal / BOD / Admin gain <c>view.all</c> via the
///   RBAC bundle (through <c>crm.**</c>, <c>**.view</c>, etc.).
/// </summary>
[ApiController]
[Route("api/contracts")]
[Route("api/v1/contracts")]
[Authorize]
public class ContractsController(
    IContractService svc,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
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

        try
        {
            var created = await svc.CreateAsync(req, userId.Value, ct);
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
        try
        {
            var updated = await svc.UpdateAsync(id, req, userId.Value, canSeeAll, ct);
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

    private int? GetUserId()
    {
        var principal = HttpContext?.User;
        if (principal == null) return null;
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("uid");
        return int.TryParse(value, out var uid) ? uid : null;
    }
}
