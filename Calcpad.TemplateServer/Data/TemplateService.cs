using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class TemplateService
{
    private readonly string _templatePath = Path.Combine("wwwroot", "templates");

    public TemplateService()
    {
        if (!Directory.Exists(_templatePath))
            Directory.CreateDirectory(_templatePath);
    }

    public async Task SaveTemplateAsync(string fileName, object template)
    {
        string filePath = Path.Combine(_templatePath, fileName);

        if (fileName.EndsWith(".json"))
        {
            string json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }
        else if (fileName.EndsWith(".yaml") || fileName.EndsWith(".yml"))
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            string yaml = serializer.Serialize(template);
            await File.WriteAllTextAsync(filePath, yaml);
        }
    }

    public async Task<string?> LoadTemplateAsync(string fileName)
    {
        string filePath = Path.Combine(_templatePath, fileName);
        if (!File.Exists(filePath)) return null;

        return await File.ReadAllTextAsync(filePath);
    }

    public async Task<List<object>> LoadAllTemplatesAsync()
    {
        var templates = new List<object>();
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        // JSON-Dateien laden
        foreach (var filePath in Directory.GetFiles(_templatePath, "*.json"))
        {
            string json = await File.ReadAllTextAsync(filePath);
            var template = JsonSerializer.Deserialize<object>(json);
            templates.Add(template);
        }

        // YAML-Dateien laden
        foreach (var filePath in Directory.GetFiles(_templatePath, "*.yaml"))
        {
            string yaml = await File.ReadAllTextAsync(filePath);
            var template = deserializer.Deserialize<object>(yaml);
            templates.Add(template);
        }

        return templates;
    }
}
