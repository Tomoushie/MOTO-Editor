using Microsoft.Extensions.Logging;
using Moto.Core.AI.Internal;
using Moto.Core.Settings;

namespace Moto.Core.Services;

/// <summary>
/// Coordonne les mitigations automatiques des risques majeurs.
/// OOM → fallback modèle léger, Corruption → vérification SHA256, Téléchargement → resume.
/// </summary>
public sealed class RiskMitigationService
{
    private readonly ILogger<RiskMitigationService> _logger;
    private readonly SettingsEngine _settings;
    private readonly MetricsCollectorService _metrics;
    private readonly AiWatchdogService _watchdog;

    public RiskMitigationService(
        ILogger<RiskMitigationService> logger,
        SettingsEngine settings,
        MetricsCollectorService metrics,
        AiWatchdogService watchdog)
    {
        _logger = logger;
        _settings = settings;
        _metrics = metrics;
        _watchdog = watchdog;

        // S'abonner aux alertes
        _metrics.AlertTriggered += OnAlertTriggered;
    }

    private async void OnAlertTriggered(MetricAlert alert)
    {
        switch (alert.Type)
        {
            case AlertType.RamPressure:
                await MitigateRamPressureAsync();
                break;

            case AlertType.ThermalCritical:
                await MitigateThermalAsync();
                break;

            case AlertType.WatchdogFailures:
                await MitigateWatchdogFailuresAsync();
                break;

            case AlertType.OomDetected:
                await MitigateOomAsync();
                break;
        }
    }

    /// <summary>Mitigation RAM : bascule quantization Q4 → Q3.</summary>
    private async Task MitigateRamPressureAsync()
    {
        _logger.LogWarning("Mitigation RAM : bascule quantization Q4 → Q3");

        var switcher = GetQuantizationSwitcher();
        if (switcher != null)
        {
            await switcher.SwitchToLevelAsync(QuantizationLevel.Q3);
        }
    }

    /// <summary>Mitigation thermique : bascule tier Lite.</summary>
    private async Task MitigateThermalAsync()
    {
        _logger.LogWarning("Mitigation thermique : bascule tier Lite");

        var governor = GetAdaptiveResourceGovernor();
        if (governor != null)
        {
            await governor.SwitchToTierAsync(Moto.Core.Performance.PerformanceTier.Lite);
        }
    }

    /// <summary>Mitigation Watchdog : vérifie le circuit breaker.</summary>
    private async Task MitigateWatchdogFailuresAsync()
    {
        _logger.LogWarning("Mitigation Watchdog : vérification circuit breaker");

        var state = await _watchdog.GetStateAsync("inference-host");
        if (state == WatchdogState.CircuitOpen)
        {
            _logger.LogError("Circuit breaker ouvert — fallback vers modèle léger");
            await FallbackToLightModelAsync();
        }
    }

    /// <summary>Mitigation OOM : décharge le modèle et fallback.</summary>
    private async Task MitigateOomAsync()
    {
        _logger.LogError("Mitigation OOM : décharge modèle + fallback léger");

        var engine = GetEmbeddedLlmEngine();
        if (engine != null)
        {
            engine.UnloadModel();
            await FallbackToLightModelAsync();
        }
    }

    /// <summary>Fallback vers un modèle léger (500M).</summary>
    private async Task FallbackToLightModelAsync()
    {
        _settings.Set("ai.embedded.forcedTier", "lite");
        _logger.LogInformation("Fallback vers tier Lite (modèle 500M)");
    }

    // Getters pour les services (à adapter selon votre DI)
    private QuantizationSwitcher? GetQuantizationSwitcher() => null;
    private Moto.Core.Performance.AdaptiveResourceGovernor? GetAdaptiveResourceGovernor() => null;
    private EmbeddedLlmEngine? GetEmbeddedLlmEngine() => null;
}
