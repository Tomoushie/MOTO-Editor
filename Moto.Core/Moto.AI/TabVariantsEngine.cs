// Moto.Core/AI/TabVariantsEngine.cs
// Suggestions de variantes de code déclenchées par TAB.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.AI
{
    public sealed class CodeVariant
    {
        public string Description { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public double Confidence { get; init; }
    }

    /// <summary>
    /// Génère des variantes de code contextuelles via Ollama.
    /// Déclenché par TAB ou Ctrl+TAB.
    /// </summary>
    public sealed class TabVariantsEngine
    {
        private readonly HttpClient _http;
        private readonly ILogger<TabVariantsEngine> _logger;
        private readonly string _ollamaUrl;
        private readonly string _model;

        public TabVariantsEngine(
            ILogger<TabVariantsEngine> logger,
            string ollamaUrl = "http://localhost:11434",
            string model = "llama3.1")
        {
            _logger = logger;
            _ollamaUrl = ollamaUrl;
            _model = model;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// Génère des variantes de code basées sur le contexte.
        /// </summary>
        public async Task<IReadOnlyList<CodeVariant>> GenerateVariantsAsync(
            string currentCode,
            string filePath,
            string projectStructure,
            CancellationToken ct = default)
        {
            var variants = new List<CodeVariant>();

            try
            {
                var fileName = Path.GetFileName(filePath);
                var prompt = BuildPrompt(currentCode, fileName, projectStructure);

                var requestBody = new
                {
                    model = _model,
                    prompt = prompt,
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync($"{_ollamaUrl}/api/generate", content, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

                if (result?.Response != null)
                {
                    variants = ParseVariants(result.Response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TabVariants] Erreur génération");
            }

            return variants;
        }

        private static string BuildPrompt(string code, string fileName, string projectStructure)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return $"You are in file '{fileName}'. The project structure is:\n{projectStructure}\n\n" +
                       $"Suggest 3 code snippets that would be useful here. " +
                       $"Format: VARIANT 1: description\\n```\\ncode\\n```\\n\\nVARIANT 2: ...";
            }

            return $"Given this code in '{fileName}':\n```\n{code}\n```\n\n" +
                   $"Project structure:\n{projectStructure}\n\n" +
                   $"Suggest 3 alternative implementations or improvements. " +
                   $"Format: VARIANT 1: description\\n```\\ncode\\n```\\n\\nVARIANT 2: ...";
        }

        private static List<CodeVariant> ParseVariants(string response)
        {
            var variants = new List<CodeVariant>();
            var sections = response.Split(new[] { "VARIANT" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var section in sections)
            {
                var lines = section.Split('\n');
                var description = "";
                var code = "";
                var inCodeBlock = false;

                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("```"))
                    {
                        inCodeBlock = !inCodeBlock;
                        continue;
                    }

                    if (inCodeBlock)
                    {
                        code += line + "\n";
                    }
                    else if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(line))
                    {
                        description = line.Trim().TrimStart(':').Trim();
                    }
                }

                if (!string.IsNullOrWhiteSpace(code))
                {
                    variants.Add(new CodeVariant
                    {
                        Description = description,
                        Code = code.Trim(),
                        Confidence = 0.8
                    });
                }
            }

            return variants;
        }

        private sealed class OllamaResponse
        {
            public string? Response { get; set; }
        }
    }
}
