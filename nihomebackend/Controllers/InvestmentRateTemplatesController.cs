using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

// Read-only library of the investment-rate forms: Procurement downloads one here, fills it in Excel
// and uploads it in the input-price work package.
[ApiController]
[Authorize]
[Route("api/investment-rate-templates")]
[Route("api/v1/investment-rate-templates")]
public sealed class InvestmentRateTemplatesController : ControllerBase
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    [RequirePermission("crm.material-rates", "view")]
    public IActionResult List() => Ok(InvestmentRateTemplateCatalog.All.Select(template => new
    {
        template.Code,
        template.NameKey,
        template.File,
    }));

    [HttpGet("{code}/file")]
    [RequirePermission("crm.material-rates", "view")]
    public IActionResult File(string code)
    {
        var template = InvestmentRateTemplateCatalog.Find(code);
        return template == null ? NotFound() : File(InvestmentRateTemplateCatalog.ReadFile(template), XlsxType, template.File);
    }
}
