using Moto.Core.AI.Internal.Models;
using Moto.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Routeur vers le moteur embarqué via l'InferenceHost isolé.
/// S'intègre dans MotoAiKernel comme provider alternatif à Ollama.
/// </summary>
public sealed class EmbeddedModelRouter
{
    private readonly ILogger<EmbeddedModelRouter> _logger;
    private readonly SettingsEngine _settings;
    private readonly InferenceHostClient _client;

    public EmbeddedModelRouter(
        ILogger<EmbeddedModelRouter> logger,
        SettingsEngine settings,
        InferenceHostClient client)
    {
        _logger = logger;
        _settings = settings;
        _client = client;
    }

    /// <summary>
    /// Indique si le moteur embarqué est activé dans les paramètres.
    /// </summary>
    public bool IsEnabled =>
        _settings.GetBool("ai.embedded.enabled", defaultValue: false);

    /// <summary>
    /// Vérifie si le moteur embarqué peut traiter cette requête.
    /// </summary>
    public async Task<bool> CanHandleAsync(CancellationToken ct = default)
    {
        if (!IsEnabled) return false;

        try
        {
            var status = await _client.GetStatusAsync(ct);
            return status.IsHostAlive;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "InferenceHost injoignable.");
            return false;
        }
    }

    /// <summary>
    /// Exécute une complétion via le moteur embarqué.
    /// </summary>
    public async Task<AiResponse?> CompleteAsync(
        string prompt,
        int maxTokens = 256,
        CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("Moteur embarqué désactivé, skip.");
            return null;
        }

        var modelId = _settings.GetString("ai.embedded.modelChoice", "phi-3-mini");
        var tier = _settings.GetString("ai.embedded.forcedTier", "auto");

        try
        {
            // Assure que le modèle est chargé
            await _client.EnsureModelLoadedAsync(modelId, tier, ct);

            // Inférence
            var result = await _client.InferAsync(modelId, prompt, maxTokens, ct);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de l'inférence embarquée.");
            return null;
        }
    }
}
