using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/users")]
[Route("api/v1/users")]
[Authorize]
[RequirePermission("users", "view")]
public class UsersController(UserService svc) : ControllerBase
{
    private const string CreateScope = "users.admin.create";
    private const string UpdateScope = "users.admin.update";

    [HttpGet]
    public async Task<ActionResult<UserListResponse>> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null)
        => Ok(await svc.GetListAsync(skip, take, search, role));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDetailResponse>> GetById(int id)
    {
        var user = await svc.GetByIdAsync(id);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [RequirePermission("users", "manage")]
    [Idempotency(CreateScope)]
    public async Task<ActionResult<UserDetailResponse>> Create(
        [FromBody] CreateUserRequest req)
    {
        var created = await svc.CreateAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("users", "manage")]
    [Idempotency(UpdateScope)]
    public async Task<ActionResult<UserDetailResponse>> Update(
        int id,
        [FromBody] UpdateUserRequest req)
    {
        var updated = await svc.UpdateAsync(id, req, GetCurrentUserId());
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpPatch("{id:int}/toggle-active")]
    [RequirePermission("users", "manage")]
    public async Task<ActionResult<UserDetailResponse>> ToggleActive(int id)
    {
        var updated = await svc.ToggleActiveAsync(id, GetCurrentUserId());
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("users", "manage")]
    public async Task<IActionResult> Delete(int id)
        => await svc.DeleteAsync(id, GetCurrentUserId()) ? NoContent() : NotFound();

    [HttpDelete("{id:int}/hard")]
    [RequirePermission("users", "manage")]
    public async Task<IActionResult> HardDelete(int id)
        => await svc.HardDeleteAsync(id, GetCurrentUserId()) ? NoContent() : NotFound();

    [HttpGet("roles")]
    public async Task<ActionResult<RoleCatalogResponse>> GetRoles()
        => Ok(await svc.GetRoleCatalogAsync());

    private int GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(raw, out var id) ? id : 0;
    }
}
