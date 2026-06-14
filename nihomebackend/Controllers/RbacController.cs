using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/admin/rbac")]
[Route("api/v1/admin/rbac")]
[Authorize]
public class RbacController(
    IRoleService roles,
    IPermissionService permissions) : ControllerBase
{
    private const string PermView = "rbac.roles.view";
    private const string PermManage = "rbac.roles.manage";

    [HttpGet("permissions")]
    public async Task<ActionResult<List<PermissionResponse>>> ListPermissions(CancellationToken ct)
    {
        if (!await RequirePermissionAsync(PermView, ct)) return Forbid();
        return Ok(await roles.ListPermissionsAsync(ct));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<RoleResponse>>> ListRoles(CancellationToken ct)
    {
        if (!await RequirePermissionAsync(PermView, ct)) return Forbid();
        return Ok(await roles.ListRolesAsync(ct));
    }

    [HttpGet("roles/{id:int}")]
    public async Task<ActionResult<RoleResponse>> GetRole(int id, CancellationToken ct)
    {
        if (!await RequirePermissionAsync(PermView, ct)) return Forbid();
        var role = await roles.GetRoleAsync(id, ct);
        return role == null ? NotFound() : Ok(role);
    }

    [HttpGet("roles/{id:int}/permissions")]
    public async Task<ActionResult<RolePermissionsResponse>> GetRolePermissions(int id, CancellationToken ct)
    {
        if (!await RequirePermissionAsync(PermView, ct)) return Forbid();
        var rp = await roles.GetRolePermissionsAsync(id, ct);
        return rp == null ? NotFound() : Ok(rp);
    }

    [HttpPut("roles/{id:int}")]
    public async Task<ActionResult<RoleResponse>> UpdateRole(int id, [FromBody] UpdateRoleRequest req, CancellationToken ct)
    {
        if (!await RequirePermissionAsync(PermManage, ct)) return Forbid();
        var actorId = GetCurrentUserId();
        if (actorId <= 0) return Unauthorized();

        var result = await roles.UpdateRoleAsync(id, req, actorId, ct);
        return MapWrite(result);
    }

    [HttpPut("roles/{id:int}/permissions")]
    public async Task<ActionResult<RolePermissionsResponse>> UpdateRolePermissions(
        int id, [FromBody] UpdateRolePermissionsRequest req, CancellationToken ct)
    {
        if (!await RequirePermissionAsync(PermManage, ct)) return Forbid();
        var actorId = GetCurrentUserId();
        if (actorId <= 0) return Unauthorized();

        var result = await roles.UpdateRolePermissionsAsync(id, req, actorId, ct);
        return MapWrite(result);
    }

    private ActionResult<T> MapWrite<T>(RoleWriteResult<T> result) where T : class => result.Status switch
    {
        RoleWriteStatus.Success => Ok(result.Value!),
        RoleWriteStatus.NotFound => NotFound(),
        RoleWriteStatus.ForbiddenSystemRole => StatusCode(StatusCodes.Status403Forbidden, new
        {
            error = "system_role_immutable",
            message = "System roles cannot be modified through the API.",
        }),
        RoleWriteStatus.ForbiddenEscalation => StatusCode(StatusCodes.Status403Forbidden, new
        {
            error = "privilege_escalation_blocked",
            message = "You cannot grant permissions you do not hold.",
            offending = result.OffendingCodes,
        }),
        RoleWriteStatus.InvalidPermissionCodes => BadRequest(new
        {
            error = "unknown_permission_codes",
            offending = result.OffendingCodes,
        }),
        RoleWriteStatus.InvalidRequest => BadRequest(new
        {
            error = "invalid_request",
            message = result.Error,
        }),
        _ => StatusCode(StatusCodes.Status500InternalServerError),
    };

    private async Task<bool> RequirePermissionAsync(string code, CancellationToken ct)
    {
        var uid = GetCurrentUserId();
        if (uid <= 0) return false;
        return await permissions.HasAsync(uid, code, ct);
    }

    private int GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(raw, out var id) ? id : 0;
    }
}
