using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController, Authorize]
[Route("api/material-rate-catalogs")]
[Route("api/v1/material-rate-catalogs")]
public sealed class BoqConfiguredWorkbooksController(BoqConfiguredWorkbookService workbooks, IMaterialRateService rates, IAuditLogger audit) : ControllerBase
{
    [HttpPut("{catalogId:int}/boq-configuration")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [RequirePermission("crm.material-rates", "manage")]
    public Task<IActionResult> Configure(int catalogId, ConfigureBoqRequest request, CancellationToken ct) => Run(async () =>
    {
        var result = await workbooks.ConfigureAsync(catalogId, request, UserId, ct);
        if (result == null) return NotFound();
        Log("boq.configuration", catalogId);
        return Ok(result);
    });

    [HttpGet("{catalogId:int}/configured-boq-template")]
    [RequirePermission("crm.material-rates", "view")]
    public Task<IActionResult> Template(int catalogId, [FromQuery] string language = "vi", CancellationToken ct = default) => Run(async () =>
    {
        if (!new[] { "vi", "en", "zh", "ja" }.Contains(language)) throw BoqConfigurationRules.Error("configuration");
        var bytes = await workbooks.TemplateAsync(catalogId, language, ct);
        return bytes == null ? NotFound() : File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BOQ-{catalogId}-{language}.xlsx");
    });

    [HttpPost("{catalogId:int}/configured-boq-preview"), Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [RequirePermission("crm.material-rates", "manage")]
    public Task<IActionResult> Preview(int catalogId, IFormFile? file, CancellationToken ct) => Run(async () =>
    {
        var result = await workbooks.PreviewAsync(catalogId, file, ct);
        return result == null ? NotFound() : Ok(result);
    });

    [HttpPut("{catalogId:int}/revisions/{revisionId:int}/configured-boq-rows")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [RequirePermission("crm.material-rates", "manage")]
    public Task<IActionResult> Save(int catalogId, int revisionId, SaveBoqRowsRequest request, CancellationToken ct) => Run(async () =>
    {
        var result = await rates.SaveConfiguredRowsAsync(catalogId, revisionId, request, UserId, ct: ct);
        if (result == null) return NotFound();
        Log("boq.rows", revisionId);
        return Ok(result);
    });

    [HttpPost("{catalogId:int}/revisions/{revisionId:int}/configured-boq-import"), Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [RequirePermission("crm.material-rates", "manage")]
    public Task<IActionResult> Import(int catalogId, int revisionId, IFormFile? file, [FromForm] string sourceHash,
        [FromForm] DateTime expectedUpdatedAt, CancellationToken ct) => Run(async () =>
    {
        var preview = await workbooks.PreviewAsync(catalogId, file, ct);
        if (preview == null) return NotFound();
        if (sourceHash != preview.SourceHash) throw BoqConfigurationRules.Error("changedFile");
        var result = await rates.SaveConfiguredRowsAsync(catalogId, revisionId, new SaveBoqRowsRequest
        { Rows = preview.Rows, ConfigurationHash = preview.ConfigurationHash, ExpectedUpdatedAt = expectedUpdatedAt }, UserId, Path.GetFileName(file!.FileName), sourceHash, ct);
        if (result == null) return NotFound();
        Log("boq.import", revisionId);
        return Ok(result);
    });

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private void Log(string action, int id) => audit.Log(new AuditEvent { Action = action, ResourceType = "MaterialRateCatalog", ResourceId = id.ToString() });
    private async Task<IActionResult> Run(Func<Task<IActionResult>> operation)
    {
        try { return await operation(); }
        catch (BoqConfigurationConflictException) { return Conflict(new { messageKey = "boqConfigured.error.conflict" }); }
        catch (MaterialRateOperationException error) { return BadRequest(new { message = error.Message, messageKey = error.MessageKey, context = error.Data["context"] }); }
    }
}
