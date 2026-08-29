using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.DevOps;

/// <summary>
/// Item 98 — Telemetry privacy sandbox : collecte perf locale,
/// partage anonymisé opt-in uniquement.
/// </summary>
public sealed class TelemetryPrivacyService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly PerfGateService _perfGate;

    public TelemetryPrivacyService(SettingsEngine settings, StructuredLogCollector log, PerfGateService perfGate)
    {
        _settings = settings;
        _log = log;
        _perfGate = perfGate;
    }

    /// <summary>Crée un bundle anonymisé (sans chemin utilisateur, sans nom machine).</summary>
    public async Task<string?> CreateAnonymizedBundleAsync()
    {
        if (!_settings.Shared.DevOps.TelemetryPrivacySandbox.Value)
        {
            _log.Info("TelemetryPrivacy", "Sandbox désactivé par l'utilisateur.");
            return null;
        }

        var metrics = _perfGate.CaptureCurrentMetrics();
        var anonymized = new
        {
            timestamp = DateTime.UtcNow,
            startupMs = metrics.StartupTimeMs,
            memoryMb = metrics.PeakMemoryMb
            // Volontairement : PAS de MachineName, PAS de chemins, PAS d'identifiants.
        };

        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoEditor", "telemetry", $"anon-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(anonymized));

        _log.Info("TelemetryPrivacy", "Bundle anonymisé créé", new { path });
        await Task.CompletedTask;
        return path;
    }
}
