using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/contacts")]
[Route("api/v1/contacts")]
public class ContactsController(ContactMessageService svc) : ControllerBase
{
    /// <summary>Public: submit a contact message.</summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitContactRequest req)
    {
        var created = await svc.SubmitAsync(req);
        return StatusCode(201, created);
    }

    /// <summary>Admin: list all contact messages.</summary>
    [HttpGet]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> GetAll([FromQuery] bool? replied)
        => Ok(await svc.GetAllAsync(replied));

    /// <summary>Admin: get a single contact message.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await svc.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Admin: reply to a contact message (sends email).</summary>
    [HttpPost("{id:int}/reply")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyContactRequest req)
    {
        var result = await svc.ReplyAsync(id, req);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Admin: mark a contact as replied without sending email.</summary>
    [HttpPatch("{id:int}/mark-replied")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> MarkReplied(int id)
    {
        var result = await svc.MarkRepliedAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Admin: delete a contact message.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
    public async Task<IActionResult> Delete(int id)
        => await svc.DeleteAsync(id) ? NoContent() : NotFound();
}
