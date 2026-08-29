// Moto.Core/AI/Embedded/AiObservabilityService.cs
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Observabilité complète de l'IA embarquée :
/// - Métriques temps réel (RAM, CPU, queue, tokens/s, latence)
/// - Alertes automatiques (RAM > seuil, CPU > 85%)
/// - Logs structurés (load/unload, download, benchmark, mode changes)
/// - Export heatmap JSON
/// </summary>
public sealed class AiObservabilityService
{
    private readonly ConcurrentQueue<ObservabilityLog> _logs = new();
    private readonly ConcurrentDictionary<string, double> _metrics = new();
    private readonly string _logFilePath;
    private const int MaxLogEntries = 10_000;

    public static AiObservabilityService? Instance { get; private set; }

    public event Action<ObservabilityAlert>? OnAlert;

    public AiObservabilityService(string? logDirectory = null)
    {
        var dir = logDirectory ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoEditor", "logs");
        Directory.CreateDirectory(dir);
        _logFilePath = System.IO.Path.Combine(dir, $"ai-obs-{DateTime.Now:yyyyMMdd}.jsonl");
        Instance = this;
    }

    /// <summary>
    /// Enregistre une métrique.
    /// </summary>
    public void RecordMetric(string name, double value)
    {
        _metrics[name] = value;
        CheckAlerts(name, value);
    }

    /// <summary>
    /// Enregistre un événement structuré.
    /// </summary>
    public void LogEvent(string category, string message, object? data = null)
    {
        var entry = new ObservabilityLog
        {
            Timestamp = DateTime.UtcNow,
            Category = category,
            Message = message,
            Data = data
        };

        _logs.Enqueue(entry);
        TrimLogs();
        _ = AppendToDiskAsync(entry);
    }

    /// <summary>
    /// Exporte toutes les métriques en JSON (heatmap).
    /// </summary>
    public string ExportMetricsJson()
    {
        return JsonSerializer.Serialize(_metrics, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Exporte les logs récents en JSON.
    /// </summary>
    public string ExportLogsJson(int count = 100)
    {
        var recent = _logs.ToArray()[^Math.Min(count, _logs.Count)..];
        return JsonSerializer.Serialize(recent, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Snapshot complet pour le dashboard.
    /// </summary>
    public ObservabilitySnapshot GetSnapshot() => new()
    {
        Metrics = _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        RecentLogs = _logs.ToArray()[^Math.Min(20, _logs.Count)..],
        Timestamp = DateTime.UtcNow
    };

    private void CheckAlerts(string name, double value)
    {
        if (name == "inference_host_ram_mb" && value > 6144)
        {
            OnAlert?.Invoke(new ObservabilityAlert
            {
                Level = AlertLevel.Warning,
                Message = $"RAM InferenceHost élevée: {value:F0} MB",
                Metric = name,
                Value = value,
                Threshold = 6144
            });
        }

        if (name == "system_cpu_percent" && value > 85)
        {
            OnAlert?.Invoke(new ObservabilityAlert
            {
                Level = AlertLevel.Critical,
                Message = $"CPU système critique: {value:F1}%. Mode Emergency activé.",
                Metric = name,
                Value = value,
                Threshold = 85
            });
        }

        if (name == "inference_failure_rate" && value > 0.1)
        {
            OnAlert?.Invoke(new ObservabilityAlert
            {
                Level = AlertLevel.Warning,
                Message = $"Taux d'échec inférence: {value:P1}",
                Metric = name,
                Value = value,
                Threshold = 0.1
            });
        }
    }

    private void TrimLogs()
    {
        while (_logs.Count > MaxLogEntries)
        {
            _logs.TryDequeue(out _);
        }
    }

    private async Task AppendToDiskAsync(ObservabilityLog entry)
    {
        try
        {
            var json = JsonSerializer.Serialize(entry) + "\n";
            await File.AppendAllTextAsync(_logFilePath, json);
        }
        catch { /* Disk full, ignore */ }
    }
}

public class ObservabilityLog
{
    public DateTime Timestamp { get; set; }
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";
    public object? Data { get; set; }
}

public class ObservabilityAlert
{
    public AlertLevel Level { get; set; }
    public string Message { get; set; } = "";
    public string Metric { get; set; } = "";
    public double Value { get; set; }
    public double Threshold { get; set; }
}

public class ObservabilitySnapshot
{
    public Dictionary<string, double> Metrics { get; set; } = new();
    public ObservabilityLog[] RecentLogs { get; set; } = Array.Empty<ObservabilityLog>();
    public DateTime Timestamp { get; set; }
}

public enum AlertLevel { Info, Warning, Critical }
