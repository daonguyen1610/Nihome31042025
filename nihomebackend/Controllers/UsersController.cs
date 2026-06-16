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
    [HttpGet]
    public async Task<ActionResult<UserListResponse>> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null)
    {
        try
        {
            return Ok(await svc.GetListAsync(skip, take, search, role));
        }
        catch (UserServiceException ex)
        {
            return ToErrorResult(ex);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDetailResponse>> GetById(int id)
    {
        var user = await svc.GetByIdAsync(id);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [RequirePermission("users", "manage")]
    public async Task<ActionResult<UserDetailResponse>> Create([FromBody] CreateUserRequest req)
    {
        try
        {
            var created = await svc.CreateAsync(req);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (UserServiceException ex)
        {
            return ToErrorResult(ex);
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("users", "manage")]
    public async Task<ActionResult<UserDetailResponse>> Update(int id, [FromBody] UpdateUserRequest req)
    {
        try
        {
            var updated = await svc.UpdateAsync(id, req, GetCurrentUserId());
            return updated == null ? NotFound() : Ok(updated);
        }
        catch (UserServiceException ex)
        {
            return ToErrorResult(ex);
        }
    }

    [HttpPatch("{id:int}/toggle-active")]
    [RequirePermission("users", "manage")]
    public async Task<ActionResult<UserDetailResponse>> ToggleActive(int id)
    {
        try
        {
            var updated = await svc.ToggleActiveAsync(id, GetCurrentUserId());
            return updated == null ? NotFound() : Ok(updated);
        }
        catch (UserServiceException ex)
        {
            return ToErrorResult(ex);
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("users", "manage")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            return await svc.DeleteAsync(id, GetCurrentUserId()) ? NoContent() : NotFound();
        }
        catch (UserServiceException ex)
        {
            return ToErrorResult(ex);
        }
    }

    [HttpGet("roles")]
    public async Task<ActionResult<RoleCatalogResponse>> GetRoles()
        => Ok(await svc.GetRoleCatalogAsync());

    private int GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(raw, out var id) ? id : 0;
    }

    private ActionResult ToErrorResult(UserServiceException ex)
        => ex.Error == UserServiceError.DuplicatePhoneNumber
            ? Conflict(new { message = ex.Message })
            : BadRequest(new { message = ex.Message });
}
