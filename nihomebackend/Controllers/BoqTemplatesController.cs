using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

// Read-only library of the customer-format BOQ templates: Procurement downloads a form here,
// fills it in Excel and uploads it in the input-price work package.
[ApiController]
[Authorize]
[Route("api/boq-templates")]
[Route("api/v1/boq-templates")]
public sealed class BoqTemplatesController : ControllerBase
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    [RequirePermission("crm.material-rates", "view")]
    public IActionResult List() => Ok(ProjectBoqTemplateCatalog.All.Select(template => new
    {
        template.Code,
        template.NameKey,
        template.File,
        Sheets = ProjectBoqTemplateCatalog.Outline(template)
            .Select(sheet => new { sheet.Name, sheet.IsData, sheet.ItemCount }),
    }));

    [HttpGet("{code}/file")]
    [RequirePermission("crm.material-rates", "view")]
    public IActionResult File(string code)
    {
        var template = ProjectBoqTemplateCatalog.Find(code);
        return template == null ? NotFound() : File(ProjectBoqTemplateCatalog.ReadFile(template), XlsxType, template.File);
    }

    [HttpGet("{code}/sheets/{sheet}")]
    [RequirePermission("crm.material-rates", "view")]
    public IActionResult Sheet(string code, string sheet)
    {
        var template = ProjectBoqTemplateCatalog.Find(code);
        var outline = template == null ? null : ProjectBoqTemplateCatalog.Outline(template)
            .FirstOrDefault(item => string.Equals(item.Name, sheet, StringComparison.OrdinalIgnoreCase));
        return outline == null ? NotFound() : Ok(outline);
    }
}
