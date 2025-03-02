using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Core;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace Calcpad.Wpf
{
    public class TemplateClient
    {
        private readonly HttpClient _httpClient;

        public TemplateClient()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30) // Timeout auf 30 Sekunden setzen
            };
        }

        public async Task<List<Template>> GetTemplatesAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"⚠️ Fehler beim Abrufen der Daten: {response.StatusCode}");
                    return new List<Template>();
                }

                var content = await response.Content.ReadAsStringAsync();

                // Debugging-Fenster für Server-Antwort
                //ShowTextWindow("Server Response Content Debug", content);

                Console.WriteLine($"🔍 Server-Antwort:\n{content}");
                Console.WriteLine($"🧐 Ist JSON? {IsJson(content)}");
                Console.WriteLine($"🧐 Ist YAML? {IsYaml(content)}");

                if (string.IsNullOrWhiteSpace(content))
                {
                    Console.WriteLine("⚠️ Leere Antwort erhalten.");
                    return new List<Template>();
                }

                // 📌 Format automatisch erkennen
                if (IsJson(content))
                {
                    Console.WriteLine("📄 Erkanntes Format: JSON");
                    //ShowTextWindow("📄 JSON Debug", content);
                    return DeserializeJson(content);
                }
                else if (IsYaml(content))
                {
                    Console.WriteLine("📜 Erkanntes Format: YAML");
                    ShowTextWindow("📜 YAML Debug", content);
                    return DeserializeYaml(content);
                }
                else
                {
                    Console.WriteLine("⚠️ Unbekanntes Format – kann nicht verarbeitet werden.");
                    ShowTextWindow("⚠️ Fehler", "Unbekanntes Datenformat empfangen.");
                    return new List<Template>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Abrufen der Templates: {ex.Message}");
                ShowTextWindow("❌ Fehler", $"Fehler: {ex.Message}");
                return new List<Template>();
            }
        }

        private bool IsJson(string content)
        {
            content = content.Trim();
            return (content.StartsWith("{") && content.EndsWith("}")) ||
                   (content.StartsWith("[") && content.EndsWith("]"));
        }

        private bool IsYaml(string content)
        {
            content = content.Trim();

            // JSON darf nicht YAML überschreiben
            if (IsJson(content))
                return false;

            // Prüft YAML-typische Strukturen
            return content.Contains(":") || content.StartsWith("---");
        }

        private void ShowTextWindow(string title, string text)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var window = new Window
                {
                    Title = title,
                    Width = 600,
                    Height = 400,
                    Content = new Grid()
                };

                var textBox = new TextBox
                {
                    Text = text,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(10),
                    AcceptsReturn = true
                };

                ((Grid)window.Content).Children.Add(textBox);
                window.ShowDialog();
            });
        }

        private List<Template> DeserializeJson(string json)
        {
            try
            {
                var templates = JsonSerializer.Deserialize<List<Template>>(json);
                Console.WriteLine($"✅ JSON erfolgreich geladen ({templates?.Count ?? 0} Templates).");
                return templates ?? new List<Template>();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"❌ JSON-Fehler: {ex.Message}");
                ShowTextWindow("❌ JSON-Fehler", $"Fehlermeldung: {ex.Message}\n\nJSON-Daten:\n{json}");
                return new List<Template>();
            }
        }

        private List<Template> DeserializeYaml(string yaml)
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var parsedData = deserializer.Deserialize<Dictionary<string, List<Template>>>(yaml);

                if (parsedData == null || !parsedData.ContainsKey("Templates"))
                {
                    Console.WriteLine("⚠️ YAML Parsing fehlgeschlagen: Kein 'Templates'-Schlüssel gefunden.");
                    ShowTextWindow("❌ YAML-Fehler", $"Fehlender 'Templates'-Schlüssel.\n\nRohdaten:\n{yaml}");
                    return new List<Template>();
                }

                var templates = parsedData["Templates"];
                Console.WriteLine($"✅ {templates.Count} Templates aus YAML geladen.");
                Console.WriteLine($"📝 YAML Templates: {string.Join(", ", templates.Select(t => t.Title))}");

                return templates;
            }
            catch (YamlException ex)
            {
                string errorMessage = $"❌ YAML-Parsing Fehler in Zeile {ex.Start.Line}, Spalte {ex.Start.Column}:\n{ex.Message}";
                Console.WriteLine(errorMessage);
                ShowTextWindow("❌ YAML-Parsing Fehler", $"{errorMessage}\n\nRohdaten:\n{yaml}");
                return new List<Template>();
            }
            catch (Exception ex)
            {
                string errorMessage = $"❌ Allgemeiner YAML-Fehler: {ex.Message}";
                Console.WriteLine(errorMessage);
                ShowTextWindow("❌ YAML-Fehler", $"{errorMessage}\n\nRohdaten:\n{yaml}");
                return new List<Template>();
            }
        }
    }
}
