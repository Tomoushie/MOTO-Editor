using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Moto.Core.Services;

/// <summary>
/// Collecte les métriques critiques et déclenche des actions automatiques.
/// Seuils : RAM 85% → QuantizationSwitch Q3, Temp 80°C → alerte, 85°C → tier Lite.
/// </summary>
public sealed class MetricsCollectorService : IDisposable
{
    private readonly ILogger<MetricsCollectorService> _logger;
    private readonly System.Timers.Timer _timer;
    private readonly List<MetricsSnapshot> _history = new();
    private readonly object _lock = new();

    // Seuils configurables
    public double RamAlertThreshold { get; set; } = 0.85;
    public int TempAlertThresholdCelsius { get; set; } = 80;
    public int TempAutoTierThresholdCelsius { get; set; } = 85;
    public double LatencyP95AlertMultiplier { get; set; } = 1.30;

    // Événements d'alerte
    public event Action<MetricAlert>? AlertTriggered;

    public MetricsCollectorService(ILogger<MetricsCollectorService> logger)
    {
        _logger = logger;
        _timer = new System.Timers.Timer(5000); // Collecte toutes les 5s
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        CollectAndEvaluate();
    }

    /// <summary>Collecte les métriques actuelles et évalue les seuils.</summary>
    public void CollectAndEvaluate()
    {
        var snapshot = new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            RamUsagePercent = GetRamUsagePercent(),
            CpuTempCelsius = GetCpuTemperature(),
            InferenceHostWorkingSetMb = GetInferenceHostWorkingSetMb(),
            TokensPerSecond = GetCurrentTokensPerSecond(),
            LatencyP50Ms = GetLatencyPercentile(50),
            LatencyP95Ms = GetLatencyPercentile(95),
            LatencyP99Ms = GetLatencyPercentile(99),
            WatchdogFailureCount = GetWatchdogFailureCount(),
            ModelLoadTimeMs = GetModelLoadTimeMs()
        };

        lock (_lock)
        {
            _history.Add(snapshot);
            if (_history.Count > 720) _history.RemoveAt(0); // Garde 1h d'historique (720 * 5s)
        }

        EvaluateThresholds(snapshot);
    }

    private void EvaluateThresholds(MetricsSnapshot snapshot)
    {
        // 1. RAM > 85% → QuantizationSwitch Q3
        if (snapshot.RamUsagePercent > RamAlertThreshold)
        {
            TriggerAlert(new MetricAlert
            {
                Type = AlertType.RamPressure,
                Severity = AlertSeverity.Warning,
                Message = $"RAM à {snapshot.RamUsagePercent * 100:0}% — activation QuantizationSwitch Q3",
                Action = "quantization_switch_q3",
                Value = snapshot.RamUsagePercent
            });
        }

        // 2. Température > 80°C → alerte
        if (snapshot.CpuTempCelsius > TempAlertThresholdCelsius)
        {
            TriggerAlert(new MetricAlert
            {
                Type = AlertType.ThermalWarning,
                Severity = AlertSeverity.Warning,
                Message = $"Température {snapshot.CpuTempCelsius}°C — surveillance accrue",
                Action = "thermal_monitor",
                Value = snapshot.CpuTempCelsius
            });
        }

        // 3. Température > 85°C → auto-tier Lite
        if (snapshot.CpuTempCelsius > TempAutoTierThresholdCelsius)
        {
            TriggerAlert(new MetricAlert
            {
                Type = AlertType.ThermalCritical,
                Severity = AlertSeverity.Critical,
                Message = $"Température {snapshot.CpuTempCelsius}°C — bascule tier Lite",
                Action = "auto_tier_lite",
                Value = snapshot.CpuTempCelsius
            });
        }

        // 4. Latence p95 > +30% vs baseline
        var baseline = GetBaselineLatencyP95();
        if (baseline > 0 && snapshot.LatencyP95Ms > baseline * LatencyP95AlertMultiplier)
        {
            TriggerAlert(new MetricAlert
            {
                Type = AlertType.LatencyRegression,
                Severity = AlertSeverity.Warning,
                Message = $"Latence p95 à {snapshot.LatencyP95Ms}ms (+{((snapshot.LatencyP95Ms / baseline - 1) * 100):0}% vs baseline)",
                Action = "latency_investigation",
                Value = snapshot.LatencyP95Ms
            });
        }

        // 5. Watchdog > 3 échecs en 5 min
        if (snapshot.WatchdogFailureCount > 3)
        {
            TriggerAlert(new MetricAlert
            {
                Type = AlertType.WatchdogFailures,
                Severity = AlertSeverity.Critical,
                Message = $"Watchdog: {snapshot.WatchdogFailureCount} échecs en 5 min",
                Action = "watchdog_circuit_check",
                Value = snapshot.WatchdogFailureCount
            });
        }

        // 6. Model load > 10s
        if (snapshot.ModelLoadTimeMs > 10000)
        {
            TriggerAlert(new MetricAlert
            {
                Type = AlertType.SlowModelLoad,
                Severity = AlertSeverity.Warning,
                Message = $"Chargement modèle: {snapshot.ModelLoadTimeMs}ms (> 10s)",
                Action = "model_load_investigation",
                Value = snapshot.ModelLoadTimeMs
            });
        }
    }

    private void TriggerAlert(MetricAlert alert)
    {
        _logger.LogWarning("[ALERT] {Type}: {Message}", alert.Type, alert.Message);
        AlertTriggered?.Invoke(alert);
    }

    /// <summary>Exporte les métriques en JSON pour analyse CI.</summary>
    public async Task ExportMetricsAsync(string outputPath)
    {
        List<MetricsSnapshot> snapshotCopy;
        lock (_lock)
        {
            snapshotCopy = new List<MetricsSnapshot>(_history);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(snapshotCopy, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(outputPath, json);
        _logger.LogInformation("Métriques exportées: {Path} ({Count} snapshots)", outputPath, snapshotCopy.Count);
    }

    // Méthodes de collecte (à implémenter selon l'environnement réel)
    private static double GetRamUsagePercent()
    {
        var process = Process.GetCurrentProcess();
        var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return (double)process.WorkingSet64 / totalMemory;
    }

    private static int GetCpuTemperature() => 0; // À implémenter via ThermalSensor
    private static long GetInferenceHostWorkingSetMb() => 0; // À implémenter
    private static double GetCurrentTokensPerSecond() => 0; // À implémenter
    private static double GetLatencyPercentile(int percentile) => 0; // À implémenter
    private static int GetWatchdogFailureCount() => 0; // À implémenter
    private static double GetModelLoadTimeMs() => 0; // À implémenter
    private static double GetBaselineLatencyP95() => 0; // À implémenter (stocker la baseline)

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class MetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public double RamUsagePercent { get; set; }
    public int CpuTempCelsius { get; set; }
    public long InferenceHostWorkingSetMb { get; set; }
    public double TokensPerSecond { get; set; }
    public double LatencyP50Ms { get; set; }
    public double LatencyP95Ms { get; set; }
    public double LatencyP99Ms { get; set; }
    public int WatchdogFailureCount { get; set; }
    public double ModelLoadTimeMs { get; set; }
}

public class MetricAlert
{
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = "";
    public string Action { get; set; } = "";
    public double Value { get; set; }
}

public enum AlertType
{
    RamPressure,
    ThermalWarning,
    ThermalCritical,
    LatencyRegression,
    WatchdogFailures,
    SlowModelLoad,
    DownloadFailure,
    OomDetected
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
