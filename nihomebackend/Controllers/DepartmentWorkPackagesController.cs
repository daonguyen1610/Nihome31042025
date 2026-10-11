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
[Route("api/work-packages")]
[Route("api/v1/work-packages")]
[Authorize]
public sealed class DepartmentWorkPackagesController(
    IDepartmentWorkPackageService service,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("work.packages", "view")]
    public async Task<ActionResult<DepartmentWorkPackageListResponse>> List(
        [FromQuery] DepartmentWorkPackageQuery query,
        CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        return scope is null
            ? Unauthorized()
            : Ok(await service.ListAsync(query, scope.Value.UserId, scope.Value.CanViewAll, ct));
    }

    [HttpGet("options")]
    [RequirePermission("work.packages", "view")]
    public async Task<ActionResult<DepartmentWorkPackageOptionsResponse>> Options(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canManage = await permissions.HasAsync(userId.Value, "work.packages.manage", ct);
        var canAssign = await permissions.HasAsync(userId.Value, "work.packages.assign", ct);
        return canManage || canAssign
            ? Ok(await service.OptionsAsync(userId.Value, ct))
            : Forbid();
    }

    [HttpGet("{id:int}")]
    [RequirePermission("work.packages", "view")]
    public async Task<ActionResult<DepartmentWorkPackageResponse>> Get(int id, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        if (scope is null) return Unauthorized();
        var result = await service.GetAsync(id, scope.Value.UserId, scope.Value.CanViewAll, ct);
        if (result is null) return NotFound();
        SetEtag(result.RowVersion);
        return Ok(result);
    }

    [HttpGet("{id:int}/history")]
    [RequirePermission("work.packages", "view")]
    public async Task<ActionResult<IReadOnlyList<DepartmentWorkPackageHistoryResponse>>> History(int id, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        if (scope is null) return Unauthorized();
        var result = await service.HistoryAsync(id, scope.Value.UserId, scope.Value.CanViewAll, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission("work.packages", "manage")]
    [Idempotency("work.packages.create", requireKey: true)]
    public async Task<ActionResult<DepartmentWorkPackageResponse>> Create(
        [FromBody] CreateDepartmentWorkPackageRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (string.Equals(request.Kind, nameof(WorkPackageKind.Internal), StringComparison.OrdinalIgnoreCase) &&
            !await permissions.HasAsync(userId.Value, "work.packages.assign", ct))
            return Forbid();
        return await ExecuteAsync<DepartmentWorkPackageResponse>("work-package.create", null, async () =>
        {
            var result = await service.CreateAsync(request, userId.Value, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        });
    }

    [HttpPut("{id:int}")]
    [RequirePermission("work.packages", "manage")]
    [Idempotency("work.packages.update", requireKey: true)]
    public async Task<ActionResult<DepartmentWorkPackageResponse>> Update(
        int id,
        [FromBody] UpdateDepartmentWorkPackageRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteAsync<DepartmentWorkPackageResponse>("work-package.update", id, async () =>
        {
            var result = await service.UpdateAsync(id, request, userId.Value, ct);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("{id:int}/tasks")]
    [RequirePermission("work.packages", "assign")]
    [Idempotency("work.packages.tasks.create", requireKey: true)]
    public async Task<ActionResult<DepartmentWorkTaskResponse>> AddTask(
        int id,
        [FromBody] UpsertDepartmentWorkTaskRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<DepartmentWorkTaskResponse>("work-task.create", id, async () =>
        {
            var result = await service.AddTaskAsync(id, request, userId.Value, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        });
    }

    [HttpPut("{id:int}/tasks/{taskId:int}")]
    [RequirePermission("work.packages", "assign")]
    [Idempotency("work.packages.tasks.update", requireKey: true)]
    public async Task<ActionResult<DepartmentWorkTaskResponse>> UpdateTask(
        int id,
        int taskId,
        [FromBody] UpsertDepartmentWorkTaskRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteAsync<DepartmentWorkTaskResponse>("work-task.update", id, async () =>
        {
            var result = await service.UpdateTaskAsync(id, taskId, request, userId.Value, ct);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPut("{id:int}/tasks/{taskId:int}/progress")]
    [RequirePermission("work.packages", "progress")]
    [Idempotency("work.packages.tasks.progress", requireKey: true)]
    public async Task<ActionResult<DepartmentWorkTaskResponse>> Progress(
        int id,
        int taskId,
        [FromBody] UpdateDepartmentWorkTaskProgressRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteAsync<DepartmentWorkTaskResponse>("work-task.progress", id, async () =>
        {
            var result = await service.UpdateProgressAsync(id, taskId, request, userId.Value, ct);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("{id:int}/tasks/{taskId:int}/verify")]
    [RequirePermission("work.packages", "verify")]
    [Idempotency("work.packages.tasks.verify", requireKey: true)]
    public async Task<ActionResult<DepartmentWorkTaskResponse>> Verify(
        int id,
        int taskId,
        [FromBody] VerifyDepartmentWorkTaskRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteAsync<DepartmentWorkTaskResponse>("work-task.verify", id, async () =>
        {
            var result = await service.VerifyTaskAsync(id, taskId, request, userId.Value, ct);
            return result is null ? NotFound() : Ok(result);
        });
    }

    [HttpPost("{id:int}/input-price-offers")]
    [RequirePermission("work.packages", "progress")]
    [Idempotency("work.packages.input-prices.create", requireKey: true)]
    public async Task<ActionResult<InputPriceOfferResponse>> AddInputPriceOffer(
        int id, [FromBody] UpsertInputPriceOfferRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteBusinessAsync<InputPriceOfferResponse>(async () =>
        {
            var result = await service.AddInputPriceOfferAsync(id, request, userId.Value, ct);
            SetEtag(result.RowVersion);
            return StatusCode(StatusCodes.Status201Created, result);
        });
    }

    [HttpPut("{id:int}/input-price-offers/{offerId:int}")]
    [RequirePermission("work.packages", "progress")]
    [Idempotency("work.packages.input-prices.update", requireKey: true)]
    public async Task<ActionResult<InputPriceOfferResponse>> UpdateInputPriceOffer(
        int id, int offerId, [FromBody] UpsertInputPriceOfferRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteBusinessAsync<InputPriceOfferResponse>(async () =>
        {
            var result = await service.UpdateInputPriceOfferAsync(id, offerId, request, userId.Value, ct);
            if (result is null) return NotFound();
            SetEtag(result.RowVersion);
            return Ok(result);
        });
    }

    [HttpPost("{id:int}/input-price-offers/{offerId:int}/submit")]
    [RequirePermission("work.packages", "progress")]
    [Idempotency("work.packages.input-prices.submit", requireKey: true)]
    public async Task<ActionResult<InputPriceOfferResponse>> SubmitInputPriceOffer(
        int id, int offerId, [FromBody] SubmitBusinessRecordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteBusinessAsync<InputPriceOfferResponse>(async () =>
        {
            var result = await service.SubmitInputPriceOfferAsync(id, offerId, request, userId.Value, ct);
            if (result is null) return NotFound();
            SetEtag(result.RowVersion);
            return Ok(result);
        });
    }

    [HttpPost("{id:int}/input-price-offers/{offerId:int}/decision")]
    [RequirePermission("work.packages", "verify")]
    [Idempotency("work.packages.input-prices.decision", requireKey: true)]
    public async Task<ActionResult<InputPriceOfferResponse>> DecideInputPriceOffer(
        int id, int offerId, [FromBody] DecideBusinessRecordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteBusinessAsync<InputPriceOfferResponse>(async () =>
        {
            var result = await service.DecideInputPriceOfferAsync(id, offerId, request, userId.Value, ct);
            if (result is null) return NotFound();
            SetEtag(result.RowVersion);
            return Ok(result);
        });
    }

    [HttpPut("{id:int}/finance-review")]
    [RequirePermission("work.packages", "progress")]
    [Idempotency("work.packages.finance-review.save", requireKey: true)]
    public async Task<ActionResult<FinanceReviewResponse>> SaveFinanceReview(
        int id, [FromBody] UpsertFinanceReviewRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!string.IsNullOrWhiteSpace(request.RowVersion))
            request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteBusinessAsync<FinanceReviewResponse>(async () =>
        {
            var result = await service.SaveFinanceReviewAsync(id, request, userId.Value, ct);
            SetEtag(result.RowVersion);
            return Ok(result);
        });
    }

    [HttpPost("{id:int}/finance-review/submit")]
    [RequirePermission("work.packages", "progress")]
    [Idempotency("work.packages.finance-review.submit", requireKey: true)]
    public async Task<ActionResult<FinanceReviewResponse>> SubmitFinanceReview(
        int id, [FromBody] SubmitBusinessRecordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteBusinessAsync<FinanceReviewResponse>(async () =>
        {
            var result = await service.SubmitFinanceReviewAsync(id, request, userId.Value, ct);
            if (result is null) return NotFound();
            SetEtag(result.RowVersion);
            return Ok(result);
        });
    }

    [HttpPost("{id:int}/finance-review/decision")]
    [RequirePermission("work.packages", "verify")]
    [Idempotency("work.packages.finance-review.decision", requireKey: true)]
    public async Task<ActionResult<FinanceReviewResponse>> DecideFinanceReview(
        int id, [FromBody] DecideBusinessRecordRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return await ExecuteBusinessAsync<FinanceReviewResponse>(async () =>
        {
            var result = await service.DecideFinanceReviewAsync(id, request, userId.Value, ct);
            if (result is null) return NotFound();
            SetEtag(result.RowVersion);
            return Ok(result);
        });
    }

    private static async Task<ActionResult<T>> ExecuteBusinessAsync<T>(Func<Task<ActionResult<T>>> operation)
    {
        try { return await operation(); }
        catch (DepartmentWorkPackageScopeException) { return new NotFoundResult(); }
        catch (DepartmentWorkPackageOperationException exception)
        {
            return new BadRequestObjectResult(new { message = exception.Message });
        }
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(
        string action,
        int? packageId,
        Func<Task<ActionResult<T>>> operation)
    {
        try
        {
            var result = await operation();
            if (result.Result is ObjectResult { Value: T value })
            {
                var rowVersion = value switch
                {
                    DepartmentWorkPackageResponse package => package.RowVersion,
                    DepartmentWorkTaskResponse task => task.RowVersion,
                    _ => string.Empty,
                };
                SetEtag(rowVersion);
                audit.Log(new AuditEvent
                {
                    Action = action,
                    ResourceType = action.StartsWith("work-task", StringComparison.Ordinal)
                        ? EntityTypes.DepartmentWorkTask
                        : EntityTypes.DepartmentWorkPackage,
                    ResourceId = packageId?.ToString() ?? "new",
                    Message = $"{action} completed for work package #{packageId?.ToString() ?? "new"}.",
                    NewValue = value,
                });
            }
            return result;
        }
        catch (DepartmentWorkPackageScopeException)
        {
            return NotFound();
        }
        catch (DepartmentWorkPackageOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private async Task<(int UserId, bool CanViewAll)?> ScopeAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        return userId is null
            ? null
            : (userId.Value, await permissions.HasAsync(userId.Value, "work.packages.view.all", ct));
    }

    private void SetEtag(string rowVersion)
    {
        if (!string.IsNullOrWhiteSpace(rowVersion)) CrmConcurrency.SetResponseEntityTag(Response, rowVersion);
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(value, out var id) ? id : null;
    }
}
