using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Calcpad.Wpf
{
    public class TemplateServerUpdate
    {
        private readonly HttpClient _httpClient;
        private readonly string _serverBaseUrl;

        public TemplateServerUpdate(string serverBaseUrl)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _serverBaseUrl = serverBaseUrl.TrimEnd('/'); // Entferne ggf. das letzte "/"
        }

        /// <summary>
        /// Sendet einen Like für ein Template an den Server
        /// </summary>
        public async Task<bool> LikeTemplate(string templateId, string userPublicKey)
        {
            try
            {
                string url = $"{_serverBaseUrl}/api/templates/like";

                var likeData = new
                {
                    templateId = templateId,
                    userPublicKey = userPublicKey
                };

                string json = JsonSerializer.Serialize(likeData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fehler beim Liken des Templates: {ex.Message}");
                return false;
            }
        }
    }
}
