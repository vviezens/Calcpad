using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

[ApiController]
[Route("api/templates")]
public class TemplateController : ControllerBase
{
    private readonly TemplateService _templateService;

    public TemplateController(TemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet("{fileName}")]
    public async Task<IActionResult> GetTemplate(string fileName)
    {
        var template = await _templateService.LoadTemplateAsync(fileName);
        return template != null ? Ok(template) : NotFound();
    }

    [HttpPost("{fileName}")]
    public async Task<IActionResult> SaveTemplate(string fileName, [FromBody] object template)
    {
        if (!fileName.EndsWith(".json") && !fileName.EndsWith(".yaml") && !fileName.EndsWith(".yml"))
        {
            return BadRequest("Ungültiges Dateiformat. Unterstützt werden nur .json und .yaml");
        }

        await _templateService.SaveTemplateAsync(fileName, template);
        return Ok(new { message = "Template gespeichert!" });
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTemplates()
    {
        var templates = await _templateService.LoadAllTemplatesAsync();
        return Ok(templates);
    }
}
