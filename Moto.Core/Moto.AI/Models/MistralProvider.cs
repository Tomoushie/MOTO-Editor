// Moto.Core/AI/Providers/MistralProvider.cs
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.AI.Models;

namespace Moto.Core.AI.Providers
{
    /// <summary>
    /// Provider Mistral AI.
    /// </summary>
    public class MistralProvider : IAiProvider
    {
        private static readonly HttpClient _http = new HttpClient();
        private AiProviderConfig _config = AiProviderConfig.DefaultMistral();

        public string Name => "Mistral";
        public AiProviderType Type => AiProviderType.Mistral;
        public bool IsAvailable { get; private set; }

        public void Configure(AiProviderConfig config)
        {
            _config = config ?? AiProviderConfig.DefaultMistral();
        }

        public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            IsAvailable = !string.IsNullOrWhiteSpace(_config.ApiKey);
            return IsAvailable;
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
                    Error = "Clé API Mistral non configurée."
                };
            }

            var startTime = DateTime.UtcNow;

            try
            {
                var payload = new
                {
                    model = _config.ModelName,
                    messages = new[]
                    {
                        new { role = "system", content = request.SystemPrompt },
                        new { role = "user", content = BuildPrompt(request) }
                    },
                    temperature = request.Temperature > 0 ? request.Temperature : _config.Temperature,
                    max_tokens = request.MaxTokens > 0 ? request.MaxTokens : _config.MaxTokens
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_config.EndpointUrl}/chat/completions")
                {
                    Content = content
                };
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_config.TimeoutMs);

                var response = await _http.SendAsync(httpRequest, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    return new AiCompletionResult
                    {
                        Success = false,
                        ProviderName = Name,
                        Error = $"Mistral error: {response.StatusCode}"
                    };
                }

                var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(responseBody);

                var resultContent = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
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
                    Error = $"Mistral exception: {ex.Message}"
                };
            }
        }

        public Task<AiCompletionResult> GenerateCodeAsync(
            string prompt, string language, string existingCode,
            CancellationToken cancellationToken = default)
        {
            return CompleteAsync(new AiCompletionRequest
            {
                SystemPrompt = $"Tu es un générateur de code {language}.",
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
                Prompt = $"Explique ce code :\n\n{code}",
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
