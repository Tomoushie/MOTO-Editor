using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Performance;
using Moto.Core.Settings;
using Moto.Core.Logging;
using Xunit;

namespace Moto.Tests;

/// <summary>
/// Item 61 — Vérifie le backpressure : 5 chargements simultanés,
/// seuls N slots (défaut 3) acquis immédiatement, les autres attendent.
/// </summary>
public class BackpressureTests
{
    private readonly StructuredLogCollector _log = new();

    [Fact]
    public async Task FiveSimultaneousLoads_ShouldThrottleToConfiguredSlots()
    {
        var settings = SettingsEngine.Shared;
        int allowedSlots = settings.Shared.Ai.Advanced.MaxConcurrentPrefetch.Value;

        var governor = new AdaptivePrefetchService(settings, _log);

        int peakConcurrent = 0;
        int current = 0;
        var gate = new object();

        var tasks = Enumerable.Range(0, 5).Select(i => governor.RunPrefetchAsync(async ct =>
        {
            lock (gate) { current++; peakConcurrent = Math.Max(peakConcurrent, current); }
            _log.Debug("BackpressureTest", $"Slot acquis pour modèle {i}", new { current });
            await Task.Delay(100, ct);
            lock (gate) { current--; }
        })).ToArray();

        await Task.WhenAll(tasks);

        _log.Info("BackpressureTest", "Pic de concurrence", new { peakConcurrent, allowedSlots });
        Assert.True(peakConcurrent <= allowedSlots,
            $"Le pic {peakConcurrent} dépasse les {allowedSlots} slots autorisés");
    }
}
