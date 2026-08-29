using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.DevOps;

public sealed class PerfMetrics
{
    public double StartupTimeMs { get; set; }
    public double PeakMemoryMb { get; set; }
    public double TokensPerSecond { get; set; }
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Items 92 + 101 — CI perf gate + alerting de régression.
/// Échoue si startup/mémoire dépasse le seuil ; alerte si dérive > seuil %.
/// </summary>
public sealed class PerfGateService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private PerfMetrics? _baseline;

    public event Action<string>? PerfAlertRaised;

    public PerfGateService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public PerfMetrics CaptureCurrentMetrics()
    {
        var proc = Process.GetCurrentProcess();
        return new PerfMetrics
        {
            StartupTimeMs = (DateTime.Now - proc.StartTime).TotalMilliseconds,
            PeakMemoryMb = proc.PeakWorkingSet64 / (1024.0 * 1024.0)
        };
    }

    /// <summary>Item 92 — Vérifie les seuils (utilisé en CI pour fail le PR).</summary>
    public bool PassesGate(PerfMetrics metrics)
    {
        if (!_settings.Shared.DevOps.PerfGateEnabled.Value) return true;

        bool startupOk = metrics.StartupTimeMs <= _settings.Shared.DevOps.StartupTimeThresholdMs.Value;
        bool memoryOk = metrics.PeakMemoryMb <= _settings.Shared.DevOps.MemoryThresholdMb.Value;

        if (!startupOk || !memoryOk)
            _log.Warning("PerfGate", "Seuils dépassés", new { metrics.StartupTimeMs, metrics.PeakMemoryMb });

        return startupOk && memoryOk;
    }

    public void SetBaseline(PerfMetrics baseline) => _baseline = baseline;

    /// <summary>Item 101 — Alerte si régression au-delà du seuil %.</summary>
    public void CheckRegression(PerfMetrics current)
    {
        if (_baseline is null) return;
        double threshold = _settings.Shared.DevOps.PerfRegressionThresholdPercent.Value;

        double startupDrift = ((current.StartupTimeMs - _baseline.StartupTimeMs) / _baseline.StartupTimeMs) * 100;
        double memoryDrift = ((current.PeakMemoryMb - _baseline.PeakMemoryMb) / _baseline.PeakMemoryMb) * 100;

        if (startupDrift > threshold || memoryDrift > threshold)
        {
            string msg = $"Régression perf : startup {startupDrift:+0.0}%, mémoire {memoryDrift:+0.0}%";
            _log.Warning("PerfGate", msg);
            PerfAlertRaised?.Invoke(msg);
        }
    }
}
