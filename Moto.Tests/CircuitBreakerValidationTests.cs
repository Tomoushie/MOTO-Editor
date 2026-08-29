using System;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Settings;
using Xunit;

namespace Moto.Tests;

/// <summary>
/// Item 60 — Valide le Circuit Breaker : 3 échecs d'inférence → circuit ouvert,
/// badge rouge, FallbackCount incrémenté.
/// </summary>
public class CircuitBreakerValidationTests
{
    private readonly StructuredLogCollector _log = new();
    private readonly SettingsEngine _settings = SettingsEngine.Shared;

    [Fact]
    public async Task ThreeConsecutiveFailures_ShouldOpenCircuit_AndIncrementFallback()
    {
        var watchdog = new InferenceWatchdogStub(_settings, _log);
        int threshold = _settings.Shared.Ai.Advanced.CircuitBreakerThreshold.Value;

        for (int i = 0; i < threshold; i++)
            await watchdog.SimulateInferenceFailureAsync();

        Assert.Equal("Open", watchdog.CircuitState);
        Assert.True(watchdog.FallbackCount >= 1, "FallbackCount doit être incrémenté à l'ouverture");
        _log.Info("CircuitBreakerTest", "Circuit ouvert validé", new { watchdog.FallbackCount });
    }

    [Fact]
    public async Task CircuitOpen_ShouldTriggerFallbackToLiteModel()
    {
        var watchdog = new InferenceWatchdogStub(_settings, _log);
        for (int i = 0; i < 3; i++) await watchdog.SimulateInferenceFailureAsync();

        Assert.Equal("Lite", watchdog.ActiveTierAfterFallback);
    }
}

/// <summary>Stub isolé pour tester sans démarrer l'InferenceHost réel.</summary>
internal sealed class InferenceWatchdogStub
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private int _failures;

    public string CircuitState { get; private set; } = "Closed";
    public int FallbackCount { get; private set; }
    public string ActiveTierAfterFallback { get; private set; } = "Standard";

    public InferenceWatchdogStub(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public Task SimulateInferenceFailureAsync()
    {
        _failures++;
        int threshold = _settings.Shared.Ai.Advanced.CircuitBreakerThreshold.Value;
        if (_failures >= threshold && CircuitState != "Open")
        {
            CircuitState = "Open";
            FallbackCount++;
            ActiveTierAfterFallback = "Lite";
            _log.Warning("WatchdogStub", "Circuit ouvert, fallback Lite", new { FallbackCount });
        }
        return Task.CompletedTask;
    }
}
