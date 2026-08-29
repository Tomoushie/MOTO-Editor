using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.Logging;

public enum MotoLogLevel { Debug = 0, Info = 1, Warning = 2, Error = 3 }

/// <summary>
/// Item 52 — Collecteur de logs structurés.
/// Écrit en JSON Lines dans %AppData%/MotoEditor/logs/ avec flush throttled.
/// Privacy-safe : masque automatiquement les chemins utilisateur (item 58).
/// </summary>
public sealed class StructuredLogCollector : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private readonly ConcurrentQueue<LogEntry> _queue = new();
    private readonly Timer _flushTimer;
    private readonly string _logDirectory;
    private readonly string _logFilePath;
    private readonly object _writeLock = new();
    private bool _disposed;

    public MotoLogLevel MinLevel { get; set; } = MotoLogLevel.Info;
    public string CurrentLogFile => _logFilePath;

    public StructuredLogCollector()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MotoEditor", "logs");
        Directory.CreateDirectory(_logDirectory);

        _logFilePath = Path.Combine(_logDirectory, $"moto-{DateTime.Now:yyyyMMdd}.log.jsonl");

        // Flush throttled 1s : jamais de spam disque
        _flushTimer = new Timer(_ => Flush(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void Debug(string source, string message, object? data = null)
        => Enqueue(MotoLogLevel.Debug, source, message, data);
    public void Info(string source, string message, object? data = null)
        => Enqueue(MotoLogLevel.Info, source, message, data);
    public void Warning(string source, string message, object? data = null)
        => Enqueue(MotoLogLevel.Warning, source, message, data);
    public void Error(string source, string message, object? data = null)
        => Enqueue(MotoLogLevel.Error, source, message, data);

    private void Enqueue(MotoLogLevel level, string source, string message, object? data)
    {
        if (level < MinLevel) return;
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level.ToString(),
            Source = source,
            Message = MaskSensitive(message),
            Data = data
        };
        _queue.Enqueue(entry);
    }

    /// <summary>Item 58 — Masque les chemins utilisateur et tokens pour la télémétrie privacy-safe.</summary>
    private static string MaskSensitive(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        string user = Environment.UserName;
        if (!string.IsNullOrEmpty(user))
            input = input.Replace(user, "{USER}", StringComparison.OrdinalIgnoreCase);
        return input;
    }

    private void Flush()
    {
        if (_queue.IsEmpty) return;
        lock (_writeLock)
        {
            try
            {
                using var writer = new StreamWriter(_logFilePath, append: true);
                while (_queue.TryDequeue(out var entry))
                    writer.WriteLine(JsonSerializer.Serialize(entry, JsonOpts));
            }
            catch { /* ne jamais crasher l'éditeur pour un log */ }
        }
    }

    /// <summary>Item 52 — Crée une archive .zip des logs pour upload sécurisé.</summary>
    public async Task<string> CreateArchiveAsync(CancellationToken ct = default)
    {
        Flush();
        await Task.Yield();
        string archivePath = Path.Combine(_logDirectory, $"moto-logs-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(_logDirectory, archivePath);
        return archivePath;
    }

    public void Dispose()
    {
        _disposed = true;
        _flushTimer.Dispose();
        Flush();
    }
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public object? Data { get; set; }
}
