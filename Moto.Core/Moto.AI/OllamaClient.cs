// AI/OllamaClient.cs
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Moto.Editor.AI
{
    /// <summary>
    /// Client Ollama local.
    /// Permet à MOTO Editor d'utiliser des modèles locaux sans dépendance cloud.
    /// </summary>
    public class OllamaClient
    {
        private static readonly HttpClient Http = new HttpClient();

        /// <summary>
        /// Adresse du serveur Ollama.
        /// </summary>
        public string Endpoint { get; set; } = "http://127.0.0.1:11434";

        /// <summary>
        /// Modèle local utilisé par MOTO AI.
        /// </summary>
        public string Model { get; set; } = "qwen2.5-coder:7b";

        /// <summary>
        /// Génère une réponse locale via Ollama.
        /// Utilisé pour la complétion, les suggestions et les petites générations.
        /// </summary>
        public async Task<string> GenerateAsync(string prompt)
        {
            var request = new
            {
                model = Model,
                prompt = prompt,
                stream = false
            };

            var json = JsonSerializer.Serialize(request);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync($"{Endpoint.TrimEnd('/')}/api/generate", content);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            return doc.RootElement.TryGetProperty("response", out var value)
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }
    }
}
