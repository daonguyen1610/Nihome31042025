using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Services.HardDelete;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/hard-delete-operations")]
[Route("api/v1/hard-delete-operations")]
[Authorize]
public sealed class HardDeleteOperationsController(
    IHardDeleteOperationService operations) : ControllerBase
{
    [HttpGet("{operationId:guid}")]
    public async Task<ActionResult<HardDeleteOperationResult>> GetStatus(
        Guid operationId,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var result = await operations.GetAsync(operationId, ct, userId.Value.ToString());
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{operationId:guid}/retry")]
    public async Task<ActionResult<HardDeleteOperationResult>> Retry(
        Guid operationId,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var requestedBy = userId.Value.ToString();
        if (await operations.GetAsync(operationId, ct, requestedBy) is null) return NotFound();

        try
        {
            var result = await operations.ProcessAsync(operationId, ct, requestedBy);
            return result.IsComplete ? Ok(result) : Accepted(result);
        }
        catch (HardDeleteOperationConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (HardDeleteOperationException exception) when (exception.Code == "operation_not_found")
        {
            return NotFound();
        }
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(value, out var id) ? id : null;
    }
}