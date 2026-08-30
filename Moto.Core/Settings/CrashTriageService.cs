using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.DevOps;

public sealed class CrashReport
{
    public string StackTrace { get; set; } = "";
    public string ExceptionType { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? GroupKey { get; set; }
}

/// <summary>
/// Item 95 — Crash triage automation : regroupe les rapports de crash
/// et suggère la cause racine probable.
/// </summary>
public sealed class CrashTriageService
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly List<CrashReport> _reports = new();

    public CrashTriageService(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public void Report(CrashReport crash)
    {
        if (!SettingsCatalog.DevOps.CrashTriageEnabled.Value) return;
        crash.GroupKey = ComputeGroupKey(crash);
        _reports.Add(crash);
        _log.Info("CrashTriage", "Crash groupé", new { crash.GroupKey, crash.ExceptionType });
    }

    /// <summary>Regroupe par type d'exception + première frame de la stack.</summary>
    private string ComputeGroupKey(CrashReport crash)
    {
        var firstFrame = crash.StackTrace.Split('\n').FirstOrDefault()?.Trim() ?? "unknown";
        return $"{crash.ExceptionType}|{firstFrame.GetHashCode()}";
    }

    public IReadOnlyDictionary<string, int> GetGroupedCounts()
    {
        return _reports.GroupBy(r => r.GroupKey)
                       .ToDictionary(g => g.Key ?? "unknown", g => g.Count());
    }

    public string SuggestRootCause(CrashReport crash)
    {
        return crash.ExceptionType switch
        {
            "System.NullReferenceException" => "Référence null : vérifier l'initialisation de l'objet.",
            "System.OutOfMemoryException" => "Mémoire insuffisante : activer UltraLiteMode ou réduire le modèle.",
            "System.IO.IOException" => "Erreur I/O : vérifier les permissions/chemins.",
            _ => "Consulter la stack trace pour plus de détails."
        };
    }
}
