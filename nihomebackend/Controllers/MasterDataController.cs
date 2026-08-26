using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

/// <summary>
/// CRUD endpoints for the generic master-data catalogue. Read routes are
/// available to any authenticated user so any FE dropdown can consume them;
/// write routes require the <c>master-data.manage</c> permission.
/// </summary>
[ApiController]
[Route("api/master-data")]
[Route("api/v1/master-data")]
[Authorize]
public class MasterDataController(IMasterDataService svc, IAuditLogger audit) : ControllerBase
{
    [HttpGet("categories")]
    [RequirePermission("master-data", "view")]
    public async Task<ActionResult<List<MasterDataCategoryResponse>>> GetCategories(CancellationToken ct)
        => Ok(await svc.GetCategoriesAsync(ct));

    [HttpGet("{category}")]
    [RequirePermission("master-data", "view")]
    public async Task<ActionResult<List<MasterDataOptionResponse>>> GetByCategory(
        string category,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
        => Ok(await svc.GetByCategoryAsync(category, includeInactive, ct));

    [HttpGet("options/{id:int}")]
    [RequirePermission("master-data", "view")]
    public async Task<ActionResult<MasterDataOptionResponse>> GetById(int id, CancellationToken ct)
    {
        var found = await svc.GetByIdAsync(id, ct);
        return found == null ? NotFound() : Ok(found);
    }

    [HttpPost("{category}")]
    [RequirePermission("master-data", "manage")]
    public async Task<ActionResult<MasterDataOptionResponse>> Create(
        string category,
        [FromBody] UpsertMasterDataOptionRequest req,
        CancellationToken ct)
    {
        try
        {
            var created = await svc.CreateAsync(category, req, ct);
            audit.Log(new AuditEvent
            {
                Action = "masterData.create",
                ResourceType = EntityTypes.MasterDataOption,
                ResourceId = created.Id.ToString(),
                Message = $"Master-data option {created.Category}/{created.Code} created.",
                NewValue = created,
            });
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (MasterDataDuplicateCodeException ex)
        {
            return Conflict(new { message = ex.Message, category = ex.Category, code = ex.Code });
        }
    }

    [HttpPut("options/{id:int}")]
    [RequirePermission("master-data", "manage")]
    public async Task<ActionResult<MasterDataOptionResponse>> Update(
        int id,
        [FromBody] UpsertMasterDataOptionRequest req,
        CancellationToken ct)
    {
        try
        {
            var previous = await svc.GetByIdAsync(id, ct);
            var updated = await svc.UpdateAsync(id, req, ct);
            if (updated is not null)
            {
                audit.Log(new AuditEvent
                {
                    Action = "masterData.update",
                    ResourceType = EntityTypes.MasterDataOption,
                    ResourceId = id.ToString(),
                    Message = $"Master-data option {updated.Category}/{updated.Code} updated.",
                    OldValue = previous,
                    NewValue = updated,
                });
            }
            return updated == null ? NotFound() : Ok(updated);
        }
        catch (MasterDataDuplicateCodeException ex)
        {
            return Conflict(new { message = ex.Message, category = ex.Category, code = ex.Code });
        }
    }

    [HttpDelete("options/{id:int}")]
    [RequirePermission("master-data", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var previous = await svc.GetByIdAsync(id, ct);
        try
        {
            if (!await svc.DeleteAsync(id, ct)) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "masterData.delete",
                ResourceType = EntityTypes.MasterDataOption,
                ResourceId = id.ToString(),
                Message = $"Master-data option {previous?.Category}/{previous?.Code} deleted.",
                OldValue = previous,
            });
            return NoContent();
        }
        catch (MasterDataInUseException ex)
        {
            return Conflict(new { message = ex.Message, category = ex.Category, code = ex.Code });
        }
    }
}
