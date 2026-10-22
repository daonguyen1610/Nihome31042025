using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController, Authorize, Route("api/legal-checklist-templates")]
public sealed class LegalChecklistTemplatesController(LegalChecklistTemplateService service) : ControllerBase
{
    [HttpGet, RequirePermission("permit.templates", "view")]
    public Task<IActionResult> List([FromQuery] bool publishedOnly, CancellationToken ct) =>
        ExecuteAsync(async () => await service.ListAsync(publishedOnly, ct));

    [HttpGet("{id:int}"), RequirePermission("permit.templates", "view")]
    public Task<IActionResult> Get(int id, CancellationToken ct) => ExecuteAsync(async () => await service.GetAsync(id, ct));

    [HttpPost, RequirePermission("permit.templates", "manage"), Idempotency("legal.template.create", requireKey: true)]
    public Task<IActionResult> Create(SaveLegalChecklistTemplateRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => await service.SaveAsync(null, request, Actor(), ct));

    [HttpPut("{id:int}"), RequirePermission("permit.templates", "manage")]
    public Task<IActionResult> Update(int id, SaveLegalChecklistTemplateRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => await service.SaveAsync(id, request, Actor(), ct));

    [HttpPost("{id:int}/versions"), RequirePermission("permit.templates", "manage"), Idempotency("legal.template.version", requireKey: true)]
    public Task<IActionResult> Version(int id, LegalChecklistTemplateDecisionRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => await service.NewVersionAsync(id, request.RowVersion, Actor(), ct));

    [HttpPost("{id:int}/publish"), RequirePermission("permit.templates", "publish"), Idempotency("legal.template.publish", requireKey: true)]
    public Task<IActionResult> Publish(int id, LegalChecklistTemplateDecisionRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => await service.DecideAsync(id, request, Actor(), false, ct));

    [HttpPost("{id:int}/retire"), RequirePermission("permit.templates", "publish"), Idempotency("legal.template.retire", requireKey: true)]
    public Task<IActionResult> Retire(int id, LegalChecklistTemplateDecisionRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => await service.DecideAsync(id, request, Actor(), true, ct));

    private int Actor() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid")!);

    private async Task<IActionResult> ExecuteAsync(Func<Task<object?>> action)
    {
        try { var result = await action(); return result is null ? NotFound() : Ok(result); }
        catch (LegalActivationException ex) { return BadRequest(new { code = ex.Code, message = ex.Message }); }
        catch (CrmConcurrencyTokenException ex) { return BadRequest(new { code = "INVALID_ROW_VERSION", message = ex.Message }); }
        catch (CrmConcurrencyException ex) { return Conflict(new { code = "CONCURRENCY_CONFLICT", message = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { code = "LEGAL_TEMPLATE_CONFLICT", message = "Mẫu đã thay đổi hoặc mã/phiên bản bị trùng. Vui lòng tải lại." }); }
    }
}
