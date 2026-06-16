using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Authorize(Roles = "SUPER_ADMIN,ADMIN")]
[Route("api/logos")]
[Route("api/v1/logos")]
public class LogosController(LogoService svc) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll() => Ok(await svc.GetAllGroupedAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertLogoRequest req)
    {
        var result = await svc.CreateAsync(req);
        return Created("", result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertLogoRequest req)
    {
        var result = await svc.UpdateAsync(id, req);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        return await svc.DeleteAsync(id) ? NoContent() : NotFound();
    }
}
