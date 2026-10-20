using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed class ContractCareIdempotencyGuard(
    IContractService contracts, IPermissionService permissions) : IIdempotencyRequestGuard
{
    public async Task<IActionResult?> ValidateAsync(HttpContext context, CancellationToken ct = default)
    {
        var rawUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        if (!int.TryParse(rawUserId, out var userId)) return new UnauthorizedResult();
        if (!int.TryParse(context.Request.RouteValues["id"]?.ToString(), out var contractId)) return new NotFoundResult();
        var contract = await contracts.GetAsync(contractId, userId,
            await permissions.HasAsync(userId, "crm.contracts.view.all", ct), ct);
        if (contract is null) return new NotFoundResult();
        return contract.Direction == ContractDirection.Upstream ? null
            : new BadRequestObjectResult(new { message = "Customer care requires an Upstream contract." });
    }
}
