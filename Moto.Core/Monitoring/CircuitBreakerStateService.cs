using System;
using Moto.Core.Logging;

namespace Moto.Core.Monitoring;

/// <summary>
/// Item 64 — Source de vérité de l'état du Circuit Breaker, consommée par l'UI.
/// Découplé de l'InferenceWatchdog : celui-ci pousse les événements ici.
/// </summary>
public sealed class CircuitBreakerStateService
{
    private readonly StructuredLogCollector _log;
    private readonly object _lock = new();

    private int _openCountLast24h;
    private string _state = "Closed"; // Closed / Open / HalfOpen
    private int _fallbackCount;

    public string State { get { lock (_lock) return _state; } }
    public int OpenCountLast24h { get { lock (_lock) return _openCountLast24h; } }
    public int FallbackCount { get { lock (_lock) return _fallbackCount; } }

    public event EventHandler? StateChanged;

    public CircuitBreakerStateService(StructuredLogCollector log) => _log = log;

    /// <summary>Appelé par l'InferenceWatchdog à chaque ouverture de circuit.</summary>
    public void ReportCircuitOpen()
    {
        lock (_lock)
        {
            _openCountLast24h++;
            _state = "Open";
        }
        _log.Warning("CircuitBreaker", "Circuit OUVERT", new { OpenCountLast24h });
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReportCircuitClosed()
    {
        lock (_lock) _state = "Closed";
        _log.Info("CircuitBreaker", "Circuit refermé");
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReportFallback()
    {
        lock (_lock) _fallbackCount++;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reset quotidien (appelé par un timer ou au démarrage).</summary>
    public void ResetDailyCounters()
    {
        lock (_lock) _openCountLast24h = 0;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
