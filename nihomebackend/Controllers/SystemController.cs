using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController(TimeService timeService) : ControllerBase
{
    [HttpGet("health")]
    public ActionResult<HealthResponse> GetHealth()
    {
        return Ok(new HealthResponse
        {
            Name = "Nihome Backend",
            Environment = HttpContext.RequestServices
                .GetRequiredService<IHostEnvironment>()
                .EnvironmentName,
            Status = "Healthy",
            TimestampUtc = timeService.GetUtcNow()
        });
    }
}
