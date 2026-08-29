// Integration/XenoBridge.cs
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Moto.Editor.Integration
{
    public enum XenoTask
    {
        FullPipeline,
        RefactorGlobal,
        GenerateModule,
        AutoFix,
        Validate,
        ArchitectureSuggestions
    }

    /// <summary>
    /// Requête envoyée à XENO-SSS∞.
    /// </summary>
    public class XenoTaskRequest
    {
        public string WorkspacePath { get; set; } = string.Empty;
        public string Task { get; set; } = string.Empty;
        public string Parameter { get; set; } = string.Empty;
    }

    /// <summary>
    /// Réponse renvoyée par XENO-SSS∞.
    /// </summary>
    public class XenoTaskResult
    {
        public bool Success { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> Details { get; set; } = new List<string>();
    }

    /// <summary>
    /// Contrat d'intégration avec XENO-SSS∞.
    /// </summary>
    public interface IXenoBridge
    {
        Task<XenoTaskResult> ExecuteAsync(XenoTaskRequest request);
    }

    /// <summary>
    /// Client HTTP vers le host XENO-SSS∞.
    /// MOTO Editor reste un client : il n'exécute pas lui-même le pipeline.
    /// </summary>
    public class HttpXenoBridge : IXenoBridge
    {
        private static readonly HttpClient Http = new HttpClient();

        public string Endpoint { get; set; } = "http://127.0.0.1:8377";

        public async Task<XenoTaskResult> ExecuteAsync(XenoTaskRequest request)
        {
            var json = JsonSerializer.Serialize(request);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync($"{Endpoint.TrimEnd('/')}/xeno/task", content);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new XenoTaskResult
                {
                    Success = false,
                    Summary = $"XENO host error {(int)response.StatusCode}: {body}"
                };
            }

            return JsonSerializer.Deserialize<XenoTaskResult>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new XenoTaskResult
            {
                Success = false,
                Summary = "Empty XENO response."
            };
        }
    }

    /// <summary>
    /// Mapping des tâches MOTO vers les commandes XENO-SSS∞.
    /// </summary>
    public static class XenoTasks
    {
        public static string ToTaskName(XenoTask task)
        {
            return task switch
            {
                XenoTask.FullPipeline => "run-full-pipeline",
                XenoTask.RefactorGlobal => "refactor-global",
                XenoTask.GenerateModule => "generate-module",
                XenoTask.AutoFix => "auto-fix",
                XenoTask.Validate => "validate",
                XenoTask.ArchitectureSuggestions => "architecture-suggestions",
                _ => "run-full-pipeline"
            };
        }
    }
}
