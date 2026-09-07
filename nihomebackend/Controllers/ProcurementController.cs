using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/operational-projects/{projectId:int}/procurement")]
[Route("api/v1/operational-projects/{projectId:int}/procurement")]
[Authorize]
public sealed class ProcurementController(
    IProcurementService service,
    IProjectAccessService access,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet("boq-revisions")]
    [RequirePermission("proc.boq", "view")]
    public async Task<ActionResult<ProjectBoqRevisionListResponse>> ListBoqRevisions(
        int projectId,
        [FromQuery] ProjectBoqRevisionListQuery query,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        return Ok(await service.ListBoqRevisionsAsync(projectId, query, ct));
    }

    [HttpGet("boq-revisions/export")]
    [RequirePermission("proc.boq", "view")]
    public async Task<IActionResult> ExportBoqRevisions(
        int projectId,
        [FromQuery] ProjectBoqRevisionListQuery query,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        var rows = await service.ExportBoqRevisionsAsync(projectId, query, ct);
        var csv = new StringBuilder("Revision,Status,Currency,LineCount,ItemCodes,BudgetTotal,PreparedBy,SubmittedAt,ApprovedAt,RejectedAt,IsFinal,CreatedAt,UpdatedAt\r\n");
        foreach (var row in rows)
        {
            csv.Append(row.RevisionNumber).Append(',')
                .Append(Csv(row.Status.ToString())).Append(',')
                .Append(Csv(row.Currency)).Append(',')
                .Append(row.LineCount).Append(',')
                .Append(Csv(string.Join("; ", row.ItemCodes))).Append(',')
                .Append(row.CostTotal.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(row.PreparedByName)).Append(',')
                .Append(Csv(row.SubmittedAt?.ToString("O"))).Append(',')
                .Append(Csv(row.ApprovedAt?.ToString("O"))).Append(',')
                .Append(Csv(row.RejectedAt?.ToString("O"))).Append(',')
                .Append(row.IsFinal).Append(',')
                .Append(Csv(row.CreatedAt.ToString("O"))).Append(',')
                .Append(Csv(row.UpdatedAt.ToString("O"))).Append("\r\n");
        }
        audit.Log("proc.boq.export", EntityTypes.ProjectBoqRevision, projectId.ToString(), $"Exported {rows.Count} BOQ revisions for project #{projectId}.");
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8", $"project-{projectId}-boq-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    [HttpGet]
    [RequirePermission("proc.boq", "view")]
    public async Task<ActionResult<ProcurementWorkspaceResponse>> GetWorkspace(int projectId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        try
        {
            var result = await service.GetWorkspaceAsync(projectId, ct);
            if (!await permissions.HasAsync(userId.Value, "proc.material-requests.view", ct))
                result.MaterialRequests = [];
            if (!await permissions.HasAsync(userId.Value, "proc.contract-lines.view", ct))
                result.ContractLines = [];
            if (!await permissions.HasAsync(userId.Value, "proc.warehouse.view", ct))
            {
                result.Receipts = [];
                result.Issues = [];
            }
            if (!await permissions.HasAsync(userId.Value, "proc.vendor-ratings.view", ct))
                result.VendorRatings = [];
            return Ok(result);
        }
        catch (ProcurementOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("material-requests")]
    [RequirePermission("proc.material-requests", "view")]
    public async Task<ActionResult<MaterialRequestListResponse>> ListMaterialRequests(
        int projectId,
        [FromQuery] MaterialRequestListParams parameters,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        try { return Ok(await service.ListMaterialRequestsAsync(projectId, parameters, ct)); }
        catch (ProcurementOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("boq-revisions")]
    [RequirePermission("proc.boq", "manage")]
    [Idempotency("proc.boq.create", requireKey: true)]
    public Task<ActionResult<ProjectBoqRevisionResponse>> CreateBoq(
        int projectId, [FromBody] ProjectBoqRevisionRequest request, CancellationToken ct) =>
        Execute(projectId, EntityTypes.ProjectBoqRevision, "proc.boq.create", userId =>
            service.CreateBoqRevisionAsync(projectId, request, userId, ct), true, ct);

    [HttpPut("boq-revisions/{id:int}")]
    [RequirePermission("proc.boq", "manage")]
    [Idempotency("proc.boq.update", requireKey: true)]
    public Task<ActionResult<ProjectBoqRevisionResponse>> UpdateBoq(
        int projectId, int id, [FromBody] ProjectBoqRevisionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.ProjectBoqRevision, "proc.boq.update", userId =>
            service.UpdateBoqRevisionAsync(projectId, id, request, userId, ct), ct);
    }

    [HttpPost("boq-revisions/{id:int}/submit")]
    [RequirePermission("proc.boq", "manage")]
    [Idempotency("proc.boq.submit", requireKey: true)]
    public Task<ActionResult<ProjectBoqRevisionResponse>> SubmitBoq(
        int projectId, int id, [FromBody] ProcurementTransitionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.ProjectBoqRevision, "proc.boq.submit", userId =>
            service.SubmitBoqRevisionAsync(projectId, id, request, userId, ct), ct);
    }

    [HttpPost("boq-revisions/{id:int}/decision")]
    [RequirePermission("proc.boq", "approve")]
    [Idempotency("proc.boq.decision", requireKey: true)]
    public Task<ActionResult<ProjectBoqRevisionResponse>> DecideBoq(
        int projectId, int id, [FromBody] ProcurementDecisionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.ProjectBoqRevision, "proc.boq.decision", userId =>
            service.DecideBoqRevisionAsync(projectId, id, request, userId, ct), ct);
    }

    [HttpPost("material-requests")]
    [RequirePermission("proc.material-requests", "manage")]
    [Idempotency("proc.material-requests.create", requireKey: true)]
    public Task<ActionResult<MaterialRequestResponse>> CreateRequest(
        int projectId, [FromBody] MaterialRequestUpsertRequest request, CancellationToken ct) =>
        Execute(projectId, EntityTypes.MaterialRequest, "proc.material-request.create", userId =>
            service.CreateMaterialRequestAsync(projectId, request, userId, ct), true, ct);

    [HttpPut("material-requests/{id:int}")]
    [RequirePermission("proc.material-requests", "manage")]
    [Idempotency("proc.material-requests.update", requireKey: true)]
    public Task<ActionResult<MaterialRequestResponse>> UpdateRequest(
        int projectId, int id, [FromBody] MaterialRequestUpsertRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.MaterialRequest, "proc.material-request.update", userId =>
            service.UpdateMaterialRequestAsync(projectId, id, request, userId, ct), ct);
    }

    [HttpPost("material-requests/{id:int}/submit")]
    [RequirePermission("proc.material-requests", "manage")]
    [Idempotency("proc.material-requests.submit", requireKey: true)]
    public Task<ActionResult<MaterialRequestResponse>> SubmitRequest(
        int projectId, int id, [FromBody] ProcurementTransitionRequest request, CancellationToken ct) =>
        TransitionRequest(projectId, id, request, "submit", ct);

    [HttpPost("material-requests/{id:int}/decision")]
    [RequirePermission("proc.material-requests", "approve")]
    [Idempotency("proc.material-requests.decision", requireKey: true)]
    public Task<ActionResult<MaterialRequestResponse>> DecideRequest(
        int projectId, int id, [FromBody] ProcurementDecisionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.MaterialRequest, "proc.material-request.decision", userId =>
            service.DecideMaterialRequestAsync(projectId, id, request, userId, ct), ct);
    }

    [HttpPost("material-requests/{id:int}/cancel")]
    [RequirePermission("proc.material-requests", "manage")]
    [Idempotency("proc.material-requests.cancel", requireKey: true)]
    public Task<ActionResult<MaterialRequestResponse>> CancelRequest(
        int projectId, int id, [FromBody] ProcurementTransitionRequest request, CancellationToken ct) =>
        TransitionRequest(projectId, id, request, "cancel", ct);

    [HttpPost("contract-lines")]
    [RequirePermission("proc.contract-lines", "manage")]
    [Idempotency("proc.contract-lines.create", requireKey: true)]
    public Task<ActionResult<ContractLineResponse>> CreateContractLine(
        int projectId, [FromBody] ContractLineUpsertRequest request, CancellationToken ct) =>
        Execute(projectId, EntityTypes.ContractLine, "proc.contract-line.create", _ =>
            service.CreateContractLineAsync(projectId, request, ct), true, ct);

    [HttpPut("contract-lines/{id:int}")]
    [RequirePermission("proc.contract-lines", "manage")]
    [Idempotency("proc.contract-lines.update", requireKey: true)]
    public Task<ActionResult<ContractLineResponse>> UpdateContractLine(
        int projectId, int id, [FromBody] ContractLineUpsertRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.ContractLine, "proc.contract-line.update", _ =>
            service.UpdateContractLineAsync(projectId, id, request, ct), ct);
    }

    [HttpPost("receipts")]
    [RequirePermission("proc.warehouse", "post")]
    [Idempotency("proc.warehouse.receipt.create", requireKey: true)]
    public Task<ActionResult<WarehouseReceiptResponse>> CreateReceipt(
        int projectId, [FromBody] WarehouseReceiptCreateRequest request, CancellationToken ct) =>
        Execute(projectId, EntityTypes.WarehouseReceipt, "proc.receipt.create", userId =>
            service.CreateReceiptAsync(projectId, request, userId, ct), true, ct);

    [HttpPost("receipts/{id:int}/post")]
    [RequirePermission("proc.warehouse", "post")]
    [Idempotency("proc.warehouse.receipt.post", requireKey: true)]
    public Task<ActionResult<WarehouseReceiptResponse>> PostReceipt(
        int projectId, int id, [FromBody] ProcurementTransitionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.WarehouseReceipt, "proc.receipt.post", userId =>
            service.PostReceiptAsync(projectId, id, request, userId, ct), ct);
    }

    [HttpPost("receipts/{id:int}/reverse")]
    [RequirePermission("proc.warehouse", "post")]
    [Idempotency("proc.warehouse.receipt.reverse", requireKey: true)]
    public Task<ActionResult<WarehouseReceiptResponse>> ReverseReceipt(
        int projectId, int id, [FromBody] WarehouseReversalRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.WarehouseReceipt, "proc.receipt.reverse", userId =>
            service.ReverseReceiptAsync(projectId, id, request, userId, ct), ct);
    }

    [HttpPost("issues")]
    [RequirePermission("proc.warehouse", "post")]
    [Idempotency("proc.warehouse.issue.create", requireKey: true)]
    public Task<ActionResult<WarehouseIssueResponse>> CreateIssue(
        int projectId, [FromBody] WarehouseIssueCreateRequest request, CancellationToken ct) =>
        Execute(projectId, EntityTypes.WarehouseIssue, "proc.issue.create", userId =>
            service.CreateIssueAsync(projectId, request, userId, ct), true, ct);

    [HttpPost("issues/{id:int}/post")]
    [RequirePermission("proc.warehouse", "post")]
    [Idempotency("proc.warehouse.issue.post", requireKey: true)]
    public Task<ActionResult<WarehouseIssueResponse>> PostIssue(
        int projectId, int id, [FromBody] ProcurementTransitionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.WarehouseIssue, "proc.issue.post", userId =>
            service.PostIssueAsync(projectId, id, request, userId, ct), ct);
    }

    [HttpPost("issues/{id:int}/reverse")]
    [RequirePermission("proc.warehouse", "post")]
    [Idempotency("proc.warehouse.issue.reverse", requireKey: true)]
    public Task<ActionResult<WarehouseIssueResponse>> ReverseIssue(
        int projectId, int id, [FromBody] WarehouseReversalRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.WarehouseIssue, "proc.issue.reverse", userId =>
            service.ReverseIssueAsync(projectId, id, request, userId, ct), ct);
    }

    [HttpPost("vendor-ratings")]
    [RequirePermission("proc.vendor-ratings", "manage")]
    [Idempotency("proc.vendor-ratings.create", requireKey: true)]
    public Task<ActionResult<VendorRatingResponse>> CreateRating(
        int projectId, [FromBody] VendorRatingUpsertRequest request, CancellationToken ct) =>
        Execute(projectId, EntityTypes.VendorRating, "proc.vendor-rating.create", userId =>
            service.CreateVendorRatingAsync(projectId, request, userId, ct), true, ct);

    [HttpPut("vendor-ratings/{id:int}")]
    [RequirePermission("proc.vendor-ratings", "manage")]
    [Idempotency("proc.vendor-ratings.update", requireKey: true)]
    public Task<ActionResult<VendorRatingResponse>> UpdateRating(
        int projectId, int id, [FromBody] VendorRatingUpsertRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.VendorRating, "proc.vendor-rating.update", _ =>
            service.UpdateVendorRatingAsync(projectId, id, request, ct), ct);
    }

    [HttpPost("vendor-ratings/{id:int}/submit")]
    [RequirePermission("proc.vendor-ratings", "manage")]
    [Idempotency("proc.vendor-ratings.submit", requireKey: true)]
    public Task<ActionResult<VendorRatingResponse>> SubmitRating(
        int projectId, int id, [FromBody] ProcurementTransitionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.VendorRating, "proc.vendor-rating.submit", _ =>
            service.SubmitVendorRatingAsync(projectId, id, request, ct), ct);
    }

    [HttpPost("vendor-ratings/{id:int}/decision")]
    [RequirePermission("proc.vendor-ratings", "approve")]
    [Idempotency("proc.vendor-ratings.decision", requireKey: true)]
    public Task<ActionResult<VendorRatingResponse>> DecideRating(
        int projectId, int id, [FromBody] ProcurementDecisionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.VendorRating, "proc.vendor-rating.decision", userId =>
            service.DecideVendorRatingAsync(projectId, id, request, userId, ct), ct);
    }

    private Task<ActionResult<MaterialRequestResponse>> TransitionRequest(
        int projectId, int id, ProcurementTransitionRequest request, string action, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return ExecuteNullable(projectId, id, EntityTypes.MaterialRequest, $"proc.material-request.{action}", userId =>
            action == "submit"
                ? service.SubmitMaterialRequestAsync(projectId, id, request, userId, ct)
                : service.CancelMaterialRequestAsync(projectId, id, request, userId, ct), ct);
    }

    private async Task<ActionResult<T>> Execute<T>(
        int projectId, string resourceType, string action, Func<int, Task<T>> operation, bool created, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        try
        {
            var result = await operation(userId.Value);
            Audit(action, resourceType, projectId, result!);
            return created ? StatusCode(StatusCodes.Status201Created, result) : Ok(result);
        }
        catch (ProcurementOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private async Task<ActionResult<T>> ExecuteNullable<T>(
        int projectId, int id, string resourceType, string action, Func<int, Task<T?>> operation, CancellationToken ct) where T : class
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId.Value, projectId, ct)) return NotFound();
        try
        {
            var result = await operation(userId.Value);
            if (result is null) return NotFound();
            Audit(action, resourceType, projectId, result);
            return Ok(result);
        }
        catch (ProcurementOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private void Audit(string action, string resourceType, int projectId, object value) => audit.Log(new AuditEvent
    {
        Action = action,
        ResourceType = resourceType,
        ResourceId = projectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Message = $"{resourceType} changed in operational project #{projectId}.",
        NewValue = value,
    });

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}