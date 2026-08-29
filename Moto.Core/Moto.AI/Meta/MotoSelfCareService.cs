using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Meta;

public sealed class ConfigDiagnostic
{
    public string Category { get; set; } = "";
    public string Issue { get; set; } = "";
    public string Suggestion { get; set; } = "";
}

public sealed class SelfAuditReport
{
    public double AverageResponseMs { get; set; }
    public int ErrorCount { get; set; }
    public int TotalCalls { get; set; }
    public DateTime AuditedAtUtc { get; set; } = DateTime.UtcNow;
    public string Summary { get; set; } = "";
}

/// <summary>
/// Bloc 8 — IA pour MOTO Editor lui-même (meta, 5 idées).
/// </summary>
public sealed class MotoSelfCareService
{
    private static readonly string AuditLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MotoEditor", ".moto", "self-audit.md");

    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly List<double> _responseTimesMs = new();
    private int _errorCount;

    public MotoSelfCareService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>Idée "Config doctor" — inspecte la config et propose une config propre.</summary>
    public IReadOnlyList<ConfigDiagnostic> DiagnoseConfig()
    {
        var diagnostics = new List<ConfigDiagnostic>();

        // Vérifie les paramètres incohérents
        if (_settings.Shared.Ai.Advanced.MaxConcurrentPrefetch.Value > 10)
        {
            diagnostics.Add(new ConfigDiagnostic
            {
                Category = "AI/Prefetch",
                Issue = "Concurrence prefetch très élevée",
                Suggestion = "Réduire à 3-5 pour éviter la saturation I/O."
            });
        }

        return diagnostics;
    }

    /// <summary>Idée "Crash post-mortem" — lit le log, propose hypothèse + action.</summary>
    public (string hypothesis, string action) AnalyzeCrashLog(string logContent)
    {
        if (logContent.Contains("OutOfMemory", StringComparison.OrdinalIgnoreCase))
            return ("Mémoire insuffisante lors de l'opération.",
                    "Désactiver les modèles lourds ou activer le mode Low-RAM.");

        if (logContent.Contains("NullReference", StringComparison.OrdinalIgnoreCase))
            return ("Référence null non gérée.",
                    "Vérifier le dernier plugin chargé ou désactiver le plugin suspect.");

        if (logContent.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            return ("Timeout d'une opération asynchrone.",
                    "Réduire les tâches d'arrière-plan ou augmenter le timeout.");

        return ("Cause non identifiée.", "Consulter les logs détaillés dans .moto/logs.");
    }

    /// <summary>Idée "Feature toggles" — propose de désactiver les features inutilisées.</summary>
    public IReadOnlyList<string> SuggestFeatureToggles(Dictionary<string, int> featureUsage)
    {
        var suggestions = new List<string>();
        foreach (var kv in featureUsage)
        {
            if (kv.Value == 0)
                suggestions.Add($"Feature '{kv.Key}' jamais utilisée : envisager de la désactiver.");
        }
        return suggestions;
    }

    /// <summary>Idée "Minimal install" — config ultra-minimale pour machines faibles.</summary>
    public void ApplyMinimalConfig()
    {
        _settings.Shared.Ai.Profiles.Use3BByDefault.Value = true;
        _settings.Shared.Ai.Profiles.LowRamMode.Value = true;
        _settings.Shared.Editor.UxAdvanced.MicroUxAnimations.Value = false;
        _settings.Shared.Editor.UxAdvanced.FluidInteractions.Value = false;
        _log.Info("MotoSelfCare", "Configuration minimale appliquée");
    }

    /// <summary>Idée "Self-audit" — rapport sur le pipeline IA.</summary>
    public void RecordCall(double responseTimeMs, bool success)
    {
        _responseTimesMs.Add(responseTimeMs);
        if (!success) _errorCount++;
        if (_responseTimesMs.Count > 100) _responseTimesMs.RemoveAt(0);
    }

    public SelfAuditReport RunSelfAudit()
    {
        double avg = _responseTimesMs.Count > 0
            ? _responseTimesMs.Average()
            : 0;

        var report = new SelfAuditReport
        {
            AverageResponseMs = avg,
            ErrorCount = _errorCount,
            TotalCalls = _responseTimesMs.Count,
            Summary = avg < 500 ? "Pipeline IA sain." : "Pipeline IA lent : envisager un modèle plus léger."
        };

        // Consigne dans architecture-decisions style
        try
        {
            var entry = $"## Self-Audit {DateTime.Now:yyyy-MM-dd HH:mm}\n\n" +
                       $"- Réponse moyenne : {avg:F0} ms\n" +
                       $"- Erreurs : {_errorCount}\n" +
                       $"- Appels : {_responseTimesMs.Count}\n" +
                       $"- Verdict : {report.Summary}\n\n---\n\n";
            File.AppendAllText(AuditLogPath, entry);
        }
        catch { /* Optionnel */ }

        return report;
    }
}
