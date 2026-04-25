using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
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
}
