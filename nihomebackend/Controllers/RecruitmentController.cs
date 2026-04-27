using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/recruitment")]
[Route("api/v1/recruitment")]
[AllowAnonymous]
public class RecruitmentController(RecruitmentMetadataService metadataService) : ControllerBase
{
    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata([FromQuery] string lang = "vi")
    {
        var metadata = await metadataService.GetAsync(lang);
        return Ok(metadata);
    }
}
