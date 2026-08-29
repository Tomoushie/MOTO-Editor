// Moto.Core/AI/Embedded/InferenceThrottler.cs
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Limite le débit d'inférence selon le budget alloué.
/// Regroupe les requêtes en batches pour optimiser le GPU.
/// </summary>
public sealed class InferenceThrottler : IDisposable
{
    private readonly SemaphoreSlim _requestSemaphore;
    private readonly ConcurrentQueue<PendingRequest> _queue = new();
    private readonly Timer _batchTimer;
    private ResourceBudget _currentBudget;
    private DateTime _lastRequestTime = DateTime.UtcNow;
    private int _requestCount;
    private DateTime _windowStart = DateTime.UtcNow;

    public static InferenceThrottler? Instance { get; private set; }

    /// <summary>Nombre de requêtes en attente.</summary>
    public int PendingCount => _queue.Count;

    /// <summary>Nombre de requêtes traitées dans la fenêtre actuelle.</summary>
    public int CurrentWindowCount => _requestCount;

    /// <summary>Événement quand une requête est throttled.</summary>
    public event Action<int>? OnThrottled;

    public InferenceThrottler(ResourceBudget initialBudget)
    {
        _currentBudget = initialBudget.Clone();
        _requestSemaphore = new SemaphoreSlim(initialBudget.MaxThreads, initialBudget.MaxThreads);
        Instance = this;

        // Timer de batch : traite les requêtes en attente toutes les 100 ms
        _batchTimer = new Timer(ProcessBatch, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Acquiert une slot d'inférence (bloquant si budget épuisé).
    /// </summary>
    public async Task<IDisposable> AcquireAsync(CancellationToken ct = default)
    {
        // Vérifie le rate limit
        var now = DateTime.UtcNow;
        if ((now - _windowStart).TotalSeconds >= 1.0)
        {
            _requestCount = 0;
            _windowStart = now;
        }

        if (_requestCount >= _currentBudget.MaxRequestsPerSecond)
        {
            OnThrottled?.Invoke(_requestCount);
            // Attend la prochaine fenêtre
            var delay = TimeSpan.FromSeconds(1.0 - (now - _windowStart).TotalSeconds);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }

        // Acquiert le sémaphore (limite les threads concurrents)
        await _requestSemaphore.WaitAsync(ct);
        _requestCount++;
        _lastRequestTime = DateTime.UtcNow;

        return new InferenceSlot(this);
    }

    /// <summary>
    /// Met à jour le budget (appelé par le governor).
    /// </summary>
    public void UpdateBudget(ResourceBudget newBudget)
    {
        _currentBudget = newBudget.Clone();

        // Ajuste le sémaphore
        var currentCount = _requestSemaphore.CurrentCount;
        var diff = newBudget.MaxThreads - currentCount;
        if (diff > 0)
            _requestSemaphore.Release(diff);
    }

    private void ProcessBatch(object? state)
    {
        // TODO: Implémenter le batching intelligent
        // Regroupe les requêtes similaires pour optimiser le GPU
    }

    private void ReleaseSlot()
    {
        try { _requestSemaphore.Release(); }
        catch { /* Already released */ }
    }

    public void Dispose()
    {
        _batchTimer?.Dispose();
        _requestSemaphore?.Dispose();
    }

    private class InferenceSlot : IDisposable
    {
        private readonly InferenceThrottler _throttler;
        private bool _disposed;

        public InferenceSlot(InferenceThrottler throttler)
        {
            _throttler = throttler;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _throttler.ReleaseSlot();
                _disposed = true;
            }
        }
    }
}
