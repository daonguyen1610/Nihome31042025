using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/tenders/{tenderId:int}/estimates")]
[Route("api/v1/tenders/{tenderId:int}/estimates")]
[Authorize]
public sealed class TenderEstimatesController(ITenderEstimateService service, IAuditLogger audit) : ControllerBase
{
    [HttpGet("template")]
    [RequirePermission("crm.tenders", "view")]
    public IActionResult DownloadTemplate()
    {
        var body = string.Join(',', TenderEstimateService.CsvHeaders) + "\r\n"
            + "HM-001,Thi công tường thạch cao,m2,100,250000,320000,10,Gồm vật tư và nhân công\r\n";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(body)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", "tender-estimate-template.csv");
    }

    [HttpGet]
    [RequirePermission("crm.tenders", "view")]
    public async Task<ActionResult<List<TenderEstimateRevisionResponse>>> List(int tenderId, CancellationToken ct)
    {
        var result = await service.ListAsync(tenderId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{revisionId:int}")]
    [RequirePermission("crm.tenders", "view")]
    public async Task<ActionResult<TenderEstimateRevisionResponse>> Get(
        int tenderId,
        int revisionId,
        CancellationToken ct)
    {
        var result = await service.GetAsync(tenderId, revisionId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<ActionResult<TenderEstimateImportResponse>> Import(
        int tenderId,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn tệp dự toán CSV UTF-8, ví dụ: tender-estimate.csv." });
        }
        if (file.Length > TenderEstimateService.MaxCsvBytes)
        {
            return BadRequest(new { message = "Tệp dự toán CSV vượt quá dung lượng tối đa 2 MB." });
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Tệp dự toán phải có định dạng .csv, ví dụ: tender-estimate.csv." });
        }

        return await ExecuteAsync<TenderEstimateImportResponse>(async () =>
        {
            await using var stream = file.OpenReadStream();
            var result = await service.ImportAsync(tenderId, stream, file.FileName, userId.Value, ct);
            if (result is null) return NotFound();
            if (result.Errors.Count > 0) return BadRequest(result);
            Audit("tender-estimate.import", result.Revision!.Id, result.Revision);
            return CreatedAtAction(nameof(Get), new { tenderId, revisionId = result.Revision.Id }, result);
        });
    }

    [HttpPost("{revisionId:int}/submit")]
    [RequirePermission("crm.tenders", "manage")]
    public async Task<ActionResult<TenderEstimateRevisionResponse>> Submit(
        int tenderId,
        int revisionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] DecideTenderEstimateRequest? request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<TenderEstimateRevisionResponse>(async () =>
        {
            var result = await service.SubmitAsync(tenderId, revisionId, request?.Note, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("tender-estimate.submit", revisionId, result);
            return Ok(result);
        });
    }

    [HttpPost("{revisionId:int}/approve")]
    [RequirePermission("crm.tenders", "approve-estimate")]
    public Task<ActionResult<TenderEstimateRevisionResponse>> Approve(
        int tenderId,
        int revisionId,
        [FromBody] DecideTenderEstimateRequest? request,
        CancellationToken ct) => Decide(tenderId, revisionId, request?.Note, true, ct);

    [HttpPost("{revisionId:int}/reject")]
    [RequirePermission("crm.tenders", "approve-estimate")]
    public Task<ActionResult<TenderEstimateRevisionResponse>> Reject(
        int tenderId,
        int revisionId,
        [FromBody] DecideTenderEstimateRequest? request,
        CancellationToken ct) => Decide(tenderId, revisionId, request?.Note, false, ct);

    private async Task<ActionResult<TenderEstimateRevisionResponse>> Decide(
        int tenderId,
        int revisionId,
        string? note,
        bool approve,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<TenderEstimateRevisionResponse>(async () =>
        {
            var result = approve
                ? await service.ApproveAsync(tenderId, revisionId, note, userId.Value, ct)
                : await service.RejectAsync(tenderId, revisionId, note, userId.Value, ct);
            if (result is null) return NotFound();
            Audit(approve ? "tender-estimate.approve" : "tender-estimate.reject", revisionId, result);
            return Ok(result);
        });
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (TenderEstimateOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private void Audit(string action, int revisionId, object value) => audit.Log(new AuditEvent
    {
        Action = action,
        ResourceType = EntityTypes.TenderEstimateRevision,
        ResourceId = revisionId.ToString(),
        Message = $"{action} #{revisionId}.",
        NewValue = value,
    });

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(value, out var userId) ? userId : null;
    }
}
