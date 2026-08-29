// Moto.Core/AI/Providers/AnthropicProvider.cs
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.AI.Models;

namespace Moto.Core.AI.Providers
{
    /// <summary>
    /// Provider Anthropic (Claude).
    /// </summary>
    public class AnthropicProvider : IAiProvider
    {
        private static readonly HttpClient _http = new HttpClient();
        private AiProviderConfig _config = AiProviderConfig.DefaultAnthropic();

        public string Name => "Anthropic";
        public AiProviderType Type => AiProviderType.Anthropic;
        public bool IsAvailable { get; private set; }

        public void Configure(AiProviderConfig config)
        {
            _config = config ?? AiProviderConfig.DefaultAnthropic();
        }

        public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                IsAvailable = false;
                return false;
            }

            try
            {
                // Anthropic n'a pas d'endpoint health simple.
                // On considère disponible si la clé est configurée.
                IsAvailable = true;
                return true;
            }
            catch
            {
                IsAvailable = false;
                return false;
            }
        }

        public async Task<AiCompletionResult> CompleteAsync(
            AiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                return new AiCompletionResult
                {
                    Success = false,
                    ProviderName = Name,
                    Error = "Clé API Anthropic non configurée."
                };
            }

            var startTime = DateTime.UtcNow;

            try
            {
                var payload = new
                {
                    model = _config.ModelName,
                    max_tokens = request.MaxTokens > 0 ? request.MaxTokens : _config.MaxTokens,
                    system = request.SystemPrompt,
                    messages = new[]
                    {
                        new { role = "user", content = BuildPrompt(request) }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_config.EndpointUrl}/messages")
                {
                    Content = content
                };
                httpRequest.Headers.Add("x-api-key", _config.ApiKey);
                httpRequest.Headers.Add("anthropic-version", "2023-06-01");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_config.TimeoutMs);

                var response = await _http.SendAsync(httpRequest, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                    return new AiCompletionResult
                    {
                        Success = false,
                        ProviderName = Name,
                        Error = $"Anthropic error {response.StatusCode}: {errorBody}"
                    };
                }

                var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(responseBody);

                var resultContent = doc.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? string.Empty;

                return new AiCompletionResult
                {
                    Success = true,
                    Content = resultContent,
                    ProviderName = Name,
                    ModelUsed = _config.ModelName,
                    Latency = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
                return new AiCompletionResult
                {
                    Success = false,
                    ProviderName = Name,
                    Error = $"Anthropic exception: {ex.Message}"
                };
            }
        }

        public Task<AiCompletionResult> GenerateCodeAsync(
            string prompt, string language, string existingCode,
            CancellationToken cancellationToken = default)
        {
            return CompleteAsync(new AiCompletionRequest
            {
                SystemPrompt = $"Tu es un générateur de code {language}. Réponds uniquement avec le code.",
                Prompt = prompt,
                Context = existingCode,
                Temperature = 0.2
            }, cancellationToken);
        }

        public Task<AiCompletionResult> ExplainCodeAsync(
            string code, string language,
            CancellationToken cancellationToken = default)
        {
            return CompleteAsync(new AiCompletionRequest
            {
                SystemPrompt = $"Tu es un professeur de programmation {language}.",
                Prompt = $"Explique ce code simplement :\n\n{code}",
                Temperature = 0.5
            }, cancellationToken);
        }

        public Task<AiCompletionResult> FixCodeAsync(
            string code, string errors,
            CancellationToken cancellationToken = default)
        {
            return CompleteAsync(new AiCompletionRequest
            {
                SystemPrompt = "Tu es un expert en correction de code.",
                Prompt = $"Corrige ce code :\n\n{code}\n\nErreurs :\n{errors}",
                Temperature = 0.1
            }, cancellationToken);
        }

        private string BuildPrompt(AiCompletionRequest request)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(request.Context))
                sb.AppendLine($"Contexte :\n{request.Context}\n");
            sb.AppendLine(request.Prompt);
            return sb.ToString();
        }
    }
}
