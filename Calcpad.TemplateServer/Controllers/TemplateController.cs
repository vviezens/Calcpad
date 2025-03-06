using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Calcpad.Shared;

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
    public async Task<IActionResult> SaveTemplate(string fileName, [FromBody] Template template)
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

    [HttpPost("{fileName}/like")]
    public async Task<IActionResult> LikeTemplate(string fileName, [FromBody] LikeRequest likeRequest)
    {
        if (likeRequest == null || string.IsNullOrEmpty(likeRequest.UserPublicKey))
        {
            return BadRequest("Invalid request. User public key is required.");
        }

        var templateJson = await _templateService.LoadTemplateAsync(fileName);
        if (templateJson == null)
        {
            return NotFound("Template not found.");
        }

        var templateData = JsonSerializer.Deserialize<Template>(templateJson);
        if (templateData == null)
        {
            return BadRequest("Invalid template format.");
        }

        // Prüfen, ob der User bereits geliked hat
        if (templateData.Likes_users.Contains(likeRequest.UserPublicKey))
        {
            return BadRequest("User has already liked this template.");
        }

        // Like hinzufügen
        templateData.Likes_total++;
        templateData.Likes_users.Add(likeRequest.UserPublicKey);

        // Speichern der aktualisierten Datei
        await _templateService.SaveTemplateAsync(fileName, templateData);

        return Ok(new { message = "Template successfully liked!", likes = templateData.Likes_total });
    }

    // Hilfsklasse für Like-Anfragen
    public class LikeRequest
    {
        public string UserPublicKey { get; set; }
    }

}

