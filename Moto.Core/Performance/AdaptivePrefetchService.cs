using System;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Performance;

/// <summary>
/// Item 58 — Préfetch adaptatif avec backpressure.
/// Ajuste dynamiquement le nombre de prefetch simultanés selon la charge I/O,
/// en complément du SemaphoreSlim de LayeredModelLoader.
/// </summary>
public sealed class AdaptivePrefetchService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly SemaphoreSlim _gate;
    private int _currentConcurrency;

    public AdaptivePrefetchService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
        _currentConcurrency = SettingsCatalog.Ai.Advanced.MaxConcurrentPrefetch.Value;
        _gate = new SemaphoreSlim(_currentConcurrency, _currentConcurrency);
    }

    public async Task RunPrefetchAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        if (!SettingsCatalog.Ai.Advanced.AdaptivePrefetchEnabled.Value)
        {
            await work(ct);
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            await work(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Rétrogradation adaptative : réduit la concurrence en cas de saturation.</summary>
    public void ReportIoSaturation()
    {
        int target = Math.Max(1, _currentConcurrency - 1);
        if (target != _currentConcurrency)
        {
            _currentConcurrency = target;
            _log.Warning("AdaptivePrefetch", "Saturation I/O : concurrence réduite", new { target });
        }
    }
}
