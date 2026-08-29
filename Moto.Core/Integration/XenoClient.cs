// Integration/XenoClient.cs
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Moto.Editor.Integration
{
    /// <summary>
    /// Requête envoyée à XENO-SSS∞.
    /// MOTO Editor n'analyse pas le projet lui-même :
    /// il demande à XENO d'exécuter le pipeline complet.
    /// </summary>
    public class XenoRequest
    {
        public string WorkspacePath { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string Mode { get; set; } = "xeno-sss";
    }

    /// <summary>
    /// Réponse renvoyée par XENO-SSS∞.
    /// </summary>
    public class XenoResponse
    {
        public bool Success { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> Details { get; set; } = new List<string>();
        public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Client HTTP vers XENO-SSS∞ Host.
    /// Cela permet à MOTO Editor d'être un client, pas un moteur IA.
    /// </summary>
    public class HttpXenoClient
    {
        private static readonly HttpClient Http = new HttpClient();

        /// <summary>
        /// Adresse du host XENO-SSS∞.
        /// Exemple : http://127.0.0.1:8377
        /// </summary>
        public string Endpoint { get; set; } = "http://127.0.0.1:8377";

        /// <summary>
        /// Vérifie si XENO-SSS∞ est accessible.
        /// </summary>
        public async Task PingAsync()
        {
            using var response = await Http.GetAsync($"{Endpoint.TrimEnd('/')}/xeno/ping");
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Lance une opération XENO-SSS∞ sur un workspace.
        /// </summary>
        public async Task<XenoResponse> RunAsync(XenoRequest request)
        {
            var json = JsonSerializer.Serialize(request);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync($"{Endpoint.TrimEnd('/')}/xeno/run", content);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new XenoResponse
                {
                    Success = false,
                    Summary = $"XENO host error {(int)response.StatusCode}: {body}"
                };
            }

            return JsonSerializer.Deserialize<XenoResponse>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new XenoResponse
            {
                Success = false,
                Summary = "Empty XENO response."
            };
        }
    }
}
