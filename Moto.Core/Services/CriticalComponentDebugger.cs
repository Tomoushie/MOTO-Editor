using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Moto.Core.Services;

/// <summary>
/// Active les logs détaillés et le mode debug pour les composants critiques.
/// Usage : appeler EnableDebug() au démarrage sur les machines de test.
/// </summary>
public sealed class CriticalComponentDebugger : IDisposable
{
    private readonly ILogger<CriticalComponentDebugger> _logger;
    private readonly Dictionary<string, ComponentDebugState> _components = new();
    private readonly string _debugLogDir;
    private bool _debugEnabled;

    public CriticalComponentDebugger(ILogger<CriticalComponentDebugger> logger)
    {
        _logger = logger;
        _debugLogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoEditor", "DebugLogs");
        Directory.CreateDirectory(_debugLogDir);
    }

    /// <summary>Active le mode debug pour tous les composants critiques.</summary>
    public void EnableDebug()
    {
        _debugEnabled = true;

        var criticalComponents = new[]
        {
            "InferenceHost",
            "LayeredModelLoader",
            "SpeculativeDecoder",
            "Watchdog",
            "ModelDownloader",
            "QuantizationSwitcher",
            "ThermalSensor"
        };

        foreach (var component in criticalComponents)
        {
            _components[component] = new ComponentDebugState
            {
                Name = component,
                Enabled = true,
                LogFile = Path.Combine(_debugLogDir, $"{component}_{DateTime.UtcNow:yyyyMMdd}.log")
            };
        }

        _logger.LogInformation("Mode debug activé pour {Count} composants critiques.", criticalComponents.Length);
    }

    /// <summary>Log un événement pour un composant critique.</summary>
    public void LogEvent(string component, string eventType, object? data = null)
    {
        if (!_debugEnabled || !_components.TryGetValue(component, out var state))
            return;

        var entry = new DebugLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Component = component,
            EventType = eventType,
            Data = data != null ? JsonSerializer.Serialize(data) : null
        };

        var json = JsonSerializer.Serialize(entry);

        try
        {
            File.AppendAllText(state.LogFile, json + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec écriture log debug pour {Component}.", component);
        }
    }

    /// <summary>Collecte tous les logs debug pour export.</summary>
    public async Task<string> ExportDebugLogsAsync()
    {
        var exportPath = Path.Combine(_debugLogDir, $"debug_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");

        try
        {
            var files = Directory.GetFiles(_debugLogDir, "*.log");
            System.IO.Compression.ZipFile.CreateFromDirectory(_debugLogDir, exportPath);

            _logger.LogInformation("Logs debug exportés: {Path} ({Count} fichiers)", exportPath, files.Length);
            return exportPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec export logs debug.");
            return string.Empty;
        }
    }

    public void Dispose() => GC.SuppressFinalize(this);
}

public class ComponentDebugState
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public string LogFile { get; set; } = "";
}

public class DebugLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Component { get; set; } = "";
    public string EventType { get; set; } = "";
    public string? Data { get; set; }
}
