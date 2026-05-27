using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize(Roles = "SUPER_ADMIN,ADMIN")]
[Route("api/processes")]
[Route("api/v1/processes")]
public class ProcessesController(ProcessService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await svc.GetAllGroupedAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertProcessRequest req)
    {
        var result = await svc.CreateAsync(req);
        return Created("", result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertProcessRequest req)
    {
        var result = await svc.UpdateAsync(id, req);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        return await svc.DeleteAsync(id) ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/assets")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAsset(
        int id,
        [FromForm] IFormFile? file,
        [FromForm] string type,
        [FromForm] string? displayName,
        [FromForm] int? sortOrder,
        CancellationToken cancellationToken)
    {
        if (file == null)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        if (!Enum.TryParse<ProcessAssetType>(type, true, out var assetType))
        {
            return BadRequest(new { message = "Invalid process asset type" });
        }

        try
        {
            var result = await svc.AddAssetAsync(id, assetType, file, displayName, sortOrder, cancellationToken);
            return result == null ? NotFound() : Created("", result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{processId:int}/assets/{assetId:int}")]
    public async Task<IActionResult> DeleteAsset(int processId, int assetId)
    {
        return await svc.DeleteAssetAsync(processId, assetId) ? NoContent() : NotFound();
    }
}
