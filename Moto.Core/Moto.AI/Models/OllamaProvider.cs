// Moto.Core/AI/Providers/OllamaProvider.cs
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
    /// Provider Ollama pour les modèles locaux.
    /// Priorité : toujours préféré car gratuit et hors-ligne.
    /// </summary>
    public class OllamaProvider : IAiProvider
    {
        private static readonly HttpClient _http = new HttpClient();
        private AiProviderConfig _config = AiProviderConfig.DefaultOllama();

        public string Name => "Ollama";
        public AiProviderType Type => AiProviderType.Ollama;
        public bool IsAvailable { get; private set; }

        public void Configure(AiProviderConfig config)
        {
            _config = config ?? AiProviderConfig.DefaultOllama();
        }

        /// <summary>
        /// Vérifie si Ollama tourne localement.
        /// </summary>
        public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(5000);

                var response = await _http.GetAsync(
                    $"{_config.EndpointUrl.TrimEnd('/')}/api/tags",
                    cts.Token);

                IsAvailable = response.IsSuccessStatusCode;
                return IsAvailable;
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
            var startTime = DateTime.UtcNow;

            try
            {
                var payload = new
                {
                    model = _config.ModelName,
                    prompt = BuildPrompt(request),
                    stream = false,
                    options = new
                    {
                        temperature = request.Temperature > 0 ? request.Temperature : _config.Temperature,
                        num_predict = request.MaxTokens > 0 ? request.MaxTokens : _config.MaxTokens
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_config.TimeoutMs);

                var response = await _http.PostAsync(
                    $"{_config.EndpointUrl.TrimEnd('/')}/api/generate",
                    content,
                    cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    return new AiCompletionResult
                    {
                        Success = false,
                        ProviderName = Name,
                        Error = $"Ollama error: {response.StatusCode}"
                    };
                }

                var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(responseBody);

                var resultContent = doc.RootElement.TryGetProperty("response", out var resp)
                    ? resp.GetString() ?? string.Empty
                    : string.Empty;

                return new AiCompletionResult
                {
                    Success = true,
                    Content = resultContent,
                    ProviderName = Name,
                    ModelUsed = _config.ModelName,
                    Latency = DateTime.UtcNow - startTime
                };
            }
            catch (OperationCanceledException)
            {
                return new AiCompletionResult
                {
                    Success = false,
                    ProviderName = Name,
                    Error = "Ollama timeout."
                };
            }
            catch (Exception ex)
            {
                return new AiCompletionResult
                {
                    Success = false,
                    ProviderName = Name,
                    Error = $"Ollama exception: {ex.Message}"
                };
            }
        }

        public async Task<AiCompletionResult> GenerateCodeAsync(
            string prompt,
            string language,
            string existingCode,
            CancellationToken cancellationToken = default)
        {
            var request = new AiCompletionRequest
            {
                SystemPrompt = $"Tu es un générateur de code {language}. Réponds uniquement avec le code, sans explication.",
                Prompt = prompt,
                Context = existingCode,
                Temperature = 0.2,
                MaxTokens = _config.MaxTokens
            };

            return await CompleteAsync(request, cancellationToken);
        }

        public async Task<AiCompletionResult> ExplainCodeAsync(
            string code,
            string language,
            CancellationToken cancellationToken = default)
        {
            var request = new AiCompletionRequest
            {
                SystemPrompt = $"Tu es un professeur de programmation {language}. Explique le code simplement.",
                Prompt = $"Explique ce code :\n\n{code}",
                Temperature = 0.5
            };

            return await CompleteAsync(request, cancellationToken);
        }

        public async Task<AiCompletionResult> FixCodeAsync(
            string code,
            string errors,
            CancellationToken cancellationToken = default)
        {
            var request = new AiCompletionRequest
            {
                SystemPrompt = "Tu es un expert en correction de code. Corrige les erreurs et retourne uniquement le code corrigé.",
                Prompt = $"Corrige ce code :\n\n{code}\n\nErreurs :\n{errors}",
                Temperature = 0.1
            };

            return await CompleteAsync(request, cancellationToken);
        }

        private string BuildPrompt(AiCompletionRequest request)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            {
                sb.AppendLine(request.SystemPrompt);
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(request.Context))
            {
                sb.AppendLine("Contexte :");
                sb.AppendLine(request.Context);
                sb.AppendLine();
            }

            sb.AppendLine(request.Prompt);

            return sb.ToString();
        }
    }
}
