using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController, Authorize]
[Route("api/operational-projects/{projectId:int}/construction")]
[Route("api/v1/operational-projects/{projectId:int}/construction")]
public sealed class ConstructionOperationsController(IConstructionOperationsService service, IProjectAccessService access) : ControllerBase
{
    [HttpGet("preparation"), RequirePermission("construction.cm-preparation", "view")]
    public async Task<IActionResult> GetPreparation(int projectId, CancellationToken ct) => await Run(projectId, async uid => await service.GetPreparationAsync(projectId, ct));

    [HttpPut("preparation"), RequirePermission("construction.cm-preparation", "manage"), Idempotency("construction.preparation.upsert", requireKey: true)]
    public async Task<IActionResult> PutPreparation(int projectId, UpsertConstructionPreparationRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion); return await Run(projectId, uid => service.UpsertPreparationAsync(projectId, request, uid, ct)); }

    [HttpPost("readiness/evaluate"), RequirePermission("construction.cm-preparation", "manage"), Idempotency("construction.readiness.evaluate", requireKey: true)]
    public Task<IActionResult> Evaluate(int projectId, ProcurementTransitionRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.EvaluateReadinessAsync(projectId, request.RowVersion, uid, ct)); }

    [HttpPost("readiness/start"), RequirePermission("construction.cm-preparation", "start"), Idempotency("construction.readiness.start", requireKey: true)]
    public Task<IActionResult> Start(int projectId, ProcurementTransitionRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.StartAsync(projectId, request.RowVersion, uid, ct)); }

    [HttpGet("resource-requests"), RequirePermission("construction.resource-request", "view")]
    public Task<IActionResult> Resources(int projectId, CancellationToken ct) => Run(projectId, uid => service.ListResourcesAsync(projectId, ct));
    [HttpGet("resource-references"), RequirePermission("construction.resource-request", "view")]
    public Task<IActionResult> ResourceReferences(int projectId, CancellationToken ct) =>
        Run(projectId, uid => service.GetResourceReferencesAsync(projectId, ct));
    [HttpGet("resource-requests/{id:int}"), RequirePermission("construction.resource-request", "view")]
    public Task<IActionResult> Resource(int projectId, int id, CancellationToken ct) => Run(projectId, uid => service.GetResourceAsync(projectId, id, ct));
    [HttpPost("resource-requests"), RequirePermission("construction.resource-request", "create"), Idempotency("construction.resource-request.create", requireKey: true)]
    public Task<IActionResult> CreateResource(int projectId, UpsertConstructionResourceRequest request, CancellationToken ct) => Run(projectId, uid => service.CreateResourceAsync(projectId, request, uid, ct), true);
    [HttpPut("resource-requests/{id:int}"), RequirePermission("construction.resource-request", "create"), Idempotency("construction.resource-request.update", requireKey: true)]
    public Task<IActionResult> UpdateResource(int projectId, int id, UpsertConstructionResourceRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.UpdateResourceAsync(projectId, id, request, uid, ct)); }
    [HttpPost("resource-requests/{id:int}/submit"), RequirePermission("construction.resource-request", "create"), Idempotency("construction.resource-request.submit", requireKey: true)]
    public Task<IActionResult> SubmitResource(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.SubmitResourceAsync(projectId, id, request.RowVersion, uid, ct)); }
    [HttpPost("resource-requests/{id:int}/cm-decision"), RequirePermission("construction.resource-request", "cm-review"), Idempotency("construction.resource-request.cm-decision", requireKey: true)]
    public Task<IActionResult> DecideResource(int projectId, int id, CmDecisionRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.DecideResourceAsync(projectId, id, request, uid, ct)); }

    [HttpGet("qa-qc/templates"), RequirePermission("construction.qa-qc", "view")]
    public Task<IActionResult> Templates(int projectId, CancellationToken ct) => Run(projectId, uid => service.ListTemplatesAsync(ct));
    [HttpPost("qa-qc/templates"), RequirePermission("construction.qa-qc", "template-manage"), Idempotency("construction.qa-qc.template.create", requireKey: true)]
    public Task<IActionResult> CreateTemplate(int projectId, CreateQaQcTemplateRequest request, CancellationToken ct) => Run(projectId, uid => service.CreateTemplateAsync(request, uid, ct), true);
    [HttpPost("qa-qc/templates/{id:int}/publish"), RequirePermission("construction.qa-qc", "template-manage"), Idempotency("construction.qa-qc.template.publish", requireKey: true)]
    public Task<IActionResult> PublishTemplate(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.PublishTemplateAsync(id, request.RowVersion, ct)); }

    [HttpGet("qa-qc/dossiers"), RequirePermission("construction.qa-qc", "view")]
    public Task<IActionResult> Dossiers(int projectId, CancellationToken ct) => Run(projectId, uid => service.ListDossiersAsync(projectId, ct));
    [HttpGet("qa-qc/dossiers/{id:int}"), RequirePermission("construction.qa-qc", "view")]
    public Task<IActionResult> Dossier(int projectId, int id, CancellationToken ct) => Run(projectId, uid => service.GetDossierAsync(projectId, id, ct));
    [HttpPost("qa-qc/dossiers"), RequirePermission("construction.qa-qc", "create"), Idempotency("construction.qa-qc.dossier.create", requireKey: true)]
    public Task<IActionResult> CreateDossier(int projectId, CreateQaQcDossierRequest request, CancellationToken ct) => Run(projectId, uid => service.CreateDossierAsync(projectId, request, uid, ct), true);
    [HttpPut("qa-qc/dossiers/{id:int}"), RequirePermission("construction.qa-qc", "create"), Idempotency("construction.qa-qc.dossier.update", requireKey: true)]
    public Task<IActionResult> UpdateDossier(int projectId, int id, CreateQaQcDossierRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.UpdateDossierAsync(projectId, id, request, request.RowVersion, uid, ct)); }
    [HttpPost("qa-qc/dossiers/{id:int}/submit"), RequirePermission("construction.qa-qc", "create"), Idempotency("construction.qa-qc.dossier.submit", requireKey: true)]
    public Task<IActionResult> SubmitDossier(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.SubmitDossierAsync(projectId, id, request.RowVersion, uid, ct)); }
    [HttpPost("qa-qc/dossiers/{id:int}/cm-decision"), RequirePermission("construction.qa-qc", "cm-review"), Idempotency("construction.qa-qc.dossier.cm-decision", requireKey: true)]
    public Task<IActionResult> DecideDossier(int projectId, int id, CmDecisionRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.DecideDossierAsync(projectId, id, request, uid, ct)); }

    [HttpGet("qs-batches"), RequirePermission("construction.qs", "view")]
    public Task<IActionResult> Qs(int projectId, CancellationToken ct) => Run(projectId, uid => service.ListQsAsync(projectId, ct));
    [HttpGet("qs-references"), RequirePermission("construction.qs", "view")]
    public Task<IActionResult> QsReferences(int projectId, CancellationToken ct) =>
        Run(projectId, uid => service.GetQsReferencesAsync(projectId, ct));
    [HttpGet("qs-batches/{id:int}"), RequirePermission("construction.qs", "view")]
    public Task<IActionResult> Qs(int projectId, int id, CancellationToken ct) => Run(projectId, uid => service.GetQsAsync(projectId, id, ct));
    [HttpPost("qs-batches"), RequirePermission("construction.qs", "create"), Idempotency("construction.qs.create", requireKey: true)]
    public Task<IActionResult> CreateQs(int projectId, CreateConstructionQsBatchRequest request, CancellationToken ct) => Run(projectId, uid => service.CreateQsAsync(projectId, request, uid, ct), true);
    [HttpPut("qs-batches/{id:int}"), RequirePermission("construction.qs", "create"), Idempotency("construction.qs.update", requireKey: true)]
    public Task<IActionResult> UpdateQs(int projectId, int id, CreateConstructionQsBatchRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.UpdateQsAsync(projectId, id, request, request.RowVersion, uid, ct)); }
    [HttpPost("qs-batches/{id:int}/submit"), RequirePermission("construction.qs", "create"), Idempotency("construction.qs.submit", requireKey: true)]
    public Task<IActionResult> SubmitQs(int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.SubmitQsAsync(projectId, id, request.RowVersion, uid, ct)); }
    [HttpPost("qs-batches/{id:int}/cm-decision"), RequirePermission("construction.qs", "cm-review"), Idempotency("construction.qs.cm-decision", requireKey: true)]
    public Task<IActionResult> DecideQs(int projectId, int id, CmDecisionRequest request, CancellationToken ct)
    { request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion); return Run(projectId, uid => service.DecideQsAsync(projectId, id, request, uid, ct)); }

    private async Task<IActionResult> Run<T>(int projectId, Func<int, Task<T>> action, bool created = false)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"); if (!int.TryParse(raw, out var userId)) return Unauthorized();
        if (!await access.CanViewOperationalProjectAsync(userId, projectId, HttpContext.RequestAborted)) return NotFound();
        try { var value = await action(userId); if (value is null) return NotFound(); return created ? StatusCode(StatusCodes.Status201Created, value) : Ok(value); }
        catch (ConstructionOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
