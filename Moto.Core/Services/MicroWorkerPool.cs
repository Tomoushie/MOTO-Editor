using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Moto.Core.Services;

/// <summary>
/// Pool de micro-workers pour les tâches courtes (< 100ms).
/// Évite la surcharge du ThreadPool pour les opérations rapides.
/// </summary>
public sealed class MicroWorkerPool : IDisposable
{
    private readonly ILogger<MicroWorkerPool> _logger;
    private readonly ConcurrentQueue<MicroTask> _queue = new();
    private readonly Thread[] _workers;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _signal = new(0);
    private long _tasksProcessed;
    private long _tasksDropped;

    public MicroWorkerPool(
        ILogger<MicroWorkerPool> logger,
        int workerCount = 4)
    {
        _logger = logger;
        _workers = new Thread[workerCount];

        for (var i = 0; i < workerCount; i++)
        {
            _workers[i] = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"MicroWorker-{i}",
                Priority = ThreadPriority.BelowNormal
            };
            _workers[i].Start();
        }

        _logger.LogInformation("MicroWorkerPool démarré avec {Count} workers.", workerCount);
    }

    /// <summary>
    /// Soumet une tâche courte au pool.
    /// </summary>
    public void Submit(Func<Task> task, string? tag = null)
    {
        if (_cts.IsCancellationRequested)
        {
            Interlocked.Increment(ref _tasksDropped);
            return;
        }

        _queue.Enqueue(new MicroTask(task, tag));
        _signal.Release();
    }

    /// <summary>
    /// Soumet une tâche avec résultat.
    /// </summary>
    public Task<T> SubmitAsync<T>(Func<Task<T>> task, string? tag = null)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        Submit(async () =>
        {
            try
            {
                var result = await task();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, tag);

        return tcs.Task;
    }

    private void WorkerLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _signal.Wait(_cts.Token);

                if (_queue.TryDequeue(out var microTask))
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    microTask.Action().GetAwaiter().GetResult();
                    sw.Stop();

                    Interlocked.Increment(ref _tasksProcessed);

                    if (sw.ElapsedMilliseconds > 100)
                    {
                        _logger.LogWarning(
                            "Tâche micro-worker trop lente: {Tag} ({Elapsed}ms)",
                            microTask.Tag ?? "unknown",
                            sw.ElapsedMilliseconds);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans le micro-worker.");
            }
        }
    }

    /// <summary>
    /// Statistiques du pool.
    /// </summary>
    public MicroWorkerStats GetStats() => new()
    {
        TasksProcessed = Interlocked.Read(ref _tasksProcessed),
        TasksDropped = Interlocked.Read(ref _tasksDropped),
        QueueLength = _queue.Count,
        WorkerCount = _workers.Length
    };

    public void Dispose()
    {
        _cts.Cancel();
        _signal.Release(_workers.Length); // Réveille tous les workers
        foreach (var worker in _workers)
            worker.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
        _signal.Dispose();
        GC.SuppressFinalize(this);
    }
}

public record MicroTask(Func<Task> Action, string? Tag);

public class MicroWorkerStats
{
    public long TasksProcessed { get; init; }
    public long TasksDropped { get; init; }
    public int QueueLength { get; init; }
    public int WorkerCount { get; init; }
}
