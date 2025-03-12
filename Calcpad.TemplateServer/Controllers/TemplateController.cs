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
    private readonly string templatePath = "wwwroot/templates"; // Verzeichnis mit JSON-Dateien

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

    [HttpGet("tree")]
    public async Task<IActionResult> GetTemplateTree()
    {
        if (!Directory.Exists(templatePath))
        {
            return NotFound("Template-Verzeichnis nicht gefunden.");
        }

        var tree = new TreeNode("Root"); // Der "Root"-Node dient nur als Wurzel, um den Baum zu organisieren.

        foreach (var file in Directory.GetFiles(templatePath, "*.json"))
        {
            var jsonString = await System.IO.File.ReadAllTextAsync(file);
            var template = JsonSerializer.Deserialize<TemplateData>(jsonString);

            if (template != null && template.Path != null && template.Title != null)
            {
                tree.AddPath(template.Path, template.Title); // Füge das Template zur Baumstruktur hinzu
            }
        }

        return Ok(tree.Children); // Rückgabe nur der direkten Kinder des Root-Nodes
    }

}

// Model für die JSON-Dateien
public class TemplateData
{
    public List<string> Path { get; set; }
    public string Title { get; set; }
}

// Baumstruktur-Klasse
public class TreeNode
{
    public string Name { get; set; }
    public List<TreeNode> Children { get; set; } = new List<TreeNode>();

    public TreeNode(string name)
    {
        Name = name;
    }

    public void AddPath(List<string> path, string title)
    {
        var currentNode = this;
        foreach (var part in path)
        {
            var existingNode = currentNode.Children.FirstOrDefault(n => n.Name == part);
            if (existingNode == null)
            {
                existingNode = new TreeNode(part);
                currentNode.Children.Add(existingNode);
            }
            currentNode = existingNode;
        }
        currentNode.Children.Add(new TreeNode(title)); // Letzter Knoten = Title
    }
}

// Hilfsklasse für Like-Anfragen
public class LikeRequest
    {
        public string UserPublicKey { get; set; }
    }



