using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

/// <summary>"Việc của tôi" and the daily work report. Every read is scoped to the caller.</summary>
[ApiController]
[Route("api/my-work")]
[Route("api/v1/my-work")]
[Authorize]
public sealed class MyWorkController(
    MyWorkService myWork,
    DailyWorkReportService reports,
    IPermissionService permissions) : ControllerBase
{
    [HttpGet]
    [RequirePermission("work.packages", "view")]
    public async Task<ActionResult<MyWorkResponse>> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        try { return Ok(await myWork.GetAsync(UserId(), from, to, ct)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("daily-reports/today")]
    [RequirePermission("work.packages", "view")]
    public async Task<ActionResult<DailyWorkReportResponse>> Today(CancellationToken ct)
        => Ok(await reports.GetTodayAsync(UserId(), ct));

    [HttpGet("daily-reports/mine")]
    [RequirePermission("work.packages", "view")]
    public async Task<ActionResult<IReadOnlyList<DailyWorkReportResponse>>> Mine(CancellationToken ct)
        => Ok(await reports.HistoryAsync(UserId(), ct));

    [HttpPut("daily-reports/today")]
    [RequirePermission("work.packages", "view")]
    public Task<ActionResult<DailyWorkReportResponse>> Submit(SubmitDailyWorkReportRequest request, CancellationToken ct)
        => Execute(() => reports.SubmitTodayAsync(UserId(), request, ct));

    [HttpGet("daily-reports/department")]
    [RequirePermission("work.packages", "view")]
    public async Task<ActionResult<DepartmentDailyReportsResponse>> Department(
        [FromQuery] DateOnly? date, [FromQuery] int? departmentId, CancellationToken ct)
        => await Execute(async () => await reports.DepartmentAsync(UserId(), date, departmentId, await CanViewAllAsync(ct), ct));

    [HttpPost("daily-reports/{id:int}/review")]
    [RequirePermission("work.packages", "view")]
    public async Task<ActionResult<DailyWorkReportResponse>> Review(int id, ReviewDailyWorkReportRequest request, CancellationToken ct)
        => await Execute(async () => await reports.ReviewAsync(UserId(), id, request, await CanViewAllAsync(ct), ct));

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> operation)
    {
        try { return Ok(await operation()); }
        catch (DailyWorkReportScopeException) { return Forbid(); }
        catch (DailyWorkReportException exception) { return BadRequest(new { code = exception.Code, message = exception.Message }); }
    }

    private Task<bool> CanViewAllAsync(CancellationToken ct) => permissions.HasAsync(UserId(), "work.packages.view.all", ct);

    // [Authorize] plus RequirePermission guarantee an authenticated numeric user id.
    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
}
