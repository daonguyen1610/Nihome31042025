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
[Route("api/material-rate-catalogs")]
[Route("api/v1/material-rate-catalogs")]
[Authorize]
public sealed class MaterialRateCatalogsController(IMaterialRateService service, IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<ActionResult<List<MaterialRateCatalogResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default) => Ok(await service.ListCatalogsAsync(search, includeInactive, ct));

    [HttpGet("{id:int}")]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<ActionResult<MaterialRateCatalogResponse>> Get(int id, CancellationToken ct)
    {
        var result = await service.GetCatalogAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission("crm.material-rates", "manage")]
    public async Task<ActionResult<MaterialRateCatalogResponse>> Create(
        [FromBody] UpsertMaterialRateCatalogRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<MaterialRateCatalogResponse>(async () =>
        {
            var result = await service.CreateCatalogAsync(request, userId.Value, ct);
            Audit("material-rate-catalog.create", result.Id, result);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        });
    }

    [HttpPut("{id:int}")]
    [RequirePermission("crm.material-rates", "manage")]
    public async Task<ActionResult<MaterialRateCatalogResponse>> Update(
        int id,
        [FromBody] UpsertMaterialRateCatalogRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<MaterialRateCatalogResponse>(async () =>
        {
            var result = await service.UpdateCatalogAsync(id, request, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("material-rate-catalog.update", id, result);
            return Ok(result);
        });
    }

    [HttpGet("csv-template")]
    [RequirePermission("crm.material-rates", "view")]
    public IActionResult DownloadTemplate()
    {
        var body = string.Join(',', MaterialRateService.CsvHeaders) + "\r\n"
            + "VL-001,Keo dán gạch,kg,2.5,15000,5\r\n";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(body)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", "material-rate-template.csv");
    }

    [HttpGet("{catalogId:int}/revisions")]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<ActionResult<List<MaterialRateRevisionResponse>>> ListRevisions(int catalogId, CancellationToken ct)
    {
        var result = await service.ListRevisionsAsync(catalogId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{catalogId:int}/revisions/{revisionId:int}")]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<ActionResult<MaterialRateRevisionResponse>> GetRevision(
        int catalogId,
        int revisionId,
        CancellationToken ct)
    {
        var result = await service.GetRevisionAsync(catalogId, revisionId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{catalogId:int}/revisions")]
    [RequirePermission("crm.material-rates", "manage")]
    public async Task<ActionResult<MaterialRateRevisionResponse>> CreateRevision(
        int catalogId,
        [FromBody] CreateMaterialRateRevisionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<MaterialRateRevisionResponse>(async () =>
        {
            var result = await service.CreateRevisionAsync(catalogId, request, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("material-rate-revision.create", result.Id, result);
            return CreatedAtAction(nameof(GetRevision), new { catalogId, revisionId = result.Id }, result);
        });
    }

    [HttpPost("{catalogId:int}/revisions/{revisionId:int}/import")]
    [Consumes("multipart/form-data")]
    [RequirePermission("crm.material-rates", "manage")]
    public async Task<ActionResult<MaterialRateImportResponse>> Import(
        int catalogId,
        int revisionId,
        IFormFile? file,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn tệp CSV UTF-8 để nhập." });
        }
        if (file.Length > 2 * 1024 * 1024)
        {
            return BadRequest(new { message = "Tệp CSV vượt quá dung lượng tối đa 2 MB." });
        }

        return await ExecuteAsync<MaterialRateImportResponse>(async () =>
        {
            await using var stream = file.OpenReadStream();
            var result = await service.ImportAsync(catalogId, revisionId, stream, userId.Value, ct);
            if (result is null) return NotFound();
            if (result.Errors.Count > 0) return BadRequest(result);
            Audit("material-rate-revision.import", revisionId, result);
            return Ok(result);
        });
    }

    [HttpPost("{catalogId:int}/revisions/{revisionId:int}/approve")]
    [RequirePermission("crm.material-rates", "approve")]
    public Task<ActionResult<MaterialRateRevisionResponse>> Approve(
        int catalogId,
        int revisionId,
        [FromBody] DecideMaterialRateRevisionRequest? request,
        CancellationToken ct) => Decide(catalogId, revisionId, request?.Note, true, ct);

    [HttpPost("{catalogId:int}/revisions/{revisionId:int}/reject")]
    [RequirePermission("crm.material-rates", "approve")]
    public Task<ActionResult<MaterialRateRevisionResponse>> Reject(
        int catalogId,
        int revisionId,
        [FromBody] DecideMaterialRateRevisionRequest? request,
        CancellationToken ct) => Decide(catalogId, revisionId, request?.Note, false, ct);

    [HttpPost("{catalogId:int}/revisions/{revisionId:int}/retire")]
    [RequirePermission("crm.material-rates", "approve")]
    public async Task<ActionResult<MaterialRateRevisionResponse>> Retire(
        int catalogId,
        int revisionId,
        [FromBody] DecideMaterialRateRevisionRequest? request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<MaterialRateRevisionResponse>(async () =>
        {
            var result = await service.RetireAsync(catalogId, revisionId, request?.Note, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("material-rate-revision.retire", revisionId, result);
            return Ok(result);
        });
    }

    [HttpGet("{catalogId:int}/effective")]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<ActionResult<MaterialRateRevisionResponse>> GetEffective(
        int catalogId,
        [FromQuery] DateOnly? onDate,
        CancellationToken ct)
    {
        var result = await service.GetEffectiveAsync(catalogId, onDate ?? DateOnly.FromDateTime(DateTime.UtcNow), ct);
        return result is null ? NotFound() : Ok(result);
    }

    private async Task<ActionResult<MaterialRateRevisionResponse>> Decide(
        int catalogId,
        int revisionId,
        string? note,
        bool approve,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<MaterialRateRevisionResponse>(async () =>
        {
            var result = approve
                ? await service.ApproveAsync(catalogId, revisionId, note, userId.Value, ct)
                : await service.RejectAsync(catalogId, revisionId, note, userId.Value, ct);
            if (result is null) return NotFound();
            Audit(approve ? "material-rate-revision.approve" : "material-rate-revision.reject", revisionId, result);
            return Ok(result);
        });
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (MaterialRateOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private void Audit(string action, int id, object value) => audit.Log(new AuditEvent
    {
        Action = action,
        ResourceType = action.StartsWith("material-rate-catalog", StringComparison.Ordinal)
            ? EntityTypes.MaterialRateCatalog
            : EntityTypes.MaterialRateRevision,
        ResourceId = id.ToString(),
        Message = $"{action} #{id}.",
        NewValue = value,
    });

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(value, out var userId) ? userId : null;
    }
}
