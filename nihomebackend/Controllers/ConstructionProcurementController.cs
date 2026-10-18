using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController, Authorize]
[Route("api/operational-projects/{projectId:int}/procurement/construction-resources")]
[Route("api/v1/operational-projects/{projectId:int}/procurement/construction-resources")]
public sealed class ConstructionProcurementController(ConstructionProcurementService service, IProjectAccessService access, IContractService contracts, IAuditLogger audit) : ControllerBase
{
    [HttpGet, RequirePermission("proc.resource-requests", "view")]
    public Task<IActionResult> List(int projectId, [FromQuery] ConstructionResourceType? type, [FromQuery] string? state, CancellationToken ct) =>
        Run(projectId, _ => service.ListAsync(projectId, type, state, ct));

    [HttpGet("contracts"), RequirePermission("proc.resource-requests", "view")]
    public Task<IActionResult> Contracts(int projectId, CancellationToken ct) => Run(projectId, _ => service.ContractsAsync(projectId, ct));

    [HttpGet("documents"), RequirePermission("proc.resource-requests", "manage")]
    public Task<IActionResult> Documents(int projectId, CancellationToken ct) => Run(projectId, _ => service.DocumentsAsync(projectId, ct));

    [HttpPost("contracts"), RequirePermission("proc.resource-requests", "manage"), Idempotency("construction.procurement.contract.create", requireKey: true)]
    public Task<IActionResult> CreateContract(int projectId, CreateConstructionProcurementContractRequest request, CancellationToken ct) =>
        Run(projectId, async uid =>
        {
            var created = await service.CreateContractAsync(projectId, request, uid, contracts, ct);
            audit.Log(new AuditEvent
            {
                Action = "contract.create",
                ResourceType = EntityTypes.Contract,
                ResourceId = created.Id.ToString(),
                Message = $"Downstream contract {created.ContractNumber} drafted from approved construction resources.",
                NewValue = created
            });
            CrmConcurrency.SetResponseEntityTag(Response, created.RowVersion);
            return created;
        });

    [HttpPost("{id:int}/contract"), RequirePermission("proc.resource-requests", "manage"), Idempotency("construction.procurement.contract", requireKey: true)]
    public Task<IActionResult> Link(int projectId, int id, LinkConstructionContractRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return Run(projectId, uid => service.LinkAsync(projectId, id, request, uid, ct));
    }

    [HttpPost("{id:int}/supply"), RequirePermission("proc.resource-requests", "manage"), Idempotency("construction.procurement.supply", requireKey: true)]
    public Task<IActionResult> Supply(int projectId, int id, ConfirmConstructionSupplyRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return Run(projectId, uid => service.ConfirmAsync(projectId, id, request, uid, ct));
    }

    private async Task<IActionResult> Run<T>(int projectId, Func<int, Task<T>> action)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(uid, projectId, HttpContext.RequestAborted)) return NotFound();
        try { var result = await action(uid); return result is null ? NotFound() : Ok(result); }
        catch (ConstructionOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ContractValidationException ex) { return BadRequest(new { code = ex.Code, message = ex.Message }); }
        catch (ContractDuplicateNumberException ex) { return Conflict(new { message = ex.Message }); }
    }
}
