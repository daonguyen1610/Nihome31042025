using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Authorization;
using NihomeBackend.Data;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController, Authorize]
[Route("api/operational-projects/{projectId:int}/procurement/rfqs")]
[Route("api/v1/operational-projects/{projectId:int}/procurement/rfqs")]
public sealed class RfqsController(RfqService service, IProjectAccessService access,
    IProjectDocumentService documents, AppDbContext db, IAuditLogger audit) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    [HttpGet, RequirePermission("proc.rfqs", "view")]
    public async Task<IActionResult> List(int projectId, [FromQuery] RfqListQuery query, CancellationToken ct) =>
        await access.CanViewOperationalProjectAsync(UserId, projectId, ct) ? Ok(await service.ListAsync(projectId, query, false, ct)) : NotFound();

    [HttpGet("references"), RequirePermission("proc.rfqs", "view")]
    public async Task<IActionResult> References(int projectId, CancellationToken ct) =>
        await access.CanViewOperationalProjectAsync(UserId, projectId, ct) ? Ok(await service.ReferencesAsync(projectId, ct)) : NotFound();

    [HttpPost("documents/upload"), RequirePermission("proc.rfqs", "manage")]
    [Consumes("multipart/form-data")]
    [Idempotency("proc.rfqs.upload", requestGuardType: typeof(RfqIdempotencyGuard), requireKey: true)]
    [RequestSizeLimit(26 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int projectId, IFormFile file, CancellationToken ct)
    {
        if (!await access.CanViewOperationalProjectAsync(UserId, projectId, ct)) return NotFound();
        if (await db.OperationalProjects.AnyAsync(x => x.Id == projectId &&
            (x.Status == Models.OperationalProjectStatus.Completed || x.Status == Models.OperationalProjectStatus.Cancelled), ct))
            return BadRequest(new { message = "Completed or cancelled projects cannot be changed." });
        try
        {
            var result = await documents.UploadAsync(projectId, new ProjectDocumentUploadRequest
            { File = file, Category = Models.ProjectDocumentCategory.Procurement }, UserId, true, ct);
            if (result is null) return NotFound();
            audit.Log("proc.rfqs.upload", "ProjectDocument", result.Id.ToString(), $"Uploaded RFQ evidence in project #{projectId}.");
            return Ok(new RfqFileResponse(result.Id, null, result.OriginalFileName));
        }
        catch (ProjectDocumentValidationException error) { return BadRequest(new { message = error.Message }); }
    }

    [HttpGet("{id:int}"), RequirePermission("proc.rfqs", "view")]
    public async Task<IActionResult> Get(int projectId, int id, CancellationToken ct)
    {
        if (!await access.CanViewOperationalProjectAsync(UserId, projectId, ct)) return NotFound();
        var result = await service.GetAsync(projectId, id, ct);
        if (result is null) return NotFound();
        CrmConcurrency.SetResponseEntityTag(Response, result.Header.RowVersion);
        return Ok(result);
    }

    [HttpGet("export"), RequirePermission("proc.rfqs", "export"), RequirePermission("proc.rfqs", "view")]
    public async Task<IActionResult> Export(int projectId, [FromQuery] RfqListQuery query, CancellationToken ct)
    {
        if (!await access.CanViewOperationalProjectAsync(UserId, projectId, ct)) return NotFound();
        var result = await service.ListAsync(projectId, query, true, ct);
        var csv = new StringBuilder("Code,Title,Project,Customer,BOQ,Status,Owner,Invited,Received,IssuedAt,DueAt,UpdatedAt\r\n");
        foreach (var x in result.Items)
            csv.AppendLine(string.Join(',', new[] { x.Code, x.Title, x.ProjectCode, x.CustomerName, x.BoqRevision.ToString(),
                x.Status.ToString(), x.OwnerName, x.InvitedCount.ToString(), x.ReceivedCount.ToString(), x.IssuedAt?.ToString("O"),
                x.DueAt.ToString("O"), x.UpdatedAt.ToString("O") }.Select(Csv)));
        audit.Log("proc.rfqs.export", "OperationalProject", projectId.ToString(), $"Exported {result.Total} RFQs.");
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", $"rfqs-{projectId}.csv");
    }

    [HttpGet("{id:int}/export"), RequirePermission("proc.rfqs", "export"), RequirePermission("proc.rfqs", "view")]
    public async Task<IActionResult> ExportDetail(int projectId, int id, CancellationToken ct)
    {
        if (!await access.CanViewOperationalProjectAsync(UserId, projectId, ct)) return NotFound();
        var result = await service.GetAsync(projectId, id, ct);
        if (result is null) return NotFound();
        audit.Log("proc.rfqs.export-detail", "Rfq", id.ToString(), $"Exported comparison in project #{projectId}.");
        return File(JsonSerializer.SerializeToUtf8Bytes(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }),
            "application/json", $"{result.Header.Code}-comparison.json");
    }

    [HttpPost, RequirePermission("proc.rfqs", "manage"), Idempotency("proc.rfqs.create", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> Create(int projectId, RfqUpsertRequest request, CancellationToken ct) =>
        Execute(projectId, "created", () => service.SaveAsync(projectId, null, request, UserId, ct), ct, created: true);

    [HttpPut("{id:int}"), RequirePermission("proc.rfqs", "manage"), Idempotency("proc.rfqs.update", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> Update(int projectId, int id, RfqUpsertRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return Execute(projectId, "updated", () => service.SaveAsync(projectId, id, request, UserId, ct), ct);
    }

    [HttpPost("{id:int}/issue"), RequirePermission("proc.rfqs", "manage"), Idempotency("proc.rfqs.issue", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> Issue(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct) => Transition(projectId, id, "issue", request, ct);

    [HttpPost("{id:int}/evaluate"), RequirePermission("proc.rfqs", "manage"), Idempotency("proc.rfqs.evaluate", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> Evaluate(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct) => Transition(projectId, id, "evaluate", request, ct);

    [HttpPost("{id:int}/cancel"), RequirePermission("proc.rfqs", "manage"), Idempotency("proc.rfqs.cancel", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> Cancel(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct) => Transition(projectId, id, "cancel", request, ct);

    [HttpPost("{id:int}/close"), RequirePermission("proc.rfqs", "manage"), Idempotency("proc.rfqs.close", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> Close(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct) => Transition(projectId, id, "close", request, ct);

    private Task<IActionResult> Transition(int projectId, int id, string action, ProcurementTransitionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return Execute(projectId, action, () => service.TransitionAsync(projectId, id, action, request, UserId, ct), ct);
    }

    [HttpPost("{id:int}/bids"), RequirePermission("proc.rfqs", "manage"), Idempotency("proc.rfqs.bid-submit", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> SubmitBid(int projectId, int id, RfqBidRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return Execute(projectId, "bid-submitted", () => service.SubmitBidAsync(projectId, id, request, UserId, ct), ct);
    }

    [HttpPost("{id:int}/bids/{bidId:int}/withdraw"), RequirePermission("proc.rfqs", "manage"), Idempotency("proc.rfqs.bid-withdraw", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> Withdraw(int projectId, int id, int bidId, ProcurementTransitionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return Execute(projectId, "bid-withdrawn", () => service.WithdrawBidAsync(projectId, id, bidId, request, UserId, ct), ct);
    }

    [HttpPost("{id:int}/award"), RequirePermission("proc.rfqs", "award"), Idempotency("proc.rfqs.award", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> Award(int projectId, int id, RfqAwardRequest request, CancellationToken ct)
    {
        return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status410Gone,
            new { message = "The legacy whole-package award endpoint is retired. Use award-batch." }));
    }

    [HttpPost("{id:int}/bids/evaluate"), RequirePermission("proc.rfqs", "manage"), Idempotency("proc.rfqs.bid-evaluate", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> EvaluateBid(int projectId, int id, RfqBidEvaluationRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return Execute(projectId, "bid-evaluated", () => service.EvaluateBidAsync(projectId, id, request, UserId, ct), ct);
    }

    [HttpPost("{id:int}/award-batch"), RequirePermission("proc.rfqs", "award"), Idempotency("proc.rfqs.award-batch", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> BatchAward(int projectId, int id, RfqBatchAwardRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return Execute(projectId, "batch-awarded", () => service.BatchAwardAsync(projectId, id, request, UserId, ct), ct);
    }

    [HttpPost("{id:int}/invitations/resend"), RequirePermission("proc.rfqs", "manage"), Idempotency("proc.rfqs.invitation-resend", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> ResendInvitations(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return Execute(projectId, "invitations-resent", () => service.ResendInvitationsAsync(projectId, id, request, UserId, ct), ct);
    }

    [HttpPost("{id:int}/documents"), RequirePermission("proc.rfqs", "manage"), Idempotency("proc.rfqs.attach", requireKey: true, requestGuardType: typeof(RfqIdempotencyGuard))]
    public Task<IActionResult> Attach(int projectId, int id, RfqAttachDocumentRequest request, CancellationToken ct)
    {
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        return Execute(projectId, "document-attached", () => service.AttachDocumentAsync(projectId, id, request, UserId, ct), ct);
    }

    [HttpGet("{id:int}/documents/{documentId:long}/download"), RequirePermission("proc.rfqs", "view")]
    public async Task<IActionResult> Download(int projectId, int id, long documentId, CancellationToken ct)
    {
        if (!await access.CanViewOperationalProjectAsync(UserId, projectId, ct) ||
            !await db.RfqDocuments.AnyAsync(x => x.RfqId == id && x.Rfq.OperationalProjectId == projectId && x.ProjectDocumentId == documentId, ct))
            return NotFound();
        // Project scope has already been verified above, including team membership.
        var file = await documents.DownloadAsync(projectId, documentId, UserId, true, ct);
        if (file is null) return NotFound();
        audit.Log("proc.rfqs.download", "Rfq", id.ToString(), $"Downloaded document #{documentId} in project #{projectId}.");
        Response.ContentType = file.ContentType;
        Response.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
        { FileNameStar = file.OriginalFileName }.ToString();
        await file.WriteToAsync(Response.Body, ct);
        return new EmptyResult();
    }

    private async Task<IActionResult> Execute(int projectId, string action, Func<Task<RfqDetailResponse?>> operation, CancellationToken ct, bool created = false)
    {
        if (!await access.CanViewOperationalProjectAsync(UserId, projectId, ct)) return NotFound();
        try
        {
            var result = await operation();
            if (result is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = $"proc.rfqs.{action}",
                ResourceType = "Rfq",
                ResourceId = result.Header.Id.ToString(),
                Message = $"RFQ {action} in project #{projectId}.",
                Metadata = new { operationalProjectId = projectId, status = result.Header.Status, result.SelectedBidId, result.ContractId },
            });
            CrmConcurrency.SetResponseEntityTag(Response, result.Header.RowVersion);
            return created ? StatusCode(201, result) : Ok(result);
        }
        catch (ProcurementOperationException error) { return BadRequest(new { message = error.Message }); }
        catch (ProjectDocumentValidationException error) { return BadRequest(new { message = error.Message }); }
    }

    private static string Csv(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.TrimStart().StartsWith('=') || safe.TrimStart().StartsWith('+') || safe.TrimStart().StartsWith('-') || safe.TrimStart().StartsWith('@')) safe = "'" + safe;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
