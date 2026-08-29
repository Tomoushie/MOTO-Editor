using Microsoft.Extensions.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Mode "Performance Max" : active les 3 optimisations simultanément.
/// - Memory-mapped inference
/// - Parallel decoding
/// - KV-cache compression
/// </summary>
public sealed class PerformanceMaxMode
{
    private readonly ILogger<PerformanceMaxMode> _logger;
    private readonly SettingsEngine _settings;

    public PerformanceMaxMode(
        ILogger<PerformanceMaxMode> logger,
        SettingsEngine settings)
    {
        _logger = logger;
        _settings = settings;
    }

    /// <summary>
    /// Active le mode Performance Max.
    /// </summary>
    public void Enable()
    {
        _settings.Set("ai.embedded.useMemoryMapping", true);
        _settings.Set("ai.embedded.enableParallelDecoding", true);
        _settings.Set("ai.embedded.enableKvCacheCompression", true);
        _settings.Set("ai.embedded.enableQuantizationSwitching", true);

        _logger.LogInformation("Mode Performance Max activé.");
    }

    /// <summary>
    /// Désactive le mode Performance Max (retour aux valeurs par défaut).
    /// </summary>
    public void Disable()
    {
        _settings.Set("ai.embedded.useMemoryMapping", false);
        _settings.Set("ai.embedded.enableParallelDecoding", false);
        _settings.Set("ai.embedded.enableKvCacheCompression", false);
        _settings.Set("ai.embedded.enableQuantizationSwitching", false);

        _logger.LogInformation("Mode Performance Max désactivé.");
    }

    /// <summary>
    /// Vérifie si le mode Performance Max est actif.
    /// </summary>
    public bool IsEnabled =>
        _settings.GetBool("ai.embedded.useMemoryMapping", false) &&
        _settings.GetBool("ai.embedded.enableParallelDecoding", false) &&
        _settings.GetBool("ai.embedded.enableKvCacheCompression", false) &&
        _settings.GetBool("ai.embedded.enableQuantizationSwitching", false);
}
