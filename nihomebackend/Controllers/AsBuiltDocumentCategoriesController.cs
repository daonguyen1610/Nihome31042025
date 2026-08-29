using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

/// <summary>
/// CRUD endpoints for as-built document categories.
/// Viewing is open to authenticated users; management requires admin permission.
/// </summary>
[ApiController]
[Authorize]
[RequirePermission("construction.asbuilt-categories", "view")]
[Route("api/asbuilt-categories")]
[Route("api/v1/asbuilt-categories")]
public class AsBuiltDocumentCategoriesController(AsBuiltDocumentCategoryService svc) : ControllerBase
{
    /// <summary>
    /// List all as-built document categories.
    /// </summary>
    /// <param name="includeInactive">Include deactivated categories for historical document display.</param>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        => Ok(await svc.GetAllAsync(includeInactive));

    /// <summary>
    /// Get a single category by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await svc.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Create a new as-built document category.
    /// </summary>
    [HttpPost]
    [RequirePermission("construction.asbuilt-categories", "manage")]
    [RequireTranslationManageForLegacyFields]
    public async Task<IActionResult> Create([FromBody] UpsertAsBuiltDocumentCategoryRequest req)
    {
        try
        {
            var created = await svc.CreateAsync(req);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing as-built document category.
    /// </summary>
    [HttpPut("{id:int}")]
    [RequirePermission("construction.asbuilt-categories", "manage")]
    [RequireTranslationManageForLegacyFields]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertAsBuiltDocumentCategoryRequest req)
    {
        try
        {
            var updated = await svc.UpdateAsync(id, req);
            return updated == null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete an as-built document category (only if not in use).
    /// </summary>
    [HttpDelete("{id:int}")]
    [RequirePermission("construction.asbuilt-categories", "manage")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            return await svc.DeleteAsync(id) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
