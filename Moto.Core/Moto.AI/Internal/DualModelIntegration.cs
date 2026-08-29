using Microsoft.Extensions.Logging;
using Moto.Core.AI.Internal.Models;
using Moto.Core.Settings;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Intégration dual-modèle : point d'entrée unique pour l'inférence.
/// Route automatiquement entre Ollama et le modèle embarqué.
/// </summary>
public sealed class DualModelIntegration
{
    private readonly ILogger<DualModelIntegration> _logger;
    private readonly SettingsEngine _settings;
    private readonly MotoAiKernel _kernel;
    private readonly EmbeddedModelRouter _embeddedRouter;
    private readonly OllamaClient _ollamaClient;

    public DualModelIntegration(
        ILogger<DualModelIntegration> logger,
        SettingsEngine settings,
        MotoAiKernel kernel,
        EmbeddedModelRouter embeddedRouter,
        OllamaClient ollamaClient)
    {
        _logger = logger;
        _settings = settings;
        _kernel = kernel;
        _embeddedRouter = embeddedRouter;
        _ollamaClient = ollamaClient;
    }

    /// <summary>
    /// Exécute une complétion en routant vers le meilleur provider.
    /// </summary>
    public async Task<AiResponse?> CompleteAsync(
        string prompt,
        int maxTokens = 256,
        CancellationToken ct = default)
    {
        // 1. Tentative Ollama (prioritaire)
        if (await _ollamaClient.IsAvailableAsync(ct))
        {
            _logger.LogDebug("Routing vers Ollama.");
            var ollamaResult = await _ollamaClient.CompleteAsync(prompt, maxTokens, ct);
            if (ollamaResult is not null) return ollamaResult;
        }

        // 2. Tentative modèle embarqué (fallback)
        if (_embeddedRouter.IsEnabled && await _embeddedRouter.CanHandleAsync(ct))
        {
            _logger.LogDebug("Routing vers modèle embarqué.");
            var embeddedResult = await _embeddedRouter.CompleteAsync(prompt, maxTokens, ct);
            if (embeddedResult is not null) return embeddedResult;
        }

        // 3. Aucun provider disponible
        _logger.LogWarning("Aucun provider IA disponible.");
        return null;
    }

    /// <summary>
    /// Retourne le provider actif.
    /// </summary>
    public async Task<string> GetActiveProviderAsync(CancellationToken ct = default)
    {
        if (await _ollamaClient.IsAvailableAsync(ct))
            return "ollama";

        if (_embeddedRouter.IsEnabled && await _embeddedRouter.CanHandleAsync(ct))
            return "embedded";

        return "none";
    }

    /// <summary>
    /// Statistiques du dual-model routing.
    /// </summary>
    public DualModelStats GetStats() => new()
    {
        OllamaAvailable = _ollamaClient.IsAvailableAsync(default).GetAwaiter().GetResult(),
        EmbeddedEnabled = _embeddedRouter.IsEnabled,
        ActiveProvider = GetActiveProviderAsync().GetAwaiter().GetResult()
    };
}

public class DualModelStats
{
    public bool OllamaAvailable { get; init; }
    public bool EmbeddedEnabled { get; init; }
    public string ActiveProvider { get; init; } = "none";
}

/// <summary>
/// Client Ollama minimal pour l'intégration dual-model.
/// </summary>
public sealed class OllamaClient
{
    private readonly HttpClient _httpClient;

    public OllamaClient()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("http://localhost:11434/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<AiResponse?> CompleteAsync(
        string prompt,
        int maxTokens,
        CancellationToken ct = default)
    {
        try
        {
            var request = new
            {
                model = "phi3:mini",
                prompt,
                stream = false,
                options = new { num_predict = maxTokens }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("http://localhost:11434/api/generate", content, ct);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var result = System.Text.Json.JsonSerializer.Deserialize<OllamaResponse>(responseJson);

            return new AiResponse
            {
                Content = result?.response ?? string.Empty,
                Provider = "ollama",
                LatencyMs = 0
            };
        }
        catch
        {
            return null;
        }
    }
}

public class OllamaResponse
{
    public string? response { get; set; }
    public bool done { get; set; }
}
