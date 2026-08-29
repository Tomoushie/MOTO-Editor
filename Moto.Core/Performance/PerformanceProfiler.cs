using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Moto.Core.Performance;

/// <summary>
/// Profiler intégré : mesure CPU, RAM, GC, temps d'exécution.
/// Exporte les métriques pour le dashboard perf.
/// </summary>
public sealed class PerformanceProfiler
{
    private readonly ConcurrentDictionary<string, MetricEntry> _metrics = new();
    private readonly Process _currentProcess;
    private readonly Timer _samplingTimer;

    public static PerformanceProfiler Instance { get; private set; } = null!;

    public PerformanceProfiler()
    {
        _currentProcess = Process.GetCurrentProcess();
        Instance = this;

        // Échantillonne toutes les 2 secondes
        _samplingTimer = new Timer(SampleMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Mesure le temps d'exécution d'une opération.
    /// </summary>
    public async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> operation)
    {
        var sw = Stopwatch.StartNew();
        var result = await operation();
        sw.Stop();

        RecordMetric(operationName, sw.ElapsedMilliseconds, "ms");
        return result;
    }

    /// <summary>
    /// Mesure le temps d'exécution d'une opération void.
    /// </summary>
    public async Task MeasureAsync(string operationName, Func<Task> operation)
    {
        await MeasureAsync(operationName, async () => { await operation(); return 0; });
    }

    /// <summary>
    /// Enregistre une métrique personnalisée.
    /// </summary>
    public void RecordMetric(string name, double value, string unit)
    {
        _metrics.AddOrUpdate(name,
            new MetricEntry { Name = name, Value = value, Unit = unit, Timestamp = DateTime.UtcNow },
            (_, existing) =>
            {
                existing.Value = value;
                existing.Timestamp = DateTime.UtcNow;
                existing.SampleCount++;
                return existing;
            });
    }

    /// <summary>
    /// Échantillonne les métriques système (CPU, RAM, GC).
    /// </summary>
    private void SampleMetrics(object? state)
    {
        try
        {
            _currentProcess.Refresh();

            RecordMetric("cpu_usage", _currentProcess.TotalProcessorTime.TotalMilliseconds, "ms");
            RecordMetric("ram_working_set", _currentProcess.WorkingSet64 / 1024.0 / 1024.0, "MB");
            RecordMetric("ram_private_memory", _currentProcess.PrivateMemorySize64 / 1024.0 / 1024.0, "MB");
            RecordMetric("gc_gen0", GC.CollectionCount(0), "collections");
            RecordMetric("gc_gen1", GC.CollectionCount(1), "collections");
            RecordMetric("gc_gen2", GC.CollectionCount(2), "collections");
            RecordMetric("gc_total_memory", GC.GetTotalMemory(false) / 1024.0 / 1024.0, "MB");
            RecordMetric("thread_count", _currentProcess.Threads.Count, "threads");
            RecordMetric("handle_count", _currentProcess.HandleCount, "handles");
        }
        catch
        {
            // Ignore les erreurs de sampling
        }
    }

    /// <summary>
    /// Exporte toutes les métriques en JSON.
    /// </summary>
    public string ExportMetrics()
    {
        var metrics = _metrics.Values.ToList();
        return JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Retourne les métriques actuelles.
    /// </summary>
    public IReadOnlyDictionary<string, MetricEntry> GetMetrics() => _metrics;

    public void Dispose()
    {
        _samplingTimer?.Dispose();
    }
}

public class MetricEntry
{
    public string Name { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public int SampleCount { get; set; } = 1;
}
