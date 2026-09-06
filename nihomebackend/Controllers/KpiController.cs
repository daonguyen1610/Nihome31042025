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
[Route("api/kpi")]
[Route("api/v1/kpi")]
[Authorize]
public class KpiController(
    IKpiService service,
    IPermissionService permissions,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet("definitions")]
    [RequirePermission("analytics.kpi", "view")]
    public async Task<ActionResult<IReadOnlyList<KpiDefinitionResponse>>> Definitions(CancellationToken ct) =>
        Ok(await service.ListDefinitionsAsync(ct));

    [HttpGet("eligible-users")]
    [RequirePermission("analytics.kpi", "view.all")]
    public async Task<ActionResult<IReadOnlyList<KpiUserOptionResponse>>> EligibleUsers(CancellationToken ct) =>
        Ok(await service.ListEligibleUsersAsync(ct));

    [HttpPut("definitions/{id:int}")]
    [RequirePermission("analytics.kpi", "manage")]
    public async Task<ActionResult<KpiDefinitionResponse>> UpdateDefinition(
        int id,
        [FromBody] UpdateKpiDefinitionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.UpdateDefinitionAsync(id, request, userId.Value, ct);
            if (result is null) return NotFound();
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            audit.Log(new AuditEvent
            {
                Action = "kpi.definition.update",
                ResourceType = "KpiDefinition",
                ResourceId = id.ToString(),
                Message = $"KPI definition {result.Code} updated to version {result.Version}.",
                NewValue = result,
            });
            return Ok(result);
        }
        catch (KpiOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("calculate")]
    [RequirePermission("analytics.kpi", "view")]
    [Idempotency("analytics.kpi.calculate")]
    public async Task<ActionResult<KpiDashboardResponse>> Calculate(
        [FromBody] CalculateKpiPeriodRequest request,
        CancellationToken ct)
    {
        var callerUserId = GetUserId();
        if (callerUserId is null) return Unauthorized();
        var targetUserId = request.UserId ?? callerUserId.Value;
        if (targetUserId != callerUserId.Value &&
            !await permissions.HasAsync(callerUserId.Value, "analytics.kpi.view.all", ct))
        {
            return Forbid();
        }
        try
        {
            var result = await service.CalculateAsync(
                request.Year, request.Month, targetUserId, callerUserId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "kpi.calculate",
                ResourceType = "KpiPeriod",
                ResourceId = result.PeriodId.ToString(),
                Message = $"Calculated KPI {request.Year:D4}-{request.Month:D2} for user #{targetUserId}.",
                NewValue = new { result.UserId, result.TotalScore, result.IsComplete },
            });
            return Ok(result);
        }
        catch (KpiOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("dashboard")]
    [RequirePermission("analytics.kpi", "view")]
    public async Task<ActionResult<KpiDashboardResponse>> Dashboard(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int? userId,
        CancellationToken ct)
    {
        var callerUserId = GetUserId();
        if (callerUserId is null) return Unauthorized();
        var targetUserId = userId ?? callerUserId.Value;
        if (targetUserId != callerUserId.Value &&
            !await permissions.HasAsync(callerUserId.Value, "analytics.kpi.view.all", ct))
        {
            return Forbid();
        }
        try
        {
            var result = await service.GetDashboardAsync(year, month, targetUserId, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (KpiOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("periods/{year:int}/{month:int}/lock")]
    [RequirePermission("analytics.kpi", "manage")]
    public async Task<ActionResult<KpiDashboardResponse>> Lock(
        int year,
        int month,
        [FromBody] LockKpiPeriodRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (request.UserId.HasValue && request.UserId.Value != userId.Value &&
            !await permissions.HasAsync(userId.Value, "analytics.kpi.view.all", ct))
        {
            return Forbid();
        }
        request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.LockAsync(year, month, request, userId.Value, ct);
            if (result is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "kpi.period.lock",
                ResourceType = "KpiPeriod",
                ResourceId = result.PeriodId.ToString(),
                Message = $"Locked KPI period {year:D4}-{month:D2}: {request.Note.Trim()}",
            });
            return Ok(result);
        }
        catch (KpiOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("export")]
    [RequirePermission("analytics.kpi", "export")]
    public async Task<IActionResult> Export(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int? userId,
        CancellationToken ct)
    {
        var callerUserId = GetUserId();
        if (callerUserId is null) return Unauthorized();
        var targetUserId = userId ?? callerUserId.Value;
        if (targetUserId != callerUserId.Value &&
            !await permissions.HasAsync(callerUserId.Value, "analytics.kpi.view.all", ct))
        {
            return Forbid();
        }
        KpiDashboardResponse? dashboard;
        try
        {
            dashboard = await service.GetDashboardAsync(year, month, targetUserId, ct);
        }
        catch (KpiOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        if (dashboard is null) return NotFound();
        var csv = new StringBuilder("Year,Month,TimeZone,PeriodStatus,UserId,UserName,RoleCode,LockedAt,LockedBy,LockNote,Code,NameKey,Weight,TargetValue,RawValue,Score,WeightedScore,Status,DefinitionVersion,EvidenceJson\r\n");
        foreach (var score in dashboard.Scores)
        {
            csv.Append(dashboard.Year).Append(',')
                .Append(dashboard.Month).Append(',')
                .Append(Csv(dashboard.TimeZoneId)).Append(',')
                .Append(dashboard.PeriodStatus).Append(',')
                .Append(dashboard.UserId).Append(',')
                .Append(Csv(dashboard.UserName)).Append(',')
                .Append(Csv(dashboard.RoleCode)).Append(',')
                .Append(dashboard.LockedAt?.ToString("O")).Append(',')
                .Append(Csv(dashboard.LockedByName ?? string.Empty)).Append(',')
                .Append(Csv(dashboard.LockNote ?? string.Empty)).Append(',')
                .Append(Csv(score.Code)).Append(',')
                .Append(Csv(score.NameKey)).Append(',')
                .Append(score.Weight).Append(',')
                .Append(score.TargetValue).Append(',')
                .Append(score.RawValue).Append(',')
                .Append(score.Score).Append(',')
                .Append(score.WeightedScore).Append(',')
                .Append(score.Status).Append(',')
                .Append(score.DefinitionVersion).Append(',')
                .Append(Csv(score.EvidenceJson)).Append("\r\n");
        }
        audit.Log(new AuditEvent
        {
            Action = "kpi.export",
            ResourceType = "KpiPeriod",
            ResourceId = dashboard.PeriodId.ToString(),
            Message = $"Exported KPI {year:D4}-{month:D2} for user #{targetUserId}.",
        });
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8", $"kpi-{year:D4}-{month:D2}-user-{targetUserId}.csv");
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(value, out var userId) ? userId : null;
    }
}
